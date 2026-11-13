// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace RedBladePlus.Studio;

internal enum HardwareOperation { Read, Save, Preview, Flash, Control, Effects, SetupOpenRgb, TestOpenRgb, TestLighting }
internal enum HardwareControl { StopHelpers, Host, Local, Off }
internal sealed class JobRequest
{
    public int Schema{get;set;}=1;
    public HardwareOperation Operation{get;set;}
    public HardwareControl Control{get;set;}
    public EffectSettings? EffectSettings{get;set;}
    public string Payload{get;set;}="";
    public uint ExpectedGeneration{get;set;}
    public string Edition{get;set;}="full";
    public string RequestingUserSid{get;set;}="";
    public string FirmwareBase64{get;set;}="";
    public string FirmwareManifestBase64{get;set;}="";
    public string FirmwareSha256{get;set;}="";
    public bool AllowLegacyReset{get;set;}
}
internal sealed class JobResult
{
    public string Stage{get;set;}="prepare";
    public bool Done{get;set;}
    public bool Success{get;set;}
    public bool Programming{get;set;}
    public string Message{get;set;}="Preparing hardware operation…";
    public string Payload{get;set;}="";
    public uint Generation{get;set;}
    public string Firmware{get;set;}="";
    public bool Unsupported{get;set;}
    public string BackupDirectory{get;set;}="";
    public string OpenRgbStatus{get;set;}="";
    public bool RedbladeFound{get;set;}
    public int SdkPort{get;set;}
}
internal static class JobSafety
{
    internal static async Task WithCleanup(Func<Task> body,Func<Task> cleanup)
    {
        Exception? original=null;try{await body();}catch(Exception error){original=error;}
        try{await cleanup();}catch(Exception error){if(original!=null)throw new AggregateException("Operation failed and RGB restoration also needs attention.",original,error);throw;}
        if(original!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(original).Throw();
    }
}
internal static class FlashVerification
{
    internal static bool IsComplete(int exitCode,bool checksum,bool reconnected,bool settingsVerified)=>exitCode==0&&checksum&&reconnected&&settingsVerified;
    internal static bool CanRestoreTarget(byte[] payload,Version target)
    {
        if(payload.Length!=ConfigCodec.Length)throw new InvalidDataException("Invalid profile backup size.");
        for(int i=0;i<4;i++){int b=32+i*416;if(target<new Version(0,14,0)&&payload[b+14]!=0)return false;if(target<new Version(0,12,0)&&(payload[b+10]>2||payload[b+12]!=0||payload[b+13]!=0))return false;if(target<new Version(0,11,0)&&(payload[b+10]!=0||payload[b+11]!=0))return false;if(target<new Version(0,10,0)&&((payload[b+1]&0x0c)!=0||payload[b+8]!=0||payload[b+9]!=0))return false;}
        return true;
    }
    internal static bool SamePort(string original,string[] locations)=>locations.Length==1&&string.Equals(locations[0],original,StringComparison.OrdinalIgnoreCase);
}
internal static class HardwareJobs
{
    static readonly JsonSerializerOptions Json=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,MaxDepth=8};
    internal static string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RedBladePlusStudio","Jobs");
    internal static string Folder(string id,string? inputDirectory=null)
    {
        if(!Guid.TryParseExact(id,"N",out _))throw new InvalidDataException("Invalid job identifier.");
        if(inputDirectory!=null&&!Path.IsPathFullyQualified(inputDirectory))throw new InvalidDataException("Job directory must be an absolute path.");
        string folder=Path.GetFullPath(inputDirectory??Path.Combine(Root,id));
        if(!string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)),id,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Job directory does not match its identifier.");
        for(var directory=new DirectoryInfo(folder);directory!=null;directory=directory.Parent)
            if(directory.Exists&&(directory.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Job directory cannot use links or reparse points.");
        return folder;
    }
    internal static (string Id,Process Process) Start(JobRequest request)
    {
        request.RequestingUserSid=WindowsIdentity.GetCurrent().User?.Value??throw new IOException("Cannot identify requesting user.");
        Validate(request);var id=Guid.NewGuid().ToString("N");string directory=Folder(id);Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory,"request.json"),JsonSerializer.SerializeToUtf8Bytes(request,Json));
        try
        {
            // Both normal and already-elevated windows use an owner-bound broker.
            return(id,HardwareBroker.Submit(id));
        }
        catch{File.Delete(Path.Combine(directory,"request.json"));Directory.Delete(directory);throw;}
    }
    internal static bool RequiresElevation()
    {
        using var identity=WindowsIdentity.GetCurrent();
        return !new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
    internal static void Validate(JobRequest request)
    {
        if(request.Schema!=1||!Enum.IsDefined(request.Operation)||request.Edition is not("full" or "core")||request.Payload.Length>3000)throw new InvalidDataException("Invalid fixed hardware operation.");
        try{if(string.IsNullOrEmpty(request.RequestingUserSid)||request.RequestingUserSid.Length>184||new SecurityIdentifier(request.RequestingUserSid).Value!=request.RequestingUserSid)throw new InvalidDataException("Invalid requesting user SID.");}catch(ArgumentException error){throw new InvalidDataException("Invalid requesting user SID.",error);}
        if(!Enum.IsDefined(request.Control)||(request.Operation!=HardwareOperation.Control&&request.Control!=HardwareControl.StopHelpers))throw new InvalidDataException("Invalid hardware control.");
        if(request.Operation==HardwareOperation.Effects) { if(request.EffectSettings==null)throw new InvalidDataException("Missing effect settings."); try{request.EffectSettings.Validate();}catch(ArgumentException error){throw new InvalidDataException("Invalid effect settings.",error);} }
        else if(request.EffectSettings!=null)throw new InvalidDataException("Unexpected effect settings.");
        bool external=request.FirmwareBase64.Length>0||request.FirmwareManifestBase64.Length>0||request.FirmwareSha256.Length>0;
        if(external)
        {
            if(request.Operation!=HardwareOperation.Flash||request.FirmwareBase64.Length!=87384||request.FirmwareManifestBase64.Length>21848||request.FirmwareManifestBase64.Length==0||request.FirmwareSha256.Length!=64)throw new InvalidDataException("Invalid firmware job payload bounds.");
            FirmwareImage.Validate(Convert.FromBase64String(request.FirmwareBase64),Convert.FromBase64String(request.FirmwareManifestBase64),request.Edition,request.FirmwareSha256);
        }
        if(request.Operation is HardwareOperation.Save or HardwareOperation.Preview)ConfigCodec.Decode(Convert.FromBase64String(request.Payload));
        else if(request.Payload!="")throw new InvalidDataException("Unexpected job payload.");
        if(request.AllowLegacyReset&&request.Operation!=HardwareOperation.Flash)throw new InvalidDataException("Legacy migration consent is only valid for firmware installation.");
    }
    internal static JobResult? Poll(string id)
    {
        Folder(id);string path=Path.Combine(ProtectedStaging.JobPath(id),"result.json");if(!File.Exists(path))return null;
        try{return JsonSerializer.Deserialize<JobResult>(BoundedFile.Read(path,16384,snapshot:true),Json);}catch(Exception error) when(error is IOException or JsonException or UnauthorizedAccessException){return null;}
    }
    internal static void UpdateEffect(string id,EffectSettings settings)
    {
        settings.Validate();string directory=Folder(id),target=Path.Combine(directory,"effect.json"),temp=Path.Combine(directory,"effect-"+Guid.NewGuid().ToString("N")+".tmp");
        try{File.WriteAllBytes(temp,JsonSerializer.SerializeToUtf8Bytes(settings,Json));File.Move(temp,target,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    internal static EffectSettings ReadEffectFile(string path)
    {
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked effect settings refused.");
        var settings=JsonSerializer.Deserialize<EffectSettings>(BoundedFile.Read(path,4096,snapshot:true),Json)??throw new InvalidDataException("Empty effect settings.");
        settings.Validate();return settings;
    }
    internal static void Cancel(string id)=>File.WriteAllText(Path.Combine(Folder(id),"cancel"),"");
    internal static void WriteResult(string directory,JobResult result)
    {
        string temporary=Path.Combine(directory,"result.tmp"),target=Path.Combine(directory,"result.json");
        File.WriteAllBytes(temporary,JsonSerializer.SerializeToUtf8Bytes(result,Json));
        if(File.Exists(target))File.Replace(temporary,target,null);else File.Move(temporary,target);
    }
    internal static async Task<int> RunWorker(string id,string inputDirectoryPath,CancellationToken ownerLifetime=default,string? expectedSid=null)
    {
        string directory;
        try{directory=Folder(id,inputDirectoryPath);if(!Directory.Exists(directory))return 2;}catch{return 2;}
        var result=new JobResult();
        JobGate? gate=null;ProtectedStaging? protectedJob=null;string inputDirectory=directory;
        try
        {
            using var identity=WindowsIdentity.GetCurrent();if(!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new IOException("This explicit hardware job requires administrator access to pause the OpenRGB service.");
            gate=new JobGate();
            var path=Path.Combine(inputDirectory,"request.json");if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked request refused.");
            var bytes=BoundedFile.Read(path,131072);
            var request=JsonSerializer.Deserialize<JobRequest>(bytes,Json)??throw new InvalidDataException("Empty job.");Validate(request);
            if(expectedSid!=null&&!string.Equals(expectedSid,request.RequestingUserSid,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Job user differs from broker session owner.");
            ownerLifetime.ThrowIfCancellationRequested();
            protectedJob=new ProtectedStaging(id,request.RequestingUserSid);directory=protectedJob.Path;
            using var singleUse=new FileStream(Path.Combine(directory,"started"),FileMode.CreateNew,FileAccess.Write,FileShare.None);
            object progressLock=new();
            void Progress(string message){lock(progressLock){result.Message=message;WriteResult(directory,result);}}
            FirmwareImage? firmware=request.Operation==HardwareOperation.Flash?(request.FirmwareBase64.Length==0?FirmwareImage.Load(request.Edition):FirmwareImage.Validate(Convert.FromBase64String(request.FirmwareBase64),Convert.FromBase64String(request.FirmwareManifestBase64),request.Edition,request.FirmwareSha256)):null;
            if(request.Operation is HardwareOperation.SetupOpenRgb or HardwareOperation.TestOpenRgb)
            {
                using var sdkCancel=CancellationTokenSource.CreateLinkedTokenSource(ownerLifetime);sdkCancel.CancelAfter(TimeSpan.FromMinutes(8));result.Stage="openrgb";Progress("Checking OpenRGB and the RedBlade…");
                int found=await FindRunningKeyboard(sdkCancel.Token);
                if(found!=0){result.RedbladeFound=true;result.SdkPort=found;result.OpenRgbStatus="ready";result.Message=$"RedBlade detected in OpenRGB on port {found}.";result.Success=true;return 0;}
                if(request.Operation==HardwareOperation.TestOpenRgb){result.OpenRgbStatus="not_detected";throw new IOException("No OpenRGB server detected the RedBlade with 109 LEDs. Run OpenRGB setup and check the keyboard connection.");}
                using var setupRgb=new RgbSession();
                await JobSafety.WithCleanup(async()=>{await setupRgb.Pause(sdkCancel.Token);int port=setupRgb.ManagedPortAvailable;await setupRgb.StartManagedForSetup(request.RequestingUserSid,port,Progress,sdkCancel.Token);result.RedbladeFound=true;result.SdkPort=port;result.OpenRgbStatus="ready";result.Message=$"OpenRGB is ready and the RedBlade was detected on port {port}.";},()=>setupRgb.Restore());
                if(!await ProbeKeyboard(result.SdkPort,sdkCancel.Token)){result.RedbladeFound=false;result.OpenRgbStatus="not_detected";throw new IOException("The RedBlade became unavailable after restoring the other RGB applications.");}
                result.Success=true;return 0;
            }
            Progress("Pausing current RGB clients for this operation…");using var rgb=new RgbSession();
            using var cancel=CancellationTokenSource.CreateLinkedTokenSource(ownerLifetime);cancel.CancelAfter(request.Operation==HardwareOperation.Effects?TimeSpan.FromHours(12):request.Operation==HardwareOperation.Flash?TimeSpan.FromMinutes(10):TimeSpan.FromMinutes(3));
            await JobSafety.WithCleanup(async()=>
            {
                int selectedPort=request.Operation is HardwareOperation.Effects or HardwareOperation.TestLighting?await FindRunningKeyboard(cancel.Token):0;
                await rgb.Pause(cancel.Token,preferredPort:selectedPort);
                if(request.Operation is HardwareOperation.Effects or HardwareOperation.TestLighting)
                {
                    result.Stage="effects"; Progress("Starting OpenRGB effects…");
                    var effect=request.Operation==HardwareOperation.TestLighting?new EffectSettings("wave","#FF274E","#2080FF",50,100):request.EffectSettings!;
                    await RunOwnedEffects(rgb,effect,inputDirectory,request.RequestingUserSid,request.Operation==HardwareOperation.TestLighting,Progress,cancel.Token);
                    if(request.Operation==HardwareOperation.TestLighting){result.RedbladeFound=true;result.SdkPort=rgb.SdkPort;result.OpenRgbStatus="ready";}return;
                }
                if(request.Operation==HardwareOperation.Flash){await Flash(request,firmware!,directory,inputDirectory,result,Progress,cancel.Token);return;}
                using var hid=NativeHid.OpenKeyboard();var installed=await KeyboardProtocol.Identify(hid,cancel.Token);result.Firmware=installed.DisplayName;
                if(installed.Core||installed.Version<new Version(0,9,0))
                {
                    result.Unsupported=true;
                    if(request.Operation==HardwareOperation.Read){Progress(installed.Legacy?"Stock / legacy firmware identified; its version is unavailable. Install Full using the guided installer. Existing VIA data will be backed up privately; stock keycodes cannot be migrated automatically.":installed.Core?"OpenRGB Core identified. Full profile configuration is unavailable on Core.":"Older firmware identified. Upgrade to Full 0.9.0 or later to save profiles.");return;}
                    throw new InvalidDataException(installed.Core?"Core has no onboard Studio configuration. Install Full to enable it.":"This firmware needs the bundled Full upgrade before profile operations.");
                }
                var protocol=new ConfigProtocol(hid);
                switch(request.Operation)
                {
                    case HardwareOperation.Control:
                        Progress("Applying temporary keyboard control…");
                        if(request.Control==HardwareControl.StopHelpers)
                        {
                            var command=Wire.Packet(8);var response=await hid.ExchangeAsync(command,cancel.Token);
                            if(response.Length!=32||!response.AsSpan(0,3).SequenceEqual(command.AsSpan(0,3))||response[3]!=0)throw new InvalidDataException("Keyboard rejected stop command.");
                        }
                        else
                        {
                            var prior=await KeyboardProtocol.Via(hid,new byte[]{0xfe,0x81},cancel.Token,2);Wire.CheckSignature(prior);
                            if(prior[2]!=1||prior[7]>3||(prior[9]&~7)!=0)throw new InvalidDataException("Unsupported runtime lighting snapshot.");
                            byte mode=request.Control switch {HardwareControl.Host=>0,HardwareControl.Local=>1,HardwareControl.Off=>3,_=>throw new InvalidDataException("Invalid control.")};
                            await protocol.Temporary(mode,prior[8],(byte)(prior[9]&7),cancel.Token);
                        }
                        Progress("Temporary keyboard control applied.");break;
                    case HardwareOperation.Read:
                        Progress("Reading four onboard profiles…");var read=await protocol.Read(cancel.Token);result.Payload=Convert.ToBase64String(read.Bytes);result.Generation=read.Caps.Generation;Progress("Four profiles read. RGB control is being returned to your applications.");break;
                    case HardwareOperation.Save:
                        if(!FlashVerification.CanRestoreTarget(Convert.FromBase64String(request.Payload),installed.Version))throw new InvalidDataException("Profile settings require a newer Full firmware; modifier dimming requires Full 0.14.");
                        await SaveWithRecovery(protocol,ConfigCodec.Decode(Convert.FromBase64String(request.Payload)),request.ExpectedGeneration,Progress,cancel.Token);result.Generation=unchecked(request.ExpectedGeneration+1);result.Payload=request.Payload;break;
                    case HardwareOperation.Preview:
                        await Preview(hid,protocol,ConfigCodec.Decode(Convert.FromBase64String(request.Payload)),Progress,cancel.Token);break;
                }
            },()=>rgb.Restore());
            result.Success=true;return 0;
        }
        catch(Exception error){result.Success=false;result.Message=string.IsNullOrWhiteSpace(error.Message)?error.GetType().Name:error.Message;try{if(protectedJob!=null)File.WriteAllText(Path.Combine(directory,"diagnostic.log"),Diagnostic(error,result.Stage));}catch{}return 1;}
        finally{try{result.Done=true;if(result.Success)result.Stage="done";if(protectedJob!=null)WriteResult(directory,result);}finally{try{gate?.Dispose();}finally{protectedJob?.Dispose();}}}
    }
    static async Task<int> FindRunningKeyboard(CancellationToken ct)
    {
        foreach(int port in new[]{6742,6743})if(await ProbeKeyboard(port,ct))return port;return 0;
    }
    internal static async Task<bool> ProbeKeyboard(int port,CancellationToken ct)
    {
        try{using var sdk=new OpenRgbSdk();await sdk.ConnectAsync(port,ct);await sdk.FindKeyboardAsync(ct);return true;}
        catch(Exception e) when(e is SocketException or TimeoutException or EndOfStreamException){return false;}
        catch(OperationCanceledException) when(!ct.IsCancellationRequested){return false;}
    }
    static async Task RunOwnedEffects(RgbSession rgb,EffectSettings settings,string inputDirectory,string sid,bool test,Action<string> progress,CancellationToken cancel)
    {
        await rgb.EnsureSdkStopped(cancel);
        EffectLightingSnapshot snapshot;using(var hid=NativeHid.OpenKeyboard())snapshot=await EffectLightingSnapshot.Capture(hid,cancel);
        bool resumeAttempted=false;
        await JobSafety.WithCleanup(async()=>
        {
            using(var hid=NativeHid.OpenKeyboard())await snapshot.Acquire(hid,cancel);
            resumeAttempted=true;await rgb.ResumeOpenRgbForEffects(sid,progress,cancel);
            if(test){using var timer=CancellationTokenSource.CreateLinkedTokenSource(cancel);bool streaming=false;try{await OpenRgbEffects.RunAsync(rgb.SdkPort,()=>settings,new InlineProgress(message=>{progress(message);if(message=="OpenRGB connected"){streaming=true;timer.CancelAfter(TimeSpan.FromSeconds(6));}}),timer.Token);}catch(OperationCanceledException) when(streaming&&!cancel.IsCancellationRequested&&timer.IsCancellationRequested){progress("Lighting test complete. Restoring your RGB applications…");}}
            else await RunEffects(rgb.SdkPort,settings,inputDirectory,progress,cancel);
        },async()=>
        {
            using var restore=new CancellationTokenSource(TimeSpan.FromSeconds(35));
            if(resumeAttempted)await rgb.PauseOpenRgbForEffects(restore.Token);
            using var hid=NativeHid.OpenKeyboard();await snapshot.Restore(hid,restore.Token);
        });
    }
    internal sealed class InlineProgress(Action<string> report):IProgress<string> { public void Report(string value)=>report(value); }
    internal static async Task RunEffects(int port,EffectSettings initial,string inputDirectory,Action<string> progress,CancellationToken lifetime)
    {
        EffectSettings current=initial;using var stop=CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        string path=Path.Combine(inputDirectory,"effect.json"),cancelPath=Path.Combine(inputDirectory,"cancel");
        async Task PollInputs()
        {
            DateTime last=DateTime.MinValue;long length=-1;
            while(!stop.IsCancellationRequested)
            {
                if(File.Exists(cancelPath)){stop.Cancel();return;}
                try
                {
                    var info=new FileInfo(path);
                    if(info.Exists&&(info.LastWriteTimeUtc!=last||info.Length!=length))
                    {
                        last=info.LastWriteTimeUtc;length=info.Length;
                        try{current=ReadEffectFile(path);}catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException){progress("Invalid effect update ignored; the previous settings remain active.");}
                    }
                }
                catch(Exception e) when(e is IOException or UnauthorizedAccessException){progress("Effect update could not be read; the previous settings remain active.");}
                await Task.Delay(100,stop.Token);
            }
        }
        var poll=PollInputs();
        try{await OpenRgbEffects.RunAsync(port,()=>current,new InlineProgress(progress),stop.Token);}
        catch(OperationCanceledException) when(!lifetime.IsCancellationRequested&&File.Exists(cancelPath)){progress("Effect stopped. Restoring your RGB applications…");}
        finally{stop.Cancel();try{await poll;}catch(OperationCanceledException){}}
    }
    static string Diagnostic(Exception error,string stage)
    {
        var lines=new List<string>{"Stage: "+stage};
        void Add(Exception e){lines.Add(e.GetType().FullName+" HRESULT=0x"+e.HResult.ToString("X8"));lines.Add(e.StackTrace??"(no stack)");if(e is AggregateException aggregate)foreach(var inner in aggregate.InnerExceptions)Add(inner);else if(e.InnerException!=null)Add(e.InnerException);}
        Add(error);return string.Join(Environment.NewLine,lines);
    }
    static async Task SaveWithRecovery(ConfigProtocol protocol,StudioConfig config,uint generation,Action<string> progress,CancellationToken cancel)
    {
        try{await protocol.Save(config,generation,progress,cancel);}
        catch(Exception original)
        {
            if(!protocol.SaveMayHaveBegun)throw;
            // A timeout closes the original transport. Abort safely through a new validated handle,
            // never replay the uncertain write. Aborting cannot undo a completed commit.
            bool aborted=false;
            try{using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5));using var fresh=NativeHid.OpenKeyboard();var recovery=new ConfigProtocol(fresh);await recovery.Capabilities(cleanup.Token);await recovery.Abort(cleanup.Token);aborted=true;}catch{}
            throw new IOException(original.Message+(aborted?" Incomplete storage transaction aborted. Read the keyboard again before retrying.":" Read the keyboard again; if storage remains busy, reconnect the keyboard before retrying."),original);
        }
    }
    internal static async Task Preview(IHidTransport hid,ConfigProtocol protocol,StudioConfig config,Action<string> progress,CancellationToken cancel,TimeSpan? duration=null)
    {
        var prior=await KeyboardProtocol.Via(hid,new byte[]{0xfe,0x81},cancel,2);Wire.CheckSignature(prior);if(prior[2]!=1||prior[7]>3||(prior[9]&~7)!=0)throw new InvalidDataException("Unsupported runtime lighting snapshot.");
        var vial=await KeyboardProtocol.Via(hid,new byte[]{8,0x41},cancel,2);if(Wire.U16(vial,2)>2)throw new InvalidDataException("Unsupported prior RGB mode.");var p=config.Profiles[config.Active];
        await JobSafety.WithCleanup(async()=>
        {
            await KeyboardProtocol.Via(hid,new byte[]{7,0x41,2,0,vial[4],vial[5],vial[6],255},cancel,2);
            await KeyboardProtocol.Via(hid,new byte[]{7,0x41,1,0},cancel,2);
            await protocol.Temporary(0,p.Master,(byte)(p.Modules&7),cancel);progress("Temporary per-key preview · 5 seconds · saved profiles are unchanged.");
            var until=DateTime.UtcNow+(duration??TimeSpan.FromSeconds(5));
            while(DateTime.UtcNow<until){for(int first=0;first<109;first+=9){int count=Math.Min(9,109-first);var packet=new byte[32];packet[0]=7;packet[1]=0x42;Wire.Put16(packet,2,first);packet[4]=(byte)count;for(int i=0;i<count;i++){var color=p.Keys[first+i];packet[5+i*3]=color.H;packet[6+i*3]=color.S;packet[7+i*3]=color.V;}await KeyboardProtocol.Via(hid,packet,cancel,5);}await Task.Delay(100,cancel);}
        },async()=>
        {
            using var restore=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            // Direct/Off ignore scalar writes, so restore scalars through standalone first.
            var scalars=new byte[]{7,0x41,2,0,vial[4],vial[5],vial[6],vial[7]};
            await KeyboardProtocol.Via(hid,scalars,restore.Token,2);
            await protocol.Temporary(prior[7],prior[8],(byte)(prior[9]&7),restore.Token);
            var original=new byte[8];original[0]=7;original[1]=0x41;vial.AsSpan(2,6).CopyTo(original.AsSpan(2));
            await KeyboardProtocol.Via(hid,original,restore.Token,2);
        });
        progress("Temporary preview finished; original runtime lighting restored.");
    }
    static async Task Flash(JobRequest request,FirmwareImage image,string directory,string inputDirectory,JobResult result,Action<string> progress,CancellationToken cancel)
    {
        using var protectedStaging=new ProtectedStaging();string staging=protectedStaging.Path;image.Extract(staging);
        if(!CryptographicOperations.FixedTimeEquals(SHA256.HashData(BoundedFile.Read(Path.Combine(staging,"firmware.bin"),65536)),SHA256.HashData(image.Bytes)))throw new InvalidDataException("Protected firmware copy verification failed.");
        result.Stage="backup";progress("Preparing private backup…");
        ViaBackup backup;ReadConfiguration? custom=null;KeyboardIdentity previous;
        using(var hid=NativeHid.OpenKeyboard())
        {
            previous=await KeyboardProtocol.Identify(hid,cancel);progress("Backing up VIA layers and macros privately before bootloader entry…");backup=await ViaBackup.Read(hid,previous,cancel);
            if(!previous.Core&&previous.Version>=new Version(0,9,0))custom=await new ConfigProtocol(hid).Read(cancel);
        }
        string backupDirectory=Path.Combine(directory,"private-backup");Directory.CreateDirectory(backupDirectory);
        await File.WriteAllBytesAsync(Path.Combine(backupDirectory,"via.json"),JsonSerializer.SerializeToUtf8Bytes(backup),cancel);
        if(custom!=null)await File.WriteAllBytesAsync(Path.Combine(backupDirectory,"onboard.bin"),custom.Bytes,cancel);
        result.BackupDirectory=backupDirectory;
        if(previous.Legacy&&!request.AllowLegacyReset)throw new InvalidDataException("Stock / legacy VIA data was backed up privately. Connect the keyboard first, then restart the guided installer and acknowledge that the stock keymap/macros cannot be restored automatically. No firmware was programmed.");
        if(custom!=null&&request.Edition=="full"&&!FlashVerification.CanRestoreTarget(custom.Bytes,image.Version))throw new InvalidDataException("The backed-up profiles contain settings unsupported by this older Full firmware. Flashing stopped; the private backup was retained.");
        var originalLocations=NativeHid.UsbLocations("320f&pid_5136");
        if(originalLocations.Length!=1)throw new IOException("A unique Redblade USB port could not be identified. Flashing refused.");
        string originalLocation=originalLocations[0];
        if(NativeHid.CountBootloaders()!=0)throw new IOException("A Sonix bootloader is already connected. Disconnect it before starting the flash.");
        result.Stage="waitingboot";progress("Backup complete. Hold Fn + Esc for 2 seconds now. Waiting up to 2 minutes for the bootloader…");
        var deadline=DateTime.UtcNow.AddMinutes(2);bool found=false;
        while(DateTime.UtcNow<deadline)
        {
            cancel.ThrowIfCancellationRequested();if(File.Exists(Path.Combine(inputDirectory,"cancel")))throw new OperationCanceledException("Flash cancelled before programming. Private backup retained.");
            int count=NativeHid.CountBootloaders();if(count>1)throw new IOException("Several matching Sonix bootloaders are connected. Disconnect the others before flashing.");if(count==1&&NativeHid.CountKeyboards()==0){var locations=NativeHid.UsbLocations("0c45&pid_7040");if(!FlashVerification.SamePort(originalLocation,locations))throw new IOException("Bootloader USB port does not match the original Redblade. Flashing refused.");found=true;break;}await Task.Delay(400,cancel);
        }
        if(!found)throw new TimeoutException("No unique Sonix bootloader appeared. No firmware was programmed.");
        string helper=Path.Combine(staging,"sonixflasher.exe");var info=await RunFlasher(helper,new[]{"-v","0c45/7040","--info"},staging,false,progress,cancel);
        if(info.Exit!=0||!info.Output.Contains("SN32F24XB",StringComparison.Ordinal)||!Regex.IsMatch(info.Output,@"ROM size:\s+64 KB")||!Regex.IsMatch(info.Output,@"Security level:\s+CS0"))throw new InvalidDataException("Bootloader must identify SN32F24XB, 64 KB and CS0. Programming refused.");
        var finalLocations=NativeHid.UsbLocations("0c45&pid_7040");if(NativeHid.CountBootloaders()!=1||!FlashVerification.SamePort(originalLocation,finalLocations))throw new IOException("Bootloader identity became ambiguous.");
        if(File.Exists(Path.Combine(inputDirectory,"cancel")))throw new OperationCanceledException("Flash cancelled before programming.");
        result.Stage="program";result.Programming=true;progress("Programming and verifying firmware. Keep the keyboard connected; this operation cannot be cancelled.");
        // Once started, cancellation never kills the programming process.
        var flashed=await RunFlasher(helper,new[]{"-v","0c45/7040","-f",Path.Combine(staging,"firmware.bin"),"-o","0"},staging,true,progress,CancellationToken.None);
        result.Programming=false;bool checksums=flashed.Output.Contains("Programming verified successfully",StringComparison.Ordinal)&&flashed.Output.Contains("Checksum verified successfully",StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(directory,"flash.log"),flashed.Output);
        if(flashed.Exit!=0||!checksums)throw new IOException("Firmware programming/checksum verification failed. Private backups and flash log were retained; use recovery guidance.");
        result.Stage="verify";progress("Firmware checksum verified. Waiting for the normal keyboard interface…");
        var connection=await ReconnectVerifiedAsync(()=>NativeHid.OpenKeyboard(),image.Version,request.Edition=="core",TimeSpan.FromSeconds(30),TimeSpan.FromMilliseconds(500),CancellationToken.None);
        var reconnected=connection.Hid;
        using(reconnected)
        {
            using var verify=new CancellationTokenSource(TimeSpan.FromMinutes(2));var current=connection.Identity;result.Firmware=current.DisplayName;result.Unsupported=current.Core||current.Version<new Version(0,9,0);
            result.Stage="restore";progress("Firmware identity verified. Checking VIA preservation…");
            if(previous.Legacy&&request.AllowLegacyReset)progress("New firmware verified. Stock VIA backup retained for manual migration; the new factory keymap is active as acknowledged before flashing.");
            else await backup.ForFirmware(previous.Version,current.Version,previous.Core||current.Core).RestoreIfChanged(reconnected,current,progress,verify.Token);
            if(custom!=null&&!current.Core)
            {
                var protocol=new ConfigProtocol(reconnected);var actual=await protocol.Read(verify.Token);
                if(!actual.Bytes.SequenceEqual(custom.Bytes)){progress("Restoring the four backed-up onboard profiles…");await SaveWithRecovery(protocol,ConfigCodec.Decode(custom.Bytes),actual.Caps.Generation,progress,verify.Token);}
            }
            if(!current.Core&&current.Version>=new Version(0,9,0)){var read=await new ConfigProtocol(reconnected).Read(verify.Token);result.Payload=Convert.ToBase64String(read.Bytes);result.Generation=read.Caps.Generation;}
            if(!FlashVerification.IsComplete(flashed.Exit,checksums,true,true))throw new IOException("Flash verification did not finish.");
        }
        progress(previous.Legacy?"Firmware and reconnection verified. New factory keymap active; stock VIA backup retained for manual migration. RGB applications are being restored.":custom!=null&&request.Edition=="core"?"Core flashed and verified. Full profile backup retained privately; Core cannot restore Full settings.":"Firmware, reconnection, VIA and supported onboard profiles verified. RGB applications are being restored.");
    }
    // Only identification reads are retried. Each attempt uses a fresh handle; no restore or programming is replayed.
    internal static async Task<(IHidTransport Hid,KeyboardIdentity Identity)> ReconnectVerifiedAsync(Func<IHidTransport> open,Version expectedVersion,bool expectedCore,TimeSpan timeout,TimeSpan delay,CancellationToken cancel)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel);deadline.CancelAfter(timeout);Exception? last=null;
        try
        {
            while(true)
            {
                deadline.Token.ThrowIfCancellationRequested();IHidTransport? hid=null;KeyboardIdentity? identity=null;
                try
                {
                    hid=open();identity=await KeyboardProtocol.Identify(hid,deadline.Token);
                    if(identity.Legacy)throw new InvalidDataException("The keyboard returned an unsupported identity while reconnecting.");
                }
                catch(Exception error) when(error is IOException or InvalidDataException){last=error;identity=null;hid?.Dispose();}
                catch{hid?.Dispose();throw;}
                if(identity!=null)
                {
                    if(identity.Core!=expectedCore||identity.Version!=expectedVersion){hid!.Dispose();throw new InvalidDataException("Reconnected firmware edition/version does not match the flashed image. Backups retained.");}
                    return(hid!,identity);
                }
                await Task.Delay(delay,deadline.Token);
            }
        }
        catch(OperationCanceledException) when(!cancel.IsCancellationRequested)
        {throw new TimeoutException("Flasher checksum passed, but keyboard identity could not be verified within the reconnect window. Backups retained. Last HID error: "+last?.Message,last);}
    }
    static async Task<(int Exit,string Output)> RunFlasher(string helper,string[] arguments,string directory,bool programming,Action<string> progress,CancellationToken cancel)
    {
        var start=new ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=directory,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string argument in arguments)start.ArgumentList.Add(argument);
        using var process=Process.Start(start)??throw new IOException("Bundled flasher could not start.");
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        if(programming)
        {
            var wait=process.WaitForExitAsync();var notice=Task.Delay(TimeSpan.FromSeconds(30));if(await Task.WhenAny(wait,notice)==notice)progress("Programming is still running. Keep the keyboard connected; waiting for the flasher to finish.");await wait;
        }
        else
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel);timeout.CancelAfter(TimeSpan.FromSeconds(30));try{await process.WaitForExitAsync(timeout.Token);}catch{try{process.Kill(true);await process.WaitForExitAsync();}catch{}throw;}
        }
        string text=Regex.Replace((await output)+(await error),@"\x1B\[[0-?]*[ -/]*[@-~]","");if(text.Length>262144)text=text[^262144..];return(process.ExitCode,text);
    }
}
internal sealed class ViaBackup
{
    public int Schema{get;set;}=1;public string Board{get;set;}="RDBLFS01";public int ViaProtocol{get;set;}public int Layers{get;set;}public string Firmware{get;set;}="";public byte[] Keymap{get;set;}=Array.Empty<byte>();public byte[] Macros{get;set;}=Array.Empty<byte>();
    internal ViaBackup ForFirmware(Version previous,Version target,bool core)
    {
        var copy=new ViaBackup { Schema=Schema,Board=Board,ViaProtocol=ViaProtocol,Layers=Layers,Firmware=Firmware,Keymap=(byte[])Keymap.Clone(),Macros=(byte[])Macros.Clone() };
        const int fnCaps=378;
        if(!core&&previous<new Version(0,10,0)&&target>=new Version(0,10,0)&&Layers>=2&&copy.Keymap.Length>fnCaps+1&&copy.Keymap[fnCaps]==0&&copy.Keymap[fnCaps+1]<=1)
        {copy.Keymap[fnCaps]=0x7e;copy.Keymap[fnCaps+1]=0x61;}
        const int fnIdle=(6*21+2*21+8)*2;
        if(!core&&previous<new Version(0,13,0)&&target>=new Version(0,13,0)&&Layers>=2&&copy.Keymap.Length>fnIdle+1&&copy.Keymap[fnIdle]==0&&copy.Keymap[fnIdle+1]<=1)
        {copy.Keymap[fnIdle]=0x7e;copy.Keymap[fnIdle+1]=0x62;}
        return copy;
    }
    static async Task<byte[]> ReadBuffer(IHidTransport hid,byte command,int size,CancellationToken cancel)
    {
        var bytes=new byte[size];for(int offset=0;offset<size;offset+=28){int count=Math.Min(28,size-offset);var r=await KeyboardProtocol.Via(hid,new[]{command,(byte)(offset>>8),(byte)offset,(byte)count},cancel,4);r.AsSpan(4,count).CopyTo(bytes.AsSpan(offset));}return bytes;
    }
    internal static async Task<ViaBackup> Read(IHidTransport hid,KeyboardIdentity identity,CancellationToken cancel)
    {
        int layers=(await KeyboardProtocol.Via(hid,new byte[]{0x11},cancel))[1];var macro=await KeyboardProtocol.Via(hid,new byte[]{0x0d},cancel);int size=(macro[1]<<8)|macro[2];if(layers is <1 or >16||size>16384)throw new InvalidDataException("Invalid VIA dimensions.");
        return new(){ViaProtocol=identity.ViaProtocol,Layers=layers,Firmware=identity.Version.ToString(),Keymap=await ReadBuffer(hid,0x12,layers*6*21*2,cancel),Macros=await ReadBuffer(hid,0x0e,size,cancel)};
    }
    static async Task WriteChanged(IHidTransport hid,byte readCommand,byte writeCommand,byte[] expected,CancellationToken cancel)
    {
        var actual=await ReadBuffer(hid,readCommand,expected.Length,cancel);
        for(int offset=0;offset<expected.Length;offset+=28){int count=Math.Min(28,expected.Length-offset);if(actual.AsSpan(offset,count).SequenceEqual(expected.AsSpan(offset,count)))continue;var packet=new byte[32];packet[0]=writeCommand;packet[1]=(byte)(offset>>8);packet[2]=(byte)offset;packet[3]=(byte)count;expected.AsSpan(offset,count).CopyTo(packet.AsSpan(4));await KeyboardProtocol.Via(hid,packet,cancel,4);}
        if(!(await ReadBuffer(hid,readCommand,expected.Length,cancel)).SequenceEqual(expected))throw new IOException("VIA restore failed complete readback. Private backup retained.");
    }
    internal async Task RestoreIfChanged(IHidTransport hid,KeyboardIdentity identity,Action<string> progress,CancellationToken cancel)
    {
        var after=await Read(hid,identity,cancel);if(after.ViaProtocol!=ViaProtocol||after.Layers!=Layers||after.Macros.Length!=Macros.Length)throw new InvalidDataException($"VIA dimensions changed: backup protocol {ViaProtocol}, {Layers} layers, {Macros.Length} macro bytes; keyboard protocol {after.ViaProtocol}, {after.Layers} layers, {after.Macros.Length} macro bytes. Automatic restore refused; private backup retained.");
        if(!after.Keymap.SequenceEqual(Keymap)||!after.Macros.SequenceEqual(Macros))
        {
            if(!System.Version.TryParse(Firmware,out var previous)||previous<new System.Version(0,4,0)||ViaProtocol!=13)throw new InvalidDataException("The previous firmware keycode generation is unknown. Automatic VIA restore refused; private backup retained for manual migration.");
            progress("Restoring changed VIA data from the private backup…");await WriteChanged(hid,0x12,0x13,Keymap,cancel);await WriteChanged(hid,0x0e,0x0f,Macros,cancel);
        }
    }
}
// This directory is created atomically with a protected DACL. Its parent and the
// directory itself remain open without delete sharing while executable files run.
internal sealed class JobGate:IDisposable
{
    readonly ManualResetEventSlim release=new();readonly Thread thread;
    internal JobGate()
    {
        using var ready=new ManualResetEventSlim();Exception? failure=null;
        thread=new Thread(()=>
        {
            try
            {
                using var mutex=new Mutex(false,@"Global\RedBladePlusStudio-HardwareJob-v1");
                bool acquired;try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
                if(!acquired)throw new IOException("Another Studio hardware job is already running.");
                ready.Set();try{release.Wait();}finally{mutex.ReleaseMutex();}
            }
            catch(Exception e){failure=e;try{ready.Set();}catch(ObjectDisposedException){}}
        }){IsBackground=true};thread.Start();ready.Wait();
        if(failure!=null){thread.Join();release.Dispose();throw new IOException("Cannot acquire hardware job lock.",failure);}
    }
    public void Dispose(){release.Set();thread.Join();release.Dispose();}
}

