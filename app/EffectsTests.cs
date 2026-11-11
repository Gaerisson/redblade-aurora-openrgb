// SPDX-License-Identifier: GPL-2.0-or-later
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace RedBladePlus.Studio;
internal static class EffectsTests
{
    static void Check(bool b,string m) { if(!b) throw new Exception("EFFECTS TEST: "+m); }
    internal static async Task RunAsync()
    {
        var oldCompatible=ConfigCodec.Encode(StudioConfig.Default());
        Check(FlashVerification.CanRestoreTarget(oldCompatible,new Version(0,9,0)),"legacy-compatible profiles permit downgrade");
        var newer=(byte[])oldCompatible.Clone();newer[32+8]=30;
        Check(!FlashVerification.CanRestoreTarget(newer,new Version(0,9,0)) && FlashVerification.CanRestoreTarget(newer,new Version(0,10,0)),"new metadata blocks older target");
        newer=(byte[])oldCompatible.Clone();newer[32+3*416+1]=0x04;
        Check(!FlashVerification.CanRestoreTarget(newer,new Version(0,9,0)),"new feature flags block older target");
        await TestLightingOwnership();
        var priorMap=new ViaBackup { Layers=2, Keymap=new byte[2*6*21*2], Macros=new byte[]{9} };
        priorMap.Keymap[379]=1;
        var migrated=priorMap.ForFirmware(new Version(0,9,0),new Version(0,10,0),false);
        Check(migrated.Keymap[378]==0x7e && migrated.Keymap[379]==0x61 && priorMap.Keymap[378]==0 && priorMap.Keymap[379]==1,"Caps Word migration preserves backup");
        priorMap.Keymap[378]=0x12; priorMap.Keymap[379]=0x34;
        Check(priorMap.ForFirmware(new Version(0,9,0),new Version(0,10,0),false).Keymap[378]==0x12,"custom Fn Caps preserved");
        priorMap.Keymap[378]=0;priorMap.Keymap[379]=0;
        Check(priorMap.ForFirmware(new Version(0,9,0),new Version(0,10,0),true).Keymap[378]==0,"Core is not migrated");
        Check(priorMap.ForFirmware(new Version(0,10,0),new Version(0,10,0),false).Keymap[378]==0,"existing 0.10 mapping preserved");
        var validSettings = new EffectSettings("solid", "#FF4000", "#0000FF", 50, 255);
        var modernProfile=ConfigCodec.Encode(StudioConfig.Default());modernProfile[32+3*416+10]=2;
        Check(!FlashVerification.CanRestoreTarget(modernProfile,new Version(0,10,0))&&FlashVerification.CanRestoreTarget(modernProfile,new Version(0,11,0)),"autonomous styles block older targets in every profile");
        modernProfile[32+3*416+10]=0;modernProfile[32+3*416+11]=16;
        Check(!FlashVerification.CanRestoreTarget(modernProfile,new Version(0,10,0)),"new wave width blocks downgrade independently of style");
        var request = new JobRequest { Operation=HardwareOperation.Effects, RequestingUserSid="S-1-5-21-1-2-3-1001", EffectSettings=validSettings };
        HardwareJobs.Validate(request);
        request.Operation=HardwareOperation.Read;
        try { HardwareJobs.Validate(request); throw new Exception("effect settings accepted for read"); } catch(InvalidDataException) { }
        request.Operation=HardwareOperation.Effects; request.EffectSettings=null;
        try { HardwareJobs.Validate(request); throw new Exception("missing effect settings accepted"); } catch(InvalidDataException) { }
        string testDir=Path.Combine(Path.GetTempPath(),"redblade-effect-input-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(testDir);
        try
        {
            string path=Path.Combine(testDir,"effect.json");
            File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(validSettings));
            Check(System.Text.Json.JsonSerializer.Serialize(HardwareJobs.ReadEffectFile(path))==System.Text.Json.JsonSerializer.Serialize(validSettings),"live settings round trip");
            foreach(string bad in new[]{"{}","{\"Preset\":\"solid\",\"Color1\":\"#FFFFFF\",\"Color2\":\"#FFFFFF\",\"Speed\":1,\"Brightness\":255,\"script\":\"x\"}",new string('x',4097)})
            {
                File.WriteAllText(path,bad); bool rejected=false; try{HardwareJobs.ReadEffectFile(path);}catch(Exception e) when(e is IOException or InvalidDataException or ArgumentException or System.Text.Json.JsonException){rejected=true;} Check(rejected,"invalid live settings rejected");
            }
        }
        finally{Directory.Delete(testDir,true);}
        var advanced = validSettings with { Preset="spectrum", TargetLeds=new[]{0}, ReactiveEnabled=true, ReactiveStyle="key" };
        advanced.Validate();
        var geometry = KeyboardLayout.Load();
        var spectrum=EffectRenderer.Render(validSettings with{Preset="spectrum"},0,geometry);
        Check(spectrum[0]==255&&spectrum[1]==0&&spectrum[2]==0,"spectrum starts with standard red hue");
        var reverseSweep=EffectRenderer.Render(validSettings with{Preset="sweep",Speed=100,Reverse=true},500,geometry);
        int middle=geometry.MinBy(k=>Math.Abs(k.X-(geometry.Min(k=>k.X)+geometry.Max(k=>k.X))/2))!.Led;
        Check(reverseSweep[middle*3]>200,"reverse sweep travels across the keyboard at the mirrored phase");
        var maskFrame = EffectRenderer.Render(advanced with { Preset="solid" }, 100, geometry, new[]{new KeyActivity(0, 100)});
        Check(maskFrame[0]==255 && maskFrame[3]==0, "reactive overlay and target mask");
        Check(KeyboardScanCodes.ToLed(0x11, false, false)==43 && KeyboardScanCodes.ToLed(0x1d, true, false)==103, "physical scan code mapping");
        Check(KeyboardScanCodes.ToLed(0x45,false,true)==15 && KeyboardScanCodes.ToLed(0x56,false,false)==79, "E1 pause and ISO key");
        var reactive = advanced with { Preset="reactive", ReactiveStyle="key", TargetLeds=null };
        Check(EffectRenderer.Render(reactive,800,geometry,new[]{new KeyActivity(0,100)}).All(x=>x==0), "expired activity disappears");
        long now=100; using(var activity=new KeyboardActivity(()=>now)) { for(int i=0;i<40;i++)activity.Add(i,now); Check(activity.Events.Count==32 && activity.Events[0].Led==8,"activity bounded"); now=2200; Check(activity.Events.Count==0,"activity expires"); activity.SetEnabled(false); Check(activity.Events.Count==0,"disable clears activity"); }
        var ripple=reactive with { ReactiveStyle="ripple", ReactiveDurationMs=1000, ReactiveWidth=1 };
        var waveFrame=EffectRenderer.Render(ripple,100,geometry,new[]{new KeyActivity(0,0)});
        Check(waveFrame[3]>0 && waveFrame[0]==0, "ripple moves with age and distance");
        foreach(var bad in new[]{advanced with { TargetLeds=new[]{0,0} }, advanced with { ReactiveStyle="other" }, advanced with { Angle=360 }, advanced with { ReactiveWidth=0 }})
        { bool rejected=false; try { bad.Validate(); } catch(ArgumentException) { rejected=true; } Check(rejected,"invalid advanced effect rejected"); }
        foreach(var preset in new[]{"spectrum","twinkle","reactive"}) Check(EffectRenderer.Render(advanced with { Preset=preset },123,geometry).SequenceEqual(EffectRenderer.Render(advanced with { Preset=preset },123,geometry)),"advanced preset deterministic");
        bool badPalette=false; try { (advanced with { Palette=new[]{"#123456"} }).Validate(); } catch(ArgumentException) { badPalette=true; } Check(badPalette,"single color palette rejected");
        var layout = KeyboardLayout.Load();
        var s = new EffectSettings("solid", "#FF4000", "#0000FF", 50, 255);
        var frame = EffectRenderer.Render(s, 0, layout);
        Check(frame.Length == 327 && Enumerable.Range(0,109).All(i => frame[i*3]==255 && frame[i*3+1]==64 && frame[i*3+2]==0),"solid renders every LED");
        Check(EffectRenderer.Render(s with { Brightness = 0 }, 0, layout).All(x=>x==0),"zero brightness");
        try { (s with { Speed = 0 }).Validate(); throw new Exception("invalid speed accepted"); } catch (ArgumentException) { }
        foreach(var preset in new[]{"solid","breathing","wave","gradient","sweep","rain"})
            Check(EffectRenderer.Render(s with { Preset=preset },1234,layout).SequenceEqual(EffectRenderer.Render(s with { Preset=preset },1234,layout)),preset+" deterministic");
        try { OpenRgbSdk.ParseController(new byte[4],3); throw new Exception("invalid packet accepted"); } catch(InvalidDataException) { }
        var server=new TcpListener(IPAddress.Loopback,0); server.Start();
        try
        {
            int port=((IPEndPoint)server.LocalEndpoint).Port; using var cancel=new CancellationTokenSource(5000);
            var run=OpenRgbEffects.RunAsync(port,()=>s,null,cancel.Token);
            using var peer=await server.AcceptTcpClientAsync(); var stream=peer.GetStream();
            var p=await ReadPacket(stream); Check(p.id==40 && p.data.SequenceEqual(new byte[]{3,0,0,0}),"version request"); await Reply(stream,0,40,new byte[]{5,0,0,0});
            p=await ReadPacket(stream); Check(p.id==50 && p.data[^1]==0,"client name");
            p=await ReadPacket(stream); Check(p.id==0,"controller count request"); await Reply(stream,0,0,new byte[]{0,0,0,0});
            p=await ReadPacket(stream); Check(p.id==0,"retry when enumeration is incomplete"); await Reply(stream,0,100,Array.Empty<byte>()); await Reply(stream,0,0,new byte[]{1,0,0,0});
            p=await ReadPacket(stream); Check(p.id==0,"refresh list after notification"); await Reply(stream,0,0,new byte[]{1,0,0,0});
            p=await ReadPacket(stream); Check(p.id==1 && p.data.SequenceEqual(new byte[]{3,0,0,0}),"controller data request"); await Reply(stream,0,1,ControllerData());
            p=await ReadPacket(stream); Check(p.id==1100 && p.data.Length==0,"direct mode");
            p=await ReadPacket(stream); Check(p.id==1050 && p.data.Length==442 && BinaryPrimitives.ReadUInt32LittleEndian(p.data)==442 && BinaryPrimitives.ReadUInt16LittleEndian(p.data.AsSpan(4))==109,"frame header");
            Check(Enumerable.Range(0,109).All(i=>p.data[6+i*4]==255 && p.data[7+i*4]==64 && p.data[8+i*4]==0 && p.data[9+i*4]==0),"RGB wire order and all 109 LEDs");
            cancel.Cancel(); p=await ReadPacket(stream); Check(p.id==1050 && p.data.Skip(6).All(b=>b==0),"black on cancellation");
            try { await run; throw new Exception("cancellation ignored"); } catch(OperationCanceledException) { }
        }
        finally { server.Stop(); }
        var duplicate=new TcpListener(IPAddress.Loopback,0);duplicate.Start();
        try
        {
            using var timeout=new CancellationTokenSource(5000);using var sdk=new OpenRgbSdk();
            var connection=sdk.ConnectAsync(((IPEndPoint)duplicate.LocalEndpoint).Port,timeout.Token);
            using var peer=await duplicate.AcceptTcpClientAsync(timeout.Token);var stream=peer.GetStream();
            await ReadPacket(stream);await Reply(stream,0,40,new byte[]{3,0,0,0});await ReadPacket(stream);await connection;
            var find=sdk.FindKeyboardAsync(timeout.Token);await ReadPacket(stream);await Reply(stream,0,0,new byte[]{2,0,0,0});
            await ReadPacket(stream);await Reply(stream,0,1,ControllerData());await ReadPacket(stream);await Reply(stream,1,1,ControllerData());
            bool refused=false;try{await find;}catch(InvalidOperationException){refused=true;}Check(refused,"duplicate matching controllers rejected");
        }
        finally{duplicate.Stop();}
        string liveDir=Path.Combine(Path.GetTempPath(),"redblade-live-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(liveDir);
        var liveServer=new TcpListener(IPAddress.Loopback,0);liveServer.Start();
        try
        {
            using var lifetime=new CancellationTokenSource(10000);
            var worker=HardwareJobs.RunEffects(((IPEndPoint)liveServer.LocalEndpoint).Port,s,liveDir,_=>{},lifetime.Token);
            using var peer=await liveServer.AcceptTcpClientAsync(lifetime.Token);var stream=peer.GetStream();
            await ReadPacket(stream);await Reply(stream,0,40,new byte[]{3,0,0,0});await ReadPacket(stream);await ReadPacket(stream);await Reply(stream,0,0,new byte[]{1,0,0,0});await ReadPacket(stream);await Reply(stream,0,1,ControllerData());await ReadPacket(stream);await ReadPacket(stream);
            var next=s with { Color1="#0000FF" };File.WriteAllText(Path.Combine(liveDir,"effect.json"),System.Text.Json.JsonSerializer.Serialize(next));
            bool blue=false;for(int i=0;i<50&&!blue;i++){var f=await ReadPacket(stream);blue=f.data[6]==0&&f.data[8]==255;}Check(blue,"valid live edit reaches LED stream");
            File.WriteAllText(Path.Combine(liveDir,"effect.json"),"{invalid");await Task.Delay(250);
            var retained=await ReadPacket(stream);Check(retained.data[6]==0&&retained.data[8]==255,"invalid live edit retains previous frame");
            File.WriteAllText(Path.Combine(liveDir,"cancel"),"");
            var finished=await Task.WhenAny(worker,Task.Delay(3000));Check(finished==worker,"cancel file stops worker");await worker;
        }
        finally{liveServer.Stop();Directory.Delete(liveDir,true);}
        // An SDK-0 server ignores version requests; the client must reconnect cleanly.
        var legacy=new TcpListener(IPAddress.Loopback,0);legacy.Start();
        try
        {
            using var sdk=new OpenRgbSdk();using var timeout=new CancellationTokenSource(6000);
            var connect=sdk.ConnectAsync(((IPEndPoint)legacy.LocalEndpoint).Port,timeout.Token);
            using var first=await legacy.AcceptTcpClientAsync(timeout.Token); var request0=await ReadPacket(first.GetStream()); Check(request0.id==40,"legacy version probe");
            using var second=await legacy.AcceptTcpClientAsync(timeout.Token);var name=await ReadPacket(second.GetStream());Check(name.id==50,"legacy reconnect and client name");await connect;
            var find=sdk.FindKeyboardAsync(timeout.Token);var count=await ReadPacket(second.GetStream());Check(count.id==0,"legacy count request");await Reply(second.GetStream(),0,0,new byte[]{1,0,0,0});
            var device=await ReadPacket(second.GetStream());Check(device.id==1 && device.data.Length==0,"legacy controller request has no version payload");await Reply(second.GetStream(),0,1,ControllerData(0));Check(await find==0,"legacy controller selection");
        }
        finally{legacy.Stop();}
    }
    static async Task TestLightingOwnership()
    {
        foreach(byte profile in new byte[]{1,2,3})
        {
            var fake=new EffectFakeHid(false,new Version(0,10,0),profile,42,1);var snapshot=await EffectLightingSnapshot.Capture(fake,CancellationToken.None);
            await snapshot.Acquire(fake,CancellationToken.None);Check(fake.Profile==0&&fake.Master==255&&fake.Value==255,"Full switches to bright Host");
            await snapshot.Restore(fake,CancellationToken.None);Check(fake.Profile==profile&&fake.Master==42&&fake.Mode==1&&fake.Value==77,"Full restores runtime and scalars");
        }
        var standalone=new EffectFakeHid(false,new Version(0,10,0),0,66,2);var standaloneSnapshot=await EffectLightingSnapshot.Capture(standalone,CancellationToken.None);await standaloneSnapshot.Acquire(standalone,CancellationToken.None);await standaloneSnapshot.Restore(standalone,CancellationToken.None);Check(standalone.Profile==0&&standalone.Master==66&&standalone.Mode==2&&standalone.Value==77,"standalone mode restored");
        var core=new EffectFakeHid(true,new Version(0,10,0),0,255,1);var coreSnapshot=await EffectLightingSnapshot.Capture(core,CancellationToken.None);await coreSnapshot.Acquire(core,CancellationToken.None);await coreSnapshot.Restore(core,CancellationToken.None);Check(core.Value==77&&core.Mode==1&&core.ConfigWrites==0,"Core restores without Full commands");
        var coreOff=new EffectFakeHid(true,new Version(0,10,0),0,255,0);var coreOffSnapshot=await EffectLightingSnapshot.Capture(coreOff,CancellationToken.None);await coreOffSnapshot.Acquire(coreOff,CancellationToken.None);await coreOffSnapshot.Restore(coreOff,CancellationToken.None);Check(coreOff.Mode==0&&coreOff.ConfigWrites==0,"Core Off restored");
        var oldHost=new EffectFakeHid(false,new Version(0,8,0),0,42,1);var oldSnapshot=await EffectLightingSnapshot.Capture(oldHost,CancellationToken.None);await oldSnapshot.Acquire(oldHost,CancellationToken.None);await oldSnapshot.Restore(oldHost,CancellationToken.None);Check(oldHost.Profile==0&&oldHost.Master==42&&oldHost.ConfigWrites==0,"old Full Host restored without unsupported runtime command");
        var oldDark=new EffectFakeHid(false,new Version(0,8,0),0,0,1);bool darkRejected=false;try{await EffectLightingSnapshot.Capture(oldDark,CancellationToken.None);}catch(InvalidDataException){darkRejected=true;}Check(darkRejected&&oldDark.Writes==0,"old Full with zero master refused before writes");
        var old=new EffectFakeHid(false,new Version(0,8,0),3,42,2);bool rejected=false;try{await EffectLightingSnapshot.Capture(old,CancellationToken.None);}catch(InvalidDataException){rejected=true;}Check(rejected&&old.Writes==0,"old non-Host refused before writes");
    }
    sealed class EffectFakeHid(bool core,Version version,byte profile,byte master,byte mode):IHidTransport
    {
        internal byte Profile=profile,Master=master,Mode=mode,Value=77;internal int Writes,ConfigWrites;
        public void Dispose(){}
        public Task<byte[]> ExchangeAsync(byte[] request,CancellationToken ct)
        {
            var r=(byte[])request.Clone();
            if(request[0]==1){r[1]=0;r[2]=13;}
            else if(request[0]==0xfe&&request[1]==0x81){r[2]=1;r[3]=(byte)version.Major;r[4]=(byte)version.Minor;r[5]=(byte)version.Build;r[7]=Profile;r[8]=Master;r[9]=7;r[19]=(byte)(core?128:0);Encoding.ASCII.GetBytes("RDBLFS01").CopyTo(r,24);}
            else if(request[0]==8&&request[1]==0x41){r[2]=Mode;r[3]=0;r[4]=10;r[5]=20;r[6]=30;r[7]=Value;}
            else if(request[0]==7&&request[1]==0x41){Writes++;if(core&&request[2]>1){r[0]=0xff;return Task.FromResult(r);}Mode=request[2];if(Mode==2){Value=request[7];if(!core)Profile=0;}}
            else if(request[0]==0xfe&&request[1]==0x90){Writes++;ConfigWrites++;r[3]=0;Profile=request[4];Master=request[5];}
            return Task.FromResult(r);
        }
    }
    static byte[] ControllerData(int version=3)
    {
        using var ms=new MemoryStream(); using var w=new BinaryWriter(ms,Encoding.UTF8,true);
        w.Write(0); w.Write(0); Str(w,"RedBlade+ VialRGB"); if(version>=1) Str(w,"Vendor"); Str(w,"Keyboard"); Str(w,"1"); Str(w,""); Str(w,@"HID: \\?\hid#vid_320f&pid_5136");
        w.Write((ushort)0); w.Write(0); w.Write((ushort)0); w.Write((ushort)109);
        for(int i=0;i<109;i++) { Str(w,"LED "+i); w.Write(i); } w.Write((ushort)109); w.Write(new byte[436]);
        var b=ms.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(b,b.Length); return b;
    }
    static void Str(BinaryWriter w,string s) { var b=Encoding.UTF8.GetBytes(s); w.Write((ushort)(b.Length+1)); w.Write(b); w.Write((byte)0); }
    static async Task<(uint id,byte[] data)> ReadPacket(NetworkStream s)
    {
        using var timeout=new CancellationTokenSource(5000); byte[] h=new byte[16]; await s.ReadExactlyAsync(h,timeout.Token);
        Check(h.AsSpan(0,4).SequenceEqual("ORGB"u8),"packet magic"); int len=BinaryPrimitives.ReadInt32LittleEndian(h.AsSpan(12)); Check(len>=0 && len<=1000000,"packet length"); byte[] d=new byte[len]; await s.ReadExactlyAsync(d,timeout.Token); return(BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(8)),d);
    }
    static async Task Reply(NetworkStream s,uint device,uint id,byte[] data)
    {
        byte[] h=new byte[16]; Encoding.ASCII.GetBytes("ORGB").CopyTo(h,0); BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(4),device); BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(8),id); BinaryPrimitives.WriteInt32LittleEndian(h.AsSpan(12),data.Length);
        foreach(byte b in h.Concat(data)) await s.WriteAsync(new byte[]{b});
    }
}

