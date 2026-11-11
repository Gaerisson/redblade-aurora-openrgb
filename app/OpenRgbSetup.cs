// SPDX-License-Identifier: GPL-2.0-or-later
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;
namespace RedBladePlus.Studio;

internal sealed record ManagedOpenRgb(string Executable,string ConfigDirectory);
internal static class OpenRgbSetup
{
    internal const string DownloadUrl="https://codeberg.org/OpenRGB/OpenRGB/releases/download/release_1.1/OpenRGB_1.1_Windows_64_8e16de2.zip";
    internal const string Sha256="63291726c6fc84eb479ef2ee752e97e1187f6ce9c49fd27eee40ded588a8172a";
    internal const int MaximumDownload=32*1024*1024;
    const long MaximumExpanded=256L*1024*1024;
    const string Root="OpenRGB Windows 64-bit/";
    const string IndexKey=@"SOFTWARE\RedBladePlusStudio\ManagedOpenRGB";
    const string Prefix="openrgb-1.1-";

    internal static async Task<ManagedOpenRgb> EnsureInstalledAsync(string requestingSid,IProgress<string>? progress,CancellationToken ct)
    {
        var sid=new SecurityIdentifier(requestingSid);string sidHash=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sid.Value)))[..20];
        using var index=Registry.LocalMachine.CreateSubKey(IndexKey,true)??throw new IOException("Cannot open managed OpenRGB index.");
        string? id=index.GetValue(sidHash) as string;
        if(id!=null && ValidId(id) && VerifyInstallation(id,sid.Value))return Paths(id);
        progress?.Report("Downloading verified OpenRGB 1.1…");
        using var handler=new HttpClientHandler{AllowAutoRedirect=false};using var http=new HttpClient(handler){Timeout=TimeSpan.FromMinutes(5)};
        byte[] archive=await DownloadAsync(http,new Uri(DownloadUrl),Sha256,ct);
        ct.ThrowIfCancellationRequested();id=Prefix+Guid.NewGuid().ToString("N");
        using(var staging=new ProtectedStaging(id,sid.Value))
        {
            string payload=Path.Combine(staging.Path,"payload"),config=Path.Combine(staging.Path,"config");Directory.CreateDirectory(payload);Directory.CreateDirectory(config);
            File.WriteAllBytes(Path.Combine(staging.Path,"source.zip"),archive);ExtractVerified(archive,Sha256,payload);
            WriteConfig(config);
        }
        if(!VerifyInstallation(id,sid.Value))throw new IOException("Managed OpenRGB installation verification failed.");
        index.SetValue(sidHash,id,RegistryValueKind.String);return Paths(id);
    }
    static bool ValidId(string id)=>id.StartsWith(Prefix,StringComparison.Ordinal)&&Guid.TryParseExact(id[Prefix.Length..],"N",out _);
    static ManagedOpenRgb Paths(string id){string root=ProtectedStaging.JobPath(id);return new(Path.Combine(root,"payload","OpenRGB.exe"),Path.Combine(root,"config"));}
    static void WriteConfig(string directory)
    {
        File.WriteAllText(Path.Combine(directory,"OpenRGB.json"),ConfigJson());
    }
    internal static string ConfigJson()
    {
        var config=new Dictionary<string,object>{["QMKVialRGBDevices"]=new{devices=new[]{new{name="Designed by GG Red Blade FS (VialRGB)",usb_vid="0x320F",usb_pid="0x5136"}}},["Server"]=new{all_controllers=false,default_host="127.0.0.1",default_port=6742,legacy_workaround=false}};
        return JsonSerializer.Serialize(config);
    }
    static bool Secure(FileSystemInfo item,string readerSid)
    {
        if((item.Attributes&FileAttributes.ReparsePoint)!=0)return false;
        FileSystemSecurity acl=item is DirectoryInfo d?d.GetAccessControl(AccessControlSections.Owner|AccessControlSections.Access):((FileInfo)item).GetAccessControl(AccessControlSections.Owner|AccessControlSections.Access);
        string? owner=(acl.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier)?.Value;
        if(owner!="S-1-5-32-544"&&owner!="S-1-5-18")return false;
        const FileSystemRights write=FileSystemRights.WriteData|FileSystemRights.AppendData|FileSystemRights.WriteAttributes|FileSystemRights.WriteExtendedAttributes|FileSystemRights.Delete|FileSystemRights.DeleteSubdirectoriesAndFiles|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership;
        foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))
        {
            string principal=((SecurityIdentifier)rule.IdentityReference).Value;
            if(rule.AccessControlType==AccessControlType.Allow && (rule.FileSystemRights&write)!=0 && principal!="S-1-5-32-544"&&principal!="S-1-5-18")return false;
            if(principal==readerSid && rule.AccessControlType==AccessControlType.Allow && (rule.FileSystemRights&write)!=0)return false;
        }
        return true;
    }
    static bool VerifyInstallation(string id,string sid)
    {
        try
        {
            string root=ProtectedStaging.JobPath(id);var stack=new Stack<DirectoryInfo>();stack.Push(new DirectoryInfo(root));
            while(stack.Count>0){var dir=stack.Pop();if(!Secure(dir,sid))return false;foreach(var child in dir.EnumerateFileSystemInfos()){if(!Secure(child,sid))return false;if(child is DirectoryInfo sub)stack.Push(sub);}}
            string source=Path.Combine(root,"source.zip"),payload=Path.Combine(root,"payload"),config=Path.Combine(root,"config");
            using var stream=File.OpenRead(source);var files=ValidateArchive(stream,Sha256);stream.Position=0;using var zip=new ZipArchive(stream,ZipArchiveMode.Read,true);
            if(Directory.EnumerateFiles(payload,"*",SearchOption.AllDirectories).Count()!=files.Count)return false;
            foreach(var entry in zip.Entries){if(entry.FullName.EndsWith('/'))continue;string relative=entry.FullName[Root.Length..].Replace('/',Path.DirectorySeparatorChar);using var expected=entry.Open();using var actual=File.OpenRead(Path.Combine(payload,relative));if(!CryptographicOperations.FixedTimeEquals(SHA256.HashData(expected),SHA256.HashData(actual)))return false;}
            WriteConfig(config);return true;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or InvalidDataException or System.Security.SecurityException or ArgumentException){return false;}
    }

    internal static async Task<byte[]> DownloadAsync(HttpClient client,Uri uri,string expectedHash,CancellationToken ct)
    {
        if(uri.Scheme!=Uri.UriSchemeHttps || !uri.Host.Equals("codeberg.org",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Untrusted OpenRGB download host.");
        using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct);
        response.EnsureSuccessStatusCode();
        if(response.RequestMessage?.RequestUri is not Uri actual || actual.Scheme!=Uri.UriSchemeHttps || !actual.Host.Equals("codeberg.org",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Untrusted OpenRGB download redirect.");
        if(response.Content.Headers.ContentLength is long length && (length<=0||length>MaximumDownload))throw new InvalidDataException("Invalid OpenRGB download size.");
        using var source=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();byte[] buffer=new byte[81920];int n;
        while((n=await source.ReadAsync(buffer,ct))!=0){if(output.Length+n>MaximumDownload)throw new InvalidDataException("OpenRGB download exceeds the limit.");await output.WriteAsync(buffer.AsMemory(0,n),ct);}
        var bytes=output.ToArray();using var archive=new MemoryStream(bytes,false);ValidateArchive(archive,expectedHash);return bytes;
    }

    // Destination must be a newly created directory protected from untrusted writes.
    internal static void ExtractVerified(byte[] bytes,string expectedHash,string destination)
    {
        using var input=new MemoryStream(bytes,false);ValidateArchive(input,expectedHash);
        if(!Directory.Exists(destination)||Directory.EnumerateFileSystemEntries(destination).Any()||(File.GetAttributes(destination)&FileAttributes.ReparsePoint)!=0)throw new IOException("OpenRGB extraction requires an empty protected directory.");
        input.Position=0;using var zip=new ZipArchive(input,ZipArchiveMode.Read,true);
        foreach(var entry in zip.Entries)
        {
            string relative=entry.FullName[Root.Length..].TrimEnd('/');if(relative.Length==0)continue;
            string path=Path.Combine(destination,relative.Replace('/',Path.DirectorySeparatorChar));
            if(entry.FullName.EndsWith('/')){Directory.CreateDirectory(path);continue;}
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var source=entry.Open();using var target=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);source.CopyTo(target);
        }
    }

    // Only validates the bytes. Extraction and persistent installation are intentionally
    // separate: no caller should execute files until both and their ACLs are verified.
    internal static IReadOnlyList<string> ValidateArchive(Stream input,string expectedHash)
    {
        if(!input.CanSeek || input.Length is <=0 or >MaximumDownload)throw new InvalidDataException("Invalid OpenRGB archive size.");
        input.Position=0;
        if(!Convert.ToHexString(SHA256.HashData(input)).Equals(expectedHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("OpenRGB archive SHA-256 mismatch.");
        input.Position=0;using var zip=new ZipArchive(input,ZipArchiveMode.Read,true);
        if(zip.Entries.Count is 0 or >4096)throw new InvalidDataException("Invalid OpenRGB archive entries.");
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var filePaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var files=new List<string>();long total=0;bool exe=false;
        foreach(var entry in zip.Entries)
        {
            string name=entry.FullName;bool directory=name.EndsWith('/');
            if(!name.StartsWith(Root,StringComparison.Ordinal)||name.Contains('\\')||name.StartsWith('/')||name.Contains(':'))throw new InvalidDataException("Unsafe OpenRGB archive path.");
            string relative=name[Root.Length..].TrimEnd('/');
            if(relative.Length==0){int rootKind=(entry.ExternalAttributes>>16)&0xf000;if(name!=Root||!paths.Add("")||entry.Length!=0||(rootKind!=0&&rootKind!=0x4000)||(entry.ExternalAttributes&0x400)!=0)throw new InvalidDataException("Invalid archive root.");continue;}
            foreach(var segment in relative.Split('/'))
            {
                if(segment.Length==0||segment is "." or ".."||segment.EndsWith(' ')||segment.EndsWith('.')||segment.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new InvalidDataException("Unsafe OpenRGB archive path.");
                string stem=segment.Split('.')[0];
                if(stem.Equals("CON",StringComparison.OrdinalIgnoreCase)||stem.Equals("PRN",StringComparison.OrdinalIgnoreCase)||stem.Equals("AUX",StringComparison.OrdinalIgnoreCase)||stem.Equals("NUL",StringComparison.OrdinalIgnoreCase)||(stem.Length==4&&(stem.StartsWith("COM",StringComparison.OrdinalIgnoreCase)||stem.StartsWith("LPT",StringComparison.OrdinalIgnoreCase))&&stem[3] is >= '1' and <= '9'))throw new InvalidDataException("Reserved archive path.");
            }
            if(!paths.Add(relative))throw new InvalidDataException("Duplicate OpenRGB archive path.");
            int kind=(entry.ExternalAttributes>>16)&0xf000;
            if(kind!=0 && kind!=(directory?0x4000:0x8000))throw new InvalidDataException("OpenRGB archive contains a link or special file.");
            if((entry.ExternalAttributes&0x400)!=0)throw new InvalidDataException("OpenRGB archive contains a reparse point.");
            if(directory){if(entry.Length!=0)throw new InvalidDataException("Invalid directory entry.");continue;}
            total=checked(total+entry.Length);if(total>MaximumExpanded)throw new InvalidDataException("OpenRGB archive expands beyond the limit.");
            using var data=entry.Open();var buffer=new byte[81920];long readTotal=0;int read;
            while((read=data.Read(buffer,0,buffer.Length))!=0){readTotal+=read;if(readTotal>entry.Length)throw new InvalidDataException("Invalid expanded entry length.");}
            if(readTotal!=entry.Length)throw new InvalidDataException("Truncated archive entry.");
            if(relative=="OpenRGB.exe")exe=true;files.Add(relative);filePaths.Add(relative);
        }
        foreach(string path in paths){int slash=path.IndexOf('/');while(slash>=0){if(filePaths.Contains(path[..slash]))throw new InvalidDataException("Archive file is used as a directory.");slash=path.IndexOf('/',slash+1);}}
        if(!exe)throw new InvalidDataException("OpenRGB.exe is missing from archive root.");
        return files;
    }
}