internal sealed class ProtectedStaging:IDisposable
{
    [StructLayout(LayoutKind.Sequential)] struct SecurityAttributes{internal int Length;internal IntPtr Descriptor;[MarshalAs(UnmanagedType.Bool)]internal bool Inherit;}
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string s,uint revision,out IntPtr descriptor,out uint size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CreateDirectory(string path,ref SecurityAttributes attributes);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
    readonly Microsoft.Win32.SafeHandles.SafeFileHandle parent,folder;readonly bool keep;
    internal string Path{get;}
    internal static string JobPath(string id)=>System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"RedBladePlusStudio-job-"+id);
    // OWNER RIGHTS suppresses the owner's implicit WRITE_DAC on inherited files.
    // Administrators and SYSTEM retain full access through their explicit ACEs.
    internal static string SecurityDescriptor(string? readerSid)=>"O:BAG:BAD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;RC;;;OW)"+(readerSid==null?"":"(A;OICI;GRGX;;;"+readerSid+")");
    internal ProtectedStaging(string? jobId=null,string? readerSid=null)
    {
        string basePath=Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        parent=OpenDirectory(basePath);
        keep=jobId!=null;Path=keep?JobPath(jobId!):System.IO.Path.Combine(basePath,"RedBladePlusStudio-"+Guid.NewGuid().ToString("N"));
        string? reader=keep?(readerSid??throw new IOException("Cannot identify job user.")):null;
        if(!ConvertStringSecurityDescriptorToSecurityDescriptor(SecurityDescriptor(reader),1,out var sd,out _)){parent.Dispose();throw new IOException("Cannot build protected staging security descriptor.");}
        try
        {
            var sa=new SecurityAttributes{Length=Marshal.SizeOf<SecurityAttributes>(),Descriptor=sd};
            if(!CreateDirectory(Path,ref sa))throw new IOException("Cannot create protected staging directory (Windows error "+Marshal.GetLastWin32Error()+").");
            folder=OpenDirectory(Path);
        }
        catch{parent.Dispose();throw;}
        finally{LocalFree(sd);}
    }
    static Microsoft.Win32.SafeHandles.SafeFileHandle OpenDirectory(string path)
    {
        var h=CreateFile(path,0x80,3,IntPtr.Zero,3,0x02200000,IntPtr.Zero);
        if(h.IsInvalid){h.Dispose();throw new IOException("Cannot secure staging directory.");}
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0){h.Dispose();throw new IOException("Linked staging directory refused.");}
        return h;
    }
    public void Dispose(){folder.Dispose();try{if(!keep)Directory.Delete(Path,true);}finally{parent.Dispose();}}
}

