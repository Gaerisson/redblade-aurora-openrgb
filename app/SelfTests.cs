using System.Text;
namespace RedBladePlus.Studio;
internal static class SelfTests
{
    static int assertions;
    static void Check(bool value, string name) { assertions++; if (!value) throw new Exception("SELF-TEST: " + name); }
    static void Reject(Action action, string name) { try { action(); } catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException or ArgumentException) { assertions++; return; } throw new Exception("Expected rejection: " + name); }
    internal static async Task<int> Run()
    {
        try
        {
            Check(ConfigCodec.Crc32(Encoding.ASCII.GetBytes("123456789")) == 0xcbf43926, "IEEE CRC vector");
            var config = StudioConfig.Default(); var bytes = ConfigCodec.Encode(config);
            Check(bytes.Length == 1696 && bytes[32] == 255 && bytes[33] == 3 && bytes[34] == 255 && bytes[37] == 50, "wire defaults");
            Check(ConfigCodec.Encode(ConfigCodec.Decode(bytes)).SequenceEqual(bytes), "four profiles wire roundtrip");
            var varied=StudioConfig.Default();varied.Active=3;varied.Profiles[3].Keys[108]=new(2,3,4);varied.Profiles[3].Effects[23]=new(5,6,7);var encoded=ConfigCodec.Encode(varied);
            Check(encoded[0]==3&&encoded[32+3*416+16+108*3]==2&&encoded[32+3*416+343+23*3]==5,"last profile/key/effect offsets");
            var bad = (byte[])bytes.Clone(); bad[1] = 1; Reject(() => ConfigCodec.Decode(bad), "global reserved");
            bad = (byte[])bytes.Clone(); bad[32 + 5] = 101; Reject(() => ConfigCodec.Decode(bad), "invalid turbo");
            bad = (byte[])bytes.Clone(); bad[32 + 415] = 1; Reject(() => ConfigCodec.Decode(bad), "profile reserved");
            Check(Hsv.FromColor(Color.Red) == new Hsv(0,255,255), "red RGB conversion");
            Check(ProfileFile.Load(ProfileFile.Save(config)).Profiles.Length == 4, "versioned profile roundtrip");
            var named=StudioConfig.Default();named.Profiles[0].Name="My user label";named.Profiles[0].GeneratedName=false;var namedFile=ProfileFile.Load(ProfileFile.Save(named));Check(namedFile.Profiles[0].Name=="My user label"&&!namedFile.Profiles[0].GeneratedName&&namedFile.Profiles[1].GeneratedName,"PC labels preserve user/generated identity");
            Reject(() => ProfileFile.Load(Encoding.UTF8.GetBytes("{\"$type\":\"unsafe\"}")), "unknown profile properties");
            Reject(() => ProfileFile.Load(Encoding.UTF8.GetBytes("{}")), "missing schema and required fields");
            Reject(() => ProfileFile.Load(new byte[ProfileFile.MaximumBytes + 1]), "profile size bound");
            string fileTest=Path.Combine(Path.GetTempPath(),"RedBladePlus-file-test-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fileTest);
            try
            {
                string current=Path.Combine(fileTest,"result.json");
                HardwareJobs.WriteResult(fileTest,new JobResult{Message="old"});
                using var snapshot=BoundedFile.OpenSnapshot(current);
                HardwareJobs.WriteResult(fileTest,new JobResult{Message="new"});
                using var reader=new StreamReader(snapshot);
                var latest=System.Text.Json.JsonSerializer.Deserialize<JobResult>(BoundedFile.Read(current,16384));
                Check(reader.ReadToEnd().Contains("old")&&latest?.Message=="new","UI polling retains its snapshot without blocking atomic worker results");
            }
            finally{Directory.Delete(fileTest,true);}
            var fake = new FakeHid(); var protocol = new ConfigProtocol(fake);
            await protocol.Save(config, uint.MaxValue, null, CancellationToken.None);
            Check(fake.Generation == 0 && fake.Writes == 71 && fake.Aborts == 0, "aligned save and generation wrap");
            Check((await protocol.Read(CancellationToken.None)).Bytes.SequenceEqual(bytes), "full readback");
            fake = new FakeHid { LoseWriteAck = true };
            try { await new ConfigProtocol(fake).Save(config, uint.MaxValue, null, CancellationToken.None); throw new Exception("lost ACK accepted"); }
            catch (IOException) { Check(fake.Writes == 1 && fake.Aborts == 1, "lost ACK is never replayed; abort attempted"); }
            fake=new FakeHid();protocol=new ConfigProtocol(fake);try{await protocol.Save(config,0,null,CancellationToken.None);throw new Exception("wrong generation accepted");}catch(InvalidDataException){Check(!protocol.SaveMayHaveBegun&&fake.Aborts==0&&fake.Writes==0,"generation rejection never aborts another transaction");}
            fake = new FakeHid { WrongResponse = true };
            try { await new ConfigProtocol(fake).Read(CancellationToken.None); throw new Exception("wrong response accepted"); }
            catch (InvalidDataException) { assertions++; }
            var restored = false;
            try { await JobSafety.WithCleanup(() => Task.FromException(new IOException("synthetic")), () => { restored = true; return Task.CompletedTask; }); } catch (IOException) { }
            Check(restored, "failure cleanup finally");
            var preview=new PreviewHid();await HardwareJobs.Preview(preview,new ConfigProtocol(preview),config,_=>{},CancellationToken.None,TimeSpan.Zero);
            Check(preview.Packets[^2].AsSpan(0,7).SequenceEqual(new byte[]{254,144,7,0,3,77,5}),"preview restores local Off/master/flags");
            Check(preview.Packets[^1].AsSpan(0,8).SequenceEqual(new byte[]{7,65,2,0,81,12,34,56}),"preview restores standalone mode/speed/HSV");
            preview=new PreviewHid{OriginalMode=1,OriginalValue=0};await HardwareJobs.Preview(preview,new ConfigProtocol(preview),config,_=>{},CancellationToken.None,TimeSpan.Zero);
            Check(preview.Packets[2][2]==2&&preview.Packets[2][7]==255&&preview.Packets[^3][2]==2&&preview.Packets[^3][7]==0&&preview.Packets[^1][2]==1,"preview normalizes zero scalar only temporarily and restores Direct");
            preview=new PreviewHid{FailDirect=true};try{await HardwareJobs.Preview(preview,new ConfigProtocol(preview),config,_=>{},CancellationToken.None,TimeSpan.Zero);throw new Exception("preview failure accepted");}catch(IOException){Check(preview.Packets[^1].AsSpan(0,8).SequenceEqual(new byte[]{7,65,2,0,81,12,34,56}),"preview failure still restores original standalone mode");}
            Check(!FlashVerification.IsComplete(0, true, false, true), "exit zero without reconnect is failure");
            Check(!FlashVerification.IsComplete(1, true, true, true), "nonzero flash exit is failure");
            Check(FlashVerification.IsComplete(0, true, true, true), "all flash gates required");
            Check(FlashVerification.SamePort("PCIROOT(0)#USB(1)",new[]{"pciroot(0)#usb(1)"}),"same physical USB location ignores case");
            Check(!FlashVerification.SamePort("port1",new[]{"port2"})&&!FlashVerification.SamePort("port1",Array.Empty<string>())&&!FlashVerification.SamePort("port1",new[]{"port1","port1"}),"missing, different or ambiguous bootloader locations rejected");
            Check(!RgbSession.ShouldWaitForSdk(false)&&RgbSession.ShouldWaitForSdk(true),"SDK restoration waits only for previously reachable server");
            Check(RgbSession.ChooseManagedPort(Array.Empty<int>())==6742&&RgbSession.ChooseManagedPort(new[]{6742})==6743&&RgbSession.ChooseManagedPort(new[]{6743})==6742,"managed SDK uses only an originally unoccupied port");
            bool portsRefused=false;try{RgbSession.ChooseManagedPort(new[]{6742,6743});}catch(IOException){portsRefused=true;}Check(portsRefused,"two occupied SDK ports cannot be taken by managed setup");
            foreach(var reader in new string?[]{null,"S-1-5-21-1-2-3-1001"})
            {
                var descriptor=new System.Security.AccessControl.RawSecurityDescriptor(ProtectedStaging.SecurityDescriptor(reader));
                var aces=descriptor.DiscretionaryAcl!.Cast<System.Security.AccessControl.CommonAce>().ToArray();
                var owner=aces.Single(a=>a.SecurityIdentifier.Value=="S-1-3-4");
                Check(owner.AccessMask==0x20000&&(owner.AceFlags&(System.Security.AccessControl.AceFlags.ObjectInherit|System.Security.AccessControl.AceFlags.ContainerInherit))==(System.Security.AccessControl.AceFlags.ObjectInherit|System.Security.AccessControl.AceFlags.ContainerInherit),"inherited OWNER RIGHTS grants READ_CONTROL without WRITE_DAC");
                Check(aces.Where(a=>a.SecurityIdentifier.Value is "S-1-5-18" or "S-1-5-32-544").Count(a=>a.AccessMask==0x1f01ff)==2,"SYSTEM and Administrators retain full control");
            }
            var job=new JobRequest{Operation=HardwareOperation.Read,RequestingUserSid="S-1-5-21-1-2-3-1001"};
            var jobCopy=System.Text.Json.JsonSerializer.Deserialize<JobRequest>(System.Text.Json.JsonSerializer.Serialize(job))!;HardwareJobs.Validate(jobCopy);
            Check(jobCopy.RequestingUserSid==job.RequestingUserSid,"request preserves the original user's SID across elevation");
            jobCopy.RequestingUserSid="S-1-5-21-1)(A;;FA;;;WD)";Reject(()=>HardwareJobs.Validate(jobCopy),"malformed SID cannot inject an ACE");
            jobCopy.RequestingUserSid=null!;Reject(()=>HardwareJobs.Validate(jobCopy),"null requesting SID is rejected");
            string jobId=Guid.NewGuid().ToString("N"),externalJob=Path.Combine(Path.GetTempPath(),jobId);
            Check(HardwareJobs.Folder(jobId,externalJob)==Path.GetFullPath(externalJob),"worker accepts the initiating user's explicit job directory");
            Reject(()=>HardwareJobs.Folder(jobId,Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"))),"worker rejects a directory for another job");
            Reject(()=>HardwareJobs.Folder(jobId,jobId),"worker rejects a relative job directory");
            var layout = KeyboardLayout.Load();
            foreach(string resource in new[]{"shortcuts.png","shortcuts.fr.png"})Check(Embedded.Read(resource).AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),"shortcut PNG in primary assembly: "+resource);
            byte[] bootstrapper=Embedded.Read("webview2-bootstrapper.exe");
            string bootstrapperHash=Encoding.ASCII.GetString(Embedded.Read("webview2-bootstrapper.sha256")).Trim();
            Check(bootstrapper.Length==1897688&&bootstrapper[0]=='M'&&bootstrapper[1]=='Z'&&Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bootstrapper)).Equals(bootstrapperHash,StringComparison.OrdinalIgnoreCase),"pinned WebView2 prerequisite is embedded and intact");
            Check(layout.Length == 109 && layout.Select(k => k.Led).Distinct().Count() == 109, "109 unique physical keys");
            Check(layout.Single(k => k.Led == 102).Label == "Fn", "physical Fn");
            Check(layout.Count(k => k.Kind == "media") == 4, "four media keys");
            Check(layout[16].Label=="Calc"&&layout[17].Label=="Mute"&&layout[18].Label=="Vol −"&&layout[19].Label=="Vol +", "actual upper-right media order");
            Check(KeyboardLayout.Legend(layout[43],true)=="Z"&&KeyboardLayout.Legend(layout[43],false)=="W","independent AZERTY/QWERTY legends preserve physical forward key");
            Check(FirmwareImage.Load("full").Bytes.Length==65536&&FirmwareImage.Load("core").Bytes.Length==65536,"bundled firmware SHA/identity/length");
            var helpers=FirmwareImage.ValidateHelpers();Check(helpers.Helper.Length>0&&helpers.Usb.Length>0,"bundled fixed flasher/DLL hashes");
            await UpdateTests.Run();SoftwareUpdateTests.Run();HardwareDiagnostics.Test();HardwareBrokerTests.Run();StudioBridgeTests.Run();StudioTextTests.Run();await EffectsTests.RunAsync();await ReconnectTests.Run();await IdentityTests.Run();await ViaBackupTests.Run();await OpenRgbSetupTests.RunAsync();EditorTests.Run();
            var report = $"PASS {assertions} hardware-free assertions plus update discovery/download/install regression suites. No HID, service or UAC operations were used; update launches were simulated.";
            Console.WriteLine(report); File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test-results.txt"), report + Environment.NewLine); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test-results.txt"), error.ToString()); return 1; }
    }
    sealed class PreviewHid:IHidTransport
    {
        internal List<byte[]> Packets=new();internal bool FailDirect;internal byte OriginalMode=2,OriginalValue=56;
        public Task<byte[]> ExchangeAsync(byte[] packet,CancellationToken cancel)
        {
            Packets.Add((byte[])packet.Clone());var response=(byte[])packet.Clone();
            if(FailDirect&&packet[0]==7&&packet[1]==65&&packet[2]==1)return Task.FromException<byte[]>(new IOException("synthetic temporary-mode failure"));
            if(packet[0]==254&&packet[1]==129){response[2]=1;response[7]=3;response[8]=77;response[9]=5;Wire.Sign(response);}
            if(packet[0]==8&&packet[1]==65)new byte[]{OriginalMode,0,81,12,34,OriginalValue}.CopyTo(response,2);
            return Task.FromResult(response);
        }
        public void Dispose(){}
    }
    sealed class FakeHid : IHidTransport
    {
        internal uint Generation = uint.MaxValue; internal int Writes, Aborts; internal bool LoseWriteAck, WrongResponse;
        byte[] active = ConfigCodec.Encode(StudioConfig.Default()), staging = new byte[1696]; int cursor;
        public Task<byte[]> ExchangeAsync(byte[] request, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested(); var response = new byte[32]; request.AsSpan(0,3).CopyTo(response);
            switch (request[2])
            {
                case 0: response[4]=1; response[5]=4; response[6]=109; response[7]=24; Wire.Put16(response,8,1696); Wire.Put16(response,10,416); Wire.Put32(response,12,Generation); response[17]=1; Wire.Sign(response); break;
                case 1: request.AsSpan(4,3).CopyTo(response.AsSpan(4)); active.AsSpan(Wire.U16(request,4),request[6]).CopyTo(response.AsSpan(8)); break;
                case 2: cursor=0; response[3]=1; break;
                case 3: response[4]=(byte)(cursor<1696?2:0); Wire.Put16(response,6,cursor); Wire.Put16(response,8,1696); Wire.Put32(response,10,Generation); response[15]=1; Wire.Sign(response); break;
                case 4: Check(Wire.U16(request,4)==cursor && request[6]%4==0, "sequential aligned write"); Writes++; request.AsSpan(8,request[6]).CopyTo(staging.AsSpan(cursor)); cursor+=request[6]; request.AsSpan(4,3).CopyTo(response.AsSpan(4)); Wire.Put16(response,8,cursor); if(LoseWriteAck) return Task.FromException<byte[]>(new IOException("ACK lost")); break;
                case 5: active=(byte[])staging.Clone(); Generation=unchecked(Generation+1); response[3]=1; break;
                case 6: Aborts++; break;
            }
            if(WrongResponse) response[0]=255;
            return Task.FromResult(response);
        }
        public void Dispose() { }
    }
}
