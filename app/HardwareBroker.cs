// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace RedBladePlus.Studio;

// Only fixed job identifiers cross the elevation boundary; requests are validated by RunWorker.
internal static class HardwareBroker
{
    static readonly object Sync=new();
    static Process? broker;
    static string? session,nonce,folder;
    internal static bool Ready
    {
        get{lock(Sync){try{return broker!=null&&!broker.HasExited&&session!=null&&File.Exists(Path.Combine(ProtectedStaging.JobPath("broker-"+session),"ready"));}catch(InvalidOperationException){return false;}}}
    }
    [DllImport("advapi32.dll",SetLastError=true)]
    static extern bool OpenProcessToken(IntPtr process,uint access,out SafeAccessTokenHandle token);

    internal static string SessionFolder(string id)
    {
        if(!Guid.TryParseExact(id,"N",out _))throw new InvalidDataException("Invalid broker session.");
        string path=Path.Combine(HardwareJobs.Root,"Sessions",id);
        for(var d=new DirectoryInfo(path);d!=null;d=d.Parent)
            if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked broker directory refused.");
        return path;
    }
    internal static string QueueContents(string job,string secret)
    {
        if(!Guid.TryParseExact(job,"N",out _)||!Guid.TryParseExact(secret,"N",out _))throw new InvalidDataException("Invalid broker queue entry.");
        return job+"\n"+secret;
    }
    internal static string ParseQueue(string fileName,byte[] bytes,string secret)
    {
        if(bytes.Length>4096)throw new InvalidDataException("Broker queue entry too large.");
        string name=Path.GetFileNameWithoutExtension(fileName);
        string text=Encoding.ASCII.GetString(bytes);
        if(!fileName.Equals(name+".job",StringComparison.Ordinal)||text!=QueueContents(name,secret))throw new InvalidDataException("Invalid broker queue entry.");
        return name;
    }
    internal static bool AcceptOnce(HashSet<string> seen,string job)
    {
        if(!seen.Add(job))return false;
        if(seen.Count>4096)throw new InvalidDataException("Too many broker jobs.");
        return true;
    }
    internal static Process Submit(string id)
    {
        lock(Sync)
        {
            if(broker==null||broker.HasExited)
            {
                broker?.Dispose();session=Guid.NewGuid().ToString("N");nonce=Guid.NewGuid().ToString("N");folder=SessionFolder(session);Directory.CreateDirectory(folder);SessionFolder(session);
                using var owner=Process.GetCurrentProcess();using var identity=WindowsIdentity.GetCurrent();
                var start=new ProcessStartInfo(Environment.ProcessPath??throw new IOException("Cannot locate Studio executable.")){UseShellExecute=true,Verb=HardwareJobs.RequiresElevation()?"runas":"",WindowStyle=ProcessWindowStyle.Hidden};
                foreach(var arg in new[]{"--broker",session,nonce,owner.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),owner.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),identity.User?.Value??throw new IOException("Cannot identify session user.")})start.ArgumentList.Add(arg);
                broker=Process.Start(start)??throw new IOException("Hardware broker did not start.");
            }
            string target=Path.Combine(folder!,id+".job"),tmp=Path.Combine(folder!,Guid.NewGuid().ToString("N")+".tmp");
            try{File.WriteAllText(tmp,QueueContents(id,nonce!),Encoding.ASCII);File.Move(tmp,target);}finally{if(File.Exists(tmp))File.Delete(tmp);}
            return Process.GetProcessById(broker.Id);
        }
    }
    internal static bool SameOwner(int actualSession,long actualTicks,string actualSid,string actualPath,int expectedSession,long expectedTicks,string expectedSid,string expectedPath)
        =>actualSession==expectedSession&&actualTicks==expectedTicks&&string.Equals(actualSid,expectedSid,StringComparison.OrdinalIgnoreCase)&&string.Equals(Path.GetFullPath(actualPath),Path.GetFullPath(expectedPath),StringComparison.OrdinalIgnoreCase);
    internal static async Task<int> Run(string sessionId,string secret,string pidText,string ticksText,string sid)
    {
        try
        {
            if(!Guid.TryParseExact(sessionId,"N",out _)||!Guid.TryParseExact(secret,"N",out _)||!int.TryParse(pidText,out int pid)||pid<=0||!long.TryParse(ticksText,out long ticks))return 2;
            using var identity=WindowsIdentity.GetCurrent();if(!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)||identity.User?.Value!=sid)return 2;
            using var owner=Process.GetProcessById(pid);using var current=Process.GetCurrentProcess();
            if(!OpenProcessToken(owner.Handle,8,out var token))return 2;
            using(token)using(var ownerIdentity=new WindowsIdentity(token.DangerousGetHandle()))
            {
                string? executable=owner.MainModule?.FileName;
                if(executable==null||ownerIdentity.User==null||!SameOwner(owner.SessionId,owner.StartTime.ToUniversalTime().Ticks,ownerIdentity.User.Value,executable,current.SessionId,ticks,sid,Environment.ProcessPath??""))return 2;
            }
            string directory=SessionFolder(sessionId);if(!Directory.Exists(directory))return 2;
            using var status=new ProtectedStaging("broker-"+sessionId,sid);
            File.WriteAllText(Path.Combine(status.Path,"ready"),"ready");
            using var lifetime=new CancellationTokenSource();
            var ownerExit=owner.WaitForExitAsync();
            _=ownerExit.ContinueWith(_=>{try{lifetime.Cancel();}catch(ObjectDisposedException){}},CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while(!lifetime.IsCancellationRequested)
            {
                SessionFolder(sessionId);
                foreach(string file in Directory.EnumerateFiles(directory,"*.job"))
                {
                    if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked queue entry refused.");
                    string job=ParseQueue(Path.GetFileName(file),BoundedFile.Read(file,4096),secret);
                    if(!AcceptOnce(seen,job))continue;
                    await HardwareJobs.RunWorker(job,HardwareJobs.Folder(job),lifetime.Token,sid);
                    if(HardwareJobs.Poll(job)?.Done!=true)return 1;
                    if(lifetime.IsCancellationRequested)break;
                }
                if(!lifetime.IsCancellationRequested)try{await Task.Delay(200,lifetime.Token);}catch(OperationCanceledException){}
            }
            return 0;
        }
        // This process is elevated: never append diagnostics into a user-writable
        // directory. Per-job errors already use protected ProgramData storage.
        catch{return 1;}
    }
}