internal sealed class FirmwareImage
{
    internal byte[] Bytes=Array.Empty<byte>();internal Version Version=new(0,0,0);internal string Edition="full";
    internal static FirmwareImage Load(string edition)
    {
        return Validate(Embedded.Read(edition+".bin"),Embedded.Read(edition+".manifest.json"),edition);
    }
    internal static FirmwareImage Validate(byte[] bytes,byte[] manifest,string edition,string? expectedSha256=null)
    {
        if(edition is not("full" or "core")||manifest.Length is 0 or >16384||bytes.Length!=65536)throw new InvalidDataException("Invalid firmware edition or size.");
        using var document=JsonDocument.Parse(manifest);var m=document.RootElement;
        string hash=Convert.ToHexString(SHA256.HashData(bytes));
        if(m.GetProperty("board").GetString()!="Designed by GG Redblade FS ISO"||m.GetProperty("mcu").GetString()!="SN32F248B"||m.GetProperty("usb_vid").GetString()!="320f"||m.GetProperty("usb_pid").GetString()!="5136"||m.GetProperty("edition").GetString()!=edition||m.GetProperty("led_count").GetInt32()!=109||m.GetProperty("binary_bytes").GetInt32()!=65536||!bytes.AsSpan(bytes.Length-4).SequenceEqual(new byte[]{0x55,0x55,0xaa,0xaa})||!hash.Equals(m.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase)||(expectedSha256!=null&&!hash.Equals(expectedSha256,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Firmware identity, Sonix flag or SHA-256 failed.");
        if(!System.Version.TryParse(m.GetProperty("release").GetString()?.TrimStart('v'),out var version))throw new InvalidDataException("Invalid firmware version.");
        return new(){Bytes=bytes,Edition=edition,Version=version};
    }
    internal void Extract(string directory)
    {
        var (helper,usb)=ValidateHelpers();
        File.WriteAllBytes(Path.Combine(directory,"sonixflasher.exe"),helper);File.WriteAllBytes(Path.Combine(directory,"libusb-1.0.dll"),usb);File.WriteAllBytes(Path.Combine(directory,"firmware.bin"),Bytes);
    }
    internal static (byte[] Helper,byte[] Usb) ValidateHelpers()
    {
        var helper=Embedded.Read("sonixflasher.exe");var usb=Embedded.Read("libusb-1.0.dll");
        if(!Convert.ToHexString(SHA256.HashData(helper)).Equals("741A87EB5ADA23E7B6380D9B07B74FDCE0ECC8A9E807A29B8F72415D73DEEED6",StringComparison.Ordinal)||!Convert.ToHexString(SHA256.HashData(usb)).Equals("BC18F430B2639C2F63961A51724ADB910AC8ED4A045AD2DCFD504E67EA52C460",StringComparison.Ordinal))throw new InvalidDataException("Bundled flasher/helper hash failed.");
        return(helper,usb);
    }
}

// Snapshot and restore the Full runtime profile and VialRGB state before SDK ownership.
internal sealed class EffectLightingSnapshot(KeyboardIdentity identity,byte[] runtime,byte[] vial)
{
    internal static async Task<EffectLightingSnapshot> Capture(IHidTransport hid,CancellationToken ct)
    {
        var identity=await KeyboardProtocol.Identify(hid,ct);
        if(!identity.Core&&identity.Version<new Version(0,8,0))throw new InvalidDataException("Unsupported firmware. Upgrade Full firmware to use effects.");
        byte[] runtime=Array.Empty<byte>();
        if(!identity.Core)
        {
            runtime=await KeyboardProtocol.Via(hid,new byte[]{0xfe,0x81},ct,2);Wire.CheckSignature(runtime);
            if(runtime[2]!=1||runtime[7]>3||(runtime[9]&~7)!=0)throw new InvalidDataException("Unsupported runtime lighting snapshot.");
            if(identity.Version<new Version(0,9,0)&&(runtime[7]!=0||runtime[8]==0))throw new InvalidDataException("Select Host with Fn+F1 or upgrade to Full 0.10 before starting software effects.");
        }
        var vial=await KeyboardProtocol.Via(hid,new byte[]{8,0x41},ct,2);
        if(Wire.U16(vial,2)>(identity.Core?1:2))throw new InvalidDataException("Unsupported prior VialRGB mode.");
        return new(identity,runtime,vial);
    }
    internal async Task Acquire(IHidTransport hid,CancellationToken ct)
    {
        if(identity.Core)return; // Core implements Direct and Off only, with no standalone scalar mode.
        await KeyboardProtocol.Via(hid,new byte[]{7,0x41,2,0,vial[4],vial[5],vial[6],255},ct,2);
        if(identity.Version>=new Version(0,9,0))await new ConfigProtocol(hid).Temporary(0,255,(byte)(runtime[9]&7),ct);
    }
    internal async Task Restore(IHidTransport hid,CancellationToken ct)
    {
        var errors=new List<Exception>();
        if(!identity.Core)try{await KeyboardProtocol.Via(hid,new byte[]{7,0x41,2,0,vial[4],vial[5],vial[6],vial[7]},ct,2);}catch(Exception e){errors.Add(e);}
        if(!identity.Core&&identity.Version>=new Version(0,9,0))try{await new ConfigProtocol(hid).Temporary(runtime[7],runtime[8],(byte)(runtime[9]&7),ct);}catch(Exception e){errors.Add(e);}
        try{var original=new byte[8];original[0]=7;original[1]=0x41;vial.AsSpan(2,6).CopyTo(original.AsSpan(2));await KeyboardProtocol.Via(hid,original,ct,2);}catch(Exception e){errors.Add(e);}
        if(errors.Count>0)throw new AggregateException("Original keyboard lighting could not be fully restored.",errors);
    }
}

internal sealed class RgbSession:IDisposable
{
    sealed record RunningApp(string Name,string Path,string[] Arguments,Microsoft.Win32.SafeHandles.SafeAccessTokenHandle Token);
    readonly List<RunningApp> apps=new();bool serviceWasRunning,paused,sdkWasReachable,serviceStartedForEffects,keepManaged;int port=6742,managedPort;Process? managedProcess;ManagedOpenRgb? managed;
    readonly HashSet<int> originalSdkPorts=new();
    internal static int ChooseManagedPort(IEnumerable<int> occupied)
    {
        var reserved=occupied.ToHashSet();foreach(int candidate in new[]{6742,6743})if(!reserved.Contains(candidate))return candidate;
        throw new IOException("Both supported OpenRGB SDK ports are already in use. Existing servers were preserved; close an unused OpenRGB server and retry setup.");
    }
    internal int ManagedPortAvailable=>ChooseManagedPort(originalSdkPorts);
    internal int SdkPort=>port;
    internal bool OriginalSdkWasReachable=>sdkWasReachable;
    internal async Task Pause(CancellationToken cancel,bool keepOpenRgb=false,int preferredPort=0)
    {
        int session=Process.GetCurrentProcess().SessionId;
        serviceWasRunning=OpenRgbService.IsRunning();
        using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\OpenRGB"))port=PortFromArgs(Arguments(key?.GetValue("ImagePath") as string??""));
        // Capture every original command before stopping anything. Commands never enter logs/job JSON.
        foreach(string name in new[]{"OpenRGB","AuroraRgb","AuroraDeviceManager"})
            foreach(var process in Process.GetProcessesByName(name))using(process)
            {
                if(process.SessionId!=session)continue;
                string path=process.MainModule?.FileName??throw new IOException("Cannot preserve RGB executable path.");
                int processId=process.Id;string[] args=Arguments(await Task.Run(()=>CommandLine(processId),cancel).WaitAsync(TimeSpan.FromSeconds(8),cancel));if(args.Length==0||!File.Exists(path))throw new IOException("Cannot preserve RGB startup command. No clients were paused.");
                var arguments=args.Skip(1).ToArray();if(name=="OpenRGB")port=PortFromArgs(arguments,port);if(!keepOpenRgb||name!="OpenRGB")apps.Add(new(name,path,arguments,PreserveToken(process)));
            }
        if(preferredPort!=0)port=preferredPort;
        // Remember every reachable original SDK, not just the instance selected
        // for this keyboard. Paused servers must be able to reclaim their ports.
        var candidates=apps.Where(a=>a.Name=="OpenRGB").Select(a=>PortFromArgs(a.Arguments)).Append(port).Concat(new[]{6742,6743}).Distinct();
        foreach(int candidate in candidates)
        {
            using var portProbe=CancellationTokenSource.CreateLinkedTokenSource(cancel);portProbe.CancelAfter(1000);using var socket=new TcpClient();
            try{await socket.ConnectAsync("127.0.0.1",candidate,portProbe.Token);originalSdkPorts.Add(candidate);}
            catch(OperationCanceledException) when(!cancel.IsCancellationRequested){}catch(SocketException){}
        }
        // Only require SDK recovery if it was actually reachable before interruption.
        // A running OpenRGB GUI does not imply that its SDK server is enabled.
        using(var probe=CancellationTokenSource.CreateLinkedTokenSource(cancel))
        {
            probe.CancelAfter(TimeSpan.FromSeconds(1));using var client=new TcpClient();
            try{await client.ConnectAsync("127.0.0.1",port,probe.Token);sdkWasReachable=true;}
            catch(OperationCanceledException) when(!cancel.IsCancellationRequested){}
            catch(SocketException){}
        }
        paused=true;
        if(!keepOpenRgb&&serviceWasRunning)await OpenRgbService.SetRunning(false,cancel);
        if(keepOpenRgb&&!sdkWasReachable&&!serviceWasRunning&&OpenRgbService.Exists())
        {
            serviceStartedForEffects=true;await OpenRgbService.SetRunning(true,cancel);
            bool ready=false;for(int n=0;n<50&&!ready;n++){using var probe=new TcpClient();try{await probe.ConnectAsync("127.0.0.1",port,cancel);ready=true;}catch(SocketException){await Task.Delay(200,cancel);}}
            if(!ready)throw new IOException($"OpenRGB SDK did not start on port {port}.");
        }
        foreach(var app in apps.Where(a=>!keepOpenRgb||a.Name!="OpenRGB"))foreach(var process in Process.GetProcessesByName(app.Name))using(process)
        {
            if(process.SessionId!=session)continue;try{process.CloseMainWindow();using var grace=CancellationTokenSource.CreateLinkedTokenSource(cancel);grace.CancelAfter(1200);await process.WaitForExitAsync(grace.Token);}catch(OperationCanceledException){if(!process.HasExited)process.Kill();await process.WaitForExitAsync(cancel);}
        }
    }
    internal Task EnsureSdkStopped(CancellationToken ct)=>EnsurePortStopped(port,ct);
    static async Task EnsurePortStopped(int selectedPort,CancellationToken ct)
    {
        // A stopped GUI/service can briefly retain its listener while child threads
        // exit. Allow it to settle; never terminate an unknown provider to take a port.
        for(int attempt=0;attempt<12;attempt++)
        {
            ct.ThrowIfCancellationRequested();using var probe=CancellationTokenSource.CreateLinkedTokenSource(ct);probe.CancelAfter(500);using var client=new TcpClient();
            try{await client.ConnectAsync("127.0.0.1",selectedPort,probe.Token);}
            catch(OperationCanceledException) when(!ct.IsCancellationRequested){return;}catch(SocketException){return;}
            await Task.Delay(250,ct);
        }
        throw new IOException($"Another OpenRGB SDK provider is still active on port {selectedPort}; exclusive keyboard control refused.");
    }
    internal async Task StartManagedForSetup(string sid,int selectedPort,Action<string> progress,CancellationToken ct)
    {
        managed=await OpenRgbSetup.EnsureInstalledAsync(sid,new HardwareJobs.InlineProgress(progress),ct);managedPort=selectedPort;
        await StartManaged(ct);
        if(!await HardwareJobs.ProbeKeyboard(selectedPort,ct))throw new IOException("OpenRGB started, but the RedBlade was not detected. Check its USB connection and firmware.");
        keepManaged=true;port=selectedPort;
    }
    async Task StartManaged(CancellationToken ct)
    {
        await EnsurePortStopped(managedPort,ct);
        var start=new ProcessStartInfo(managed!.Executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(managed.Executable)!};
        foreach(string arg in new[]{"--config",managed.ConfigDirectory,"--noautoconnect","--server","--server-host","127.0.0.1","--server-port",managedPort.ToString(System.Globalization.CultureInfo.InvariantCulture)})start.ArgumentList.Add(arg);
        managedProcess=Process.Start(start)??throw new IOException("Managed OpenRGB did not start.");
        for(int n=0;n<100;n++){ct.ThrowIfCancellationRequested();if(managedProcess.HasExited)throw new IOException("Managed OpenRGB exited before its SDK was ready.");using var client=new TcpClient();try{await client.ConnectAsync("127.0.0.1",managedPort,ct);return;}catch(SocketException){await Task.Delay(200,ct);}}
        throw new TimeoutException("Managed OpenRGB SDK did not start.");
    }
    internal async Task ResumeOpenRgbForEffects(string sid,Action<string> progress,CancellationToken ct)
    {
        if(serviceWasRunning)await OpenRgbService.SetRunning(true,ct);
        if(!serviceWasRunning&&!sdkWasReachable&&OpenRgbService.Exists()){serviceStartedForEffects=true;await OpenRgbService.SetRunning(true,ct);}
        int session=Process.GetCurrentProcess().SessionId;
        var existing=apps.Where(a=>a.Name=="OpenRGB").Select(a=>a.Name).Distinct().ToDictionary(name=>name,name=>Process.GetProcessesByName(name).Count(p=>{using(p)return p.SessionId==session;}));
        foreach(var app in apps.Where(a=>a.Name=="OpenRGB"))Start(app,existing);
        if(serviceWasRunning||serviceStartedForEffects||apps.Any(a=>a.Name=="OpenRGB"))
        {
            for(int n=0;n<50;n++){using var client=new TcpClient();try{await client.ConnectAsync("127.0.0.1",port,ct);if(await HardwareJobs.ProbeKeyboard(port,ct))return;break;}catch(SocketException){await Task.Delay(200,ct);}}
            await PauseOpenRgbForEffects(ct);
        }
        int selected=ManagedPortAvailable;
        await StartManagedForSetup(sid,selected,progress,ct);
    }
    internal async Task PauseOpenRgbForEffects(CancellationToken ct)
    {
        if(serviceWasRunning||serviceStartedForEffects)await OpenRgbService.SetRunning(false,ct);
        int session=Process.GetCurrentProcess().SessionId;
        foreach(var process in Process.GetProcessesByName("OpenRGB"))using(process)
        {
            if(process.SessionId!=session)continue;try{process.CloseMainWindow();using var grace=CancellationTokenSource.CreateLinkedTokenSource(ct);grace.CancelAfter(1200);await process.WaitForExitAsync(grace.Token);}catch(OperationCanceledException) when(!ct.IsCancellationRequested){if(!process.HasExited)process.Kill();await process.WaitForExitAsync(ct);}
        }
        await EnsureSdkStopped(ct);
    }
    internal async Task Restore()
    {
        if(!paused)return;var errors=new List<Exception>();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
        int session=Process.GetCurrentProcess().SessionId;
        int managedId=managedProcess?.Id??-1;
        if(managedProcess!=null&&!keepManaged&&!managedProcess.HasExited){managedProcess.Kill();await managedProcess.WaitForExitAsync(timeout.Token);}
        var existing=apps.Select(a=>a.Name).Distinct().ToDictionary(name=>name,name=>Process.GetProcessesByName(name).Count(p=>{using(p)return p.SessionId==session&&p.Id!=managedId;}));
        try{if(serviceStartedForEffects)await OpenRgbService.SetRunning(false,timeout.Token);if(serviceWasRunning)await OpenRgbService.SetRunning(true,timeout.Token);foreach(var app in apps.Where(a=>a.Name=="OpenRGB"))Start(app,existing);}catch(Exception error){errors.Add(error);}
        foreach(int restoredPort in originalSdkPorts)
        {
            bool ready=false;for(int n=0;n<60&&!timeout.IsCancellationRequested;n++){using var client=new TcpClient();try{await client.ConnectAsync("127.0.0.1",restoredPort,timeout.Token);ready=true;break;}catch{await Task.Delay(300);}}if(!ready)errors.Add(new IOException($"OpenRGB SDK did not return on port {restoredPort}."));
        }
        if(keepManaged&&managedProcess!=null&&managedProcess.HasExited)try{await StartManaged(timeout.Token);}catch(Exception error){errors.Add(error);}
        foreach(var app in apps.Where(a=>a.Name!="OpenRGB"))try{Start(app,existing);}catch(Exception error){errors.Add(error);}
        if(errors.Count>0)throw new AggregateException("RGB restoration needs attention.",errors);
    }
    internal static bool ShouldWaitForSdk(bool wasReachable)=>wasReachable;
    static void Start(RunningApp app,Dictionary<string,int> existing)
    {
        if(existing.TryGetValue(app.Name,out int count)&&count>0){existing[app.Name]=count-1;return;}
        var command=new System.Text.StringBuilder(Quote(app.Path)+" "+string.Join(" ",app.Arguments.Select(Quote)));
        var startup=new StartupInfo{Size=Marshal.SizeOf<StartupInfo>(),Desktop="winsta0\\default",Flags=1,ShowWindow=0};
        if(!CreateProcessWithTokenW(app.Token,1,app.Path,command,0,IntPtr.Zero,Path.GetDirectoryName(app.Path),ref startup,out var process))throw new IOException("Could not restore "+app.Name+" with its original user token (Windows error "+Marshal.GetLastWin32Error()+").");
        CloseHandle(process.Thread);CloseHandle(process.Process);
    }
    public void Dispose(){foreach(var app in apps)app.Token.Dispose();}
    static Microsoft.Win32.SafeHandles.SafeAccessTokenHandle PreserveToken(Process process)
    {
        if(!OpenProcessToken(process.SafeHandle,0x000b,out var source))throw new IOException("Cannot preserve RGB process user token. No applications were paused.");
        using(source){if(!DuplicateTokenEx(source,0x02000000,IntPtr.Zero,2,1,out var token))throw new IOException("Cannot duplicate RGB process user token.");return token;}
    }
    // Windows argv quoting; no shell is involved and all arguments came from the captured process.
    static string Quote(string value)
    {
        var result=new System.Text.StringBuilder("\"");int slashes=0;foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='\"'){result.Append('\\',slashes*2+1);result.Append(c);}else{result.Append('\\',slashes);result.Append(c);}slashes=0;}result.Append('\\',slashes*2);return result.Append('"').ToString();
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct StartupInfo{internal int Size;internal string? Reserved,Desktop,Title;internal uint X,Y,XSize,YSize,XChars,YChars,Fill,Flags;internal ushort ShowWindow,ReservedSize;internal IntPtr ReservedData,Input,Output,Error;}
    [StructLayout(LayoutKind.Sequential)]struct ProcessInfo{internal IntPtr Process,Thread;internal uint ProcessId,ThreadId;}
    [DllImport("advapi32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool OpenProcessToken(Microsoft.Win32.SafeHandles.SafeProcessHandle process,uint access,out Microsoft.Win32.SafeHandles.SafeAccessTokenHandle token);
    [DllImport("advapi32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool DuplicateTokenEx(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle source,uint access,IntPtr attributes,int level,int type,out Microsoft.Win32.SafeHandles.SafeAccessTokenHandle token);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool CreateProcessWithTokenW(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle token,uint logon,string application,System.Text.StringBuilder command,uint flags,IntPtr environment,string? directory,ref StartupInfo startup,out ProcessInfo process);
    [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]static extern bool CloseHandle(IntPtr handle);
    static int PortFromArgs(string[] args,int fallback=6742){for(int n=0;n<args.Length;n++){string argument=args[n];if(argument=="--server-port"&&n+1<args.Length&&int.TryParse(args[n+1],out int p)&&p is >0 and <65536)return p;if(argument.StartsWith("--server-port=")&&int.TryParse(argument[14..],out int q)&&q is >0 and <65536)return q;}return fallback;}
    [StructLayout(LayoutKind.Sequential)]struct UnicodeString{internal ushort Length,MaximumLength;internal IntPtr Buffer;}
    [DllImport("ntdll.dll")]static extern int NtQueryInformationProcess(Microsoft.Win32.SafeHandles.SafeProcessHandle process,int informationClass,IntPtr buffer,int length,out int returnLength);
    internal static string CommandLine(int pid)
    {
        using var process=Process.GetProcessById(pid);
        int size=0;int status=NtQueryInformationProcess(process.SafeHandle,60,IntPtr.Zero,0,out size);
        if(size<Marshal.SizeOf<UnicodeString>()||size>131072)throw new IOException($"Cannot inspect RGB process command line (NTSTATUS 0x{status:X8}). No clients were paused.");
        IntPtr buffer=Marshal.AllocHGlobal(size);
        try
        {
            status=NtQueryInformationProcess(process.SafeHandle,60,buffer,size,out int returned);
            if(status<0||returned>size)throw new IOException($"Cannot inspect RGB process command line (NTSTATUS 0x{status:X8}). No clients were paused.");
            var text=Marshal.PtrToStructure<UnicodeString>(buffer);long offset=text.Buffer.ToInt64()-buffer.ToInt64();
            if((text.Length&1)!=0||text.Length>text.MaximumLength||offset<Marshal.SizeOf<UnicodeString>()||offset+text.Length>size)throw new IOException("Invalid RGB process command line. No clients were paused.");
            return Marshal.PtrToStringUni(text.Buffer,text.Length/2)??throw new IOException("Cannot preserve RGB process arguments.");
        }
        finally{Marshal.FreeHGlobal(buffer);}
    }
    static string[] Arguments(string command)
    {
        if(string.IsNullOrEmpty(command))return Array.Empty<string>();var pointer=CommandLineToArgvW(command,out int count);if(pointer==IntPtr.Zero)throw new IOException("Cannot parse RGB command line.");try{return Enumerable.Range(0,count).Select(i=>Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer,i*IntPtr.Size))??"").ToArray();}finally{LocalFree(pointer);}
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CommandLineToArgvW(string command,out int count);
    [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr memory);
}
internal static class OpenRgbService
{
    [StructLayout(LayoutKind.Sequential)]struct Status{internal uint Type,State,Accepted,Win32,Specific,Checkpoint,WaitHint,Pid,Flags;}
    internal static bool Exists(){using var service=Open(4,false);return service!=null;}
    internal static bool IsRunning(){using var service=Open(4,false);if(service==null)return false;return Query(service.DangerousGetHandle()).State==4;}
    internal static async Task SetRunning(bool running,CancellationToken cancel)
    {
        using var service=Open(4|16|32,true)!;var pointer=service.DangerousGetHandle();var current=Query(pointer);if(current.State==(running?4u:1u))return;
        if(running){if(!StartService(pointer,0,IntPtr.Zero)&&Marshal.GetLastWin32Error()!=1056)throw new IOException("Could not restart OpenRGB service.");}
        else if(!ControlService(pointer,1,out _)&&Marshal.GetLastWin32Error()!=1062)throw new IOException("Could not pause OpenRGB service.");
        var until=DateTime.UtcNow.AddSeconds(30);while(DateTime.UtcNow<until){cancel.ThrowIfCancellationRequested();if(Query(pointer).State==(running?4u:1u))return;await Task.Delay(200,cancel);}throw new TimeoutException("OpenRGB service transition timed out.");
    }
    static ServiceHandle? Open(uint access,bool required)
    {
        using var manager=new ServiceHandle(OpenSCManager(null,null,1));if(manager.IsInvalid)throw new IOException("Cannot inspect Windows services.");var handle=new ServiceHandle(OpenService(manager.DangerousGetHandle(),"OpenRGB",access));if(handle.IsInvalid){int error=Marshal.GetLastWin32Error();handle.Dispose();if(error==1060&&!required)return null;throw new IOException("Cannot access OpenRGB service.");}return handle;
    }
    static Status Query(IntPtr handle){if(!QueryServiceStatusEx(handle,0,out var status,Marshal.SizeOf<Status>(),out _))throw new IOException("Cannot query OpenRGB service.");return status;}
    sealed class ServiceHandle:Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
    {
        internal ServiceHandle(IntPtr value):base(true){SetHandle(value);}
        protected override bool ReleaseHandle()=>CloseServiceHandle(handle);
    }
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr OpenSCManager(string? machine,string? database,uint access);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr OpenService(IntPtr manager,string name,uint access);
    [DllImport("advapi32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool QueryServiceStatusEx(IntPtr service,int level,out Status status,int length,out int needed);
    [DllImport("advapi32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool ControlService(IntPtr service,uint control,out Status status);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool StartService(IntPtr service,int count,IntPtr arguments);
    [DllImport("advapi32.dll")][return:MarshalAs(UnmanagedType.Bool)]static extern bool CloseServiceHandle(IntPtr service);
}
