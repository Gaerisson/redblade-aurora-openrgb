// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics;
using System.Text.Json;
namespace RedBladePlus.Studio;

internal static class HardwareDiagnostics
{
    internal static int Run()
    {
        var rows=new List<object>();
        foreach(string name in new[]{"OpenRGB","AuroraRgb","AuroraDeviceManager"})
            foreach(var process in Process.GetProcessesByName(name))using(process)
            {
                if(process.SessionId!=Process.GetCurrentProcess().SessionId)continue;
                try
                {
                    bool readable=RgbSession.CommandLine(process.Id).Length>0;
                    rows.Add(new {process=name,readable});
                }
                catch(Exception error){rows.Add(new {process=name,readable=false,type=error.GetType().Name,error=error.Message});}
            }
        try{rows.Add(new{service="OpenRGB",running=OpenRgbService.IsRunning()});}catch(Exception error){rows.Add(new{service="OpenRGB",error=error.Message});}
        // No Raw HID requests, control writes, process stops, UAC or bootloader actions.
        string report=JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true});
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"hardware-diagnostics.json"),report);
        return 0;
    }
    internal static void Test()
    {
        if(!RgbSession.CommandLine(Environment.ProcessId).Contains("--self-test",StringComparison.Ordinal))throw new Exception("Native command-line capture failed for the test process.");
        byte[] binary=Embedded.Read("full.bin"),manifest=Embedded.Read("full.manifest.json");
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(binary));
        var image=FirmwareImage.Validate(binary,manifest,"full",hash);
        if(image.Bytes.Length!=65536)throw new Exception("Validated firmware size mismatch.");
        var request=new JobRequest{Operation=HardwareOperation.Flash,RequestingUserSid="S-1-5-21-1-2-3-1001",FirmwareBase64=Convert.ToBase64String(binary),FirmwareManifestBase64=Convert.ToBase64String(manifest),FirmwareSha256=hash};
        HardwareJobs.Validate(request);
        binary[^1]^=1;bool rejected=false;
        try{FirmwareImage.Validate(binary,manifest,"full",hash);}catch(InvalidDataException){rejected=true;}
        if(!rejected)throw new Exception("Corrupt external firmware was accepted.");
        foreach(HardwareControl control in Enum.GetValues<HardwareControl>())HardwareJobs.Validate(new JobRequest{Operation=HardwareOperation.Control,Control=control,RequestingUserSid="S-1-5-21-1-2-3-1001"});
    }
}
