// SPDX-License-Identifier: GPL-2.0-or-later
using System.IO.Compression;
using System.Security.Cryptography;
namespace RedBladePlus.Studio;

internal static class OpenRgbSetupTests
{
    sealed class Handler:HttpMessageHandler
    {
        readonly byte[] data;readonly System.Net.HttpStatusCode status;readonly long? length;
        internal Handler(byte[] data,System.Net.HttpStatusCode status=System.Net.HttpStatusCode.OK,long? length=null){this.data=data;this.status=status;this.length=length;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {ct.ThrowIfCancellationRequested();var content=new ByteArrayContent(data);if(length.HasValue)content.Headers.ContentLength=length;return Task.FromResult(new HttpResponseMessage(status){RequestMessage=request,Content=content});}
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception("OPENRGB SETUP TEST: "+message);}
    static byte[] Archive(params string[] names)
    {
        using var stream=new MemoryStream();
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))foreach(string name in names){var entry=zip.CreateEntry(name);using var writer=new StreamWriter(entry.Open());writer.Write("data");}
        return stream.ToArray();
    }
    static byte[] SymlinkArchive()
    {
        using var stream=new MemoryStream();using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true)){var entry=zip.CreateEntry("OpenRGB Windows 64-bit/OpenRGB.exe");entry.ExternalAttributes=unchecked((int)0xa0000000);using var writer=new StreamWriter(entry.Open());writer.Write("link");}return stream.ToArray();
    }
    static byte[] HugeArchive()
    {
        var bytes=Archive("OpenRGB Windows 64-bit/OpenRGB.exe");
        for(int i=0;i<bytes.Length-28;i++)if(bytes[i]==0x50&&bytes[i+1]==0x4b&&bytes[i+2]==1&&bytes[i+3]==2){System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i+24,4),256u*1024*1024+1);return bytes;}
        throw new Exception("ZIP central directory missing");
    }
    internal static async Task RunAsync()
    {
        var good=Archive("OpenRGB Windows 64-bit/OpenRGB.exe","OpenRGB Windows 64-bit/platforms/qwindows.dll");
        string hash=Convert.ToHexString(SHA256.HashData(good));
        using(var input=new MemoryStream(good))Check(OpenRgbSetup.ValidateArchive(input,hash).Count==2,"valid archive");
        foreach(var bad in new[]{Archive("../evil"),Archive("OpenRGB Windows 64-bit/../evil","OpenRGB Windows 64-bit/OpenRGB.exe"),Archive("OpenRGB Windows 64-bit/CON.txt","OpenRGB Windows 64-bit/OpenRGB.exe"),Archive("OpenRGB Windows 64-bit/OpenRGB.exe","OpenRGB Windows 64-bit/openrgb.EXE"),Archive("OpenRGB Windows 64-bit/OpenRGB.exe","OpenRGB Windows 64-bit/OpenRGB.exe/evil"),Archive("OpenRGB Windows 64-bit/other.dll"),SymlinkArchive(),HugeArchive()})
        {
            bool rejected=false;try{using var input=new MemoryStream(bad);OpenRgbSetup.ValidateArchive(input,Convert.ToHexString(SHA256.HashData(bad)));}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid archive rejected");
        }
        bool wrongHash=false;try{using var input=new MemoryStream(good);OpenRgbSetup.ValidateArchive(input,new string('0',64));}catch(InvalidDataException){wrongHash=true;}Check(wrongHash,"hash mismatch rejected");
        using var http=new HttpClient(new Handler(good));var received=await OpenRgbSetup.DownloadAsync(http,new Uri(OpenRgbSetup.DownloadUrl),hash,CancellationToken.None);Check(received.SequenceEqual(good),"bounded download validated");
        bool hostRejected=false;try{await OpenRgbSetup.DownloadAsync(http,new Uri("https://example.com/fake.zip"),hash,CancellationToken.None);}catch(InvalidDataException){hostRejected=true;}Check(hostRejected,"untrusted host rejected");
        using(var oversized=new HttpClient(new Handler(good,length:OpenRgbSetup.MaximumDownload+1L))){bool rejected=false;try{await OpenRgbSetup.DownloadAsync(oversized,new Uri(OpenRgbSetup.DownloadUrl),hash,CancellationToken.None);}catch(InvalidDataException){rejected=true;}Check(rejected,"oversized download rejected");}
        using(var error=new HttpClient(new Handler(good,System.Net.HttpStatusCode.NotFound))){bool rejected=false;try{await OpenRgbSetup.DownloadAsync(error,new Uri(OpenRgbSetup.DownloadUrl),hash,CancellationToken.None);}catch(HttpRequestException){rejected=true;}Check(rejected,"HTTP errors rejected");}
        using(var json=System.Text.Json.JsonDocument.Parse(OpenRgbSetup.ConfigJson())){var root=json.RootElement;var device=root.GetProperty("QMKVialRGBDevices").GetProperty("devices")[0];Check(device.GetProperty("usb_vid").GetString()=="0x320F"&&device.GetProperty("usb_pid").GetString()=="0x5136","RedBlade VID/PID configured");var server=root.GetProperty("Server");Check(server.GetProperty("default_host").GetString()=="127.0.0.1"&&server.GetProperty("default_port").GetInt32()==6742,"local SDK configured");}
        using var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{await OpenRgbSetup.DownloadAsync(http,new Uri(OpenRgbSetup.DownloadUrl),hash,cancel.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"download cancellation propagated");
        string destination=Path.Combine(Path.GetTempPath(),"RedBlade-OpenRGB-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(destination);
        try{OpenRgbSetup.ExtractVerified(good,hash,destination);Check(File.Exists(Path.Combine(destination,"OpenRGB.exe"))&&File.Exists(Path.Combine(destination,"platforms","qwindows.dll")),"verified extraction");}
        finally{Directory.Delete(destination,true);}
    }
}
