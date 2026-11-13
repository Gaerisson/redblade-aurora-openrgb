// SPDX-License-Identifier: GPL-2.0-or-later
using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace RedBladePlus.Studio;

public readonly record struct Hsv(byte H, byte S, byte V)
{
    public static Hsv FromColor(Color color)
    {
        var maximum = Math.Max(color.R,Math.Max(color.G,color.B)); var minimum = Math.Min(color.R,Math.Min(color.G,color.B));
        return new((byte)Math.Round(color.GetHue()*255/360),(byte)(maximum==0?0:Math.Round((maximum-minimum)*255.0/maximum)),maximum);
    }
    public Color ToColor()
    {
        double h=H/255.0*6,s=S/255.0,v=V/255.0; int i=(int)Math.Floor(h); double f=h-i,p=v*(1-s),q=v*(1-f*s),t=v*(1-(1-f)*s);
        var (r,g,b)=(i%6) switch { 0=>(v,t,p),1=>(q,v,p),2=>(p,v,t),3=>(p,q,v),4=>(t,p,v),_=>(v,p,q) };
        return Color.FromArgb((int)Math.Round(r*255),(int)Math.Round(g*255),(int)Math.Round(b*255));
    }
}
public sealed class OnboardProfile
{
    [JsonRequired] public string Name { get; set; } = "Profile";
    public bool GeneratedName { get; set; }
    [JsonRequired] public ushort Modules { get; set; } = 1023;
    [JsonRequired] public byte Master { get; set; } = 255;
    [JsonRequired] public byte DefaultMode { get; set; }
    [JsonRequired] public byte TimeoutSeconds { get; set; } = 5;
    [JsonRequired] public byte TurboRate { get; set; } = 60;
    [JsonRequired] public byte RecorderSpeed { get; set; } = 2;
    [JsonRequired] public byte TrailFrames { get; set; } = 15;
    public byte SafetyTimeoutSeconds { get; set; }
    public byte CapsWordLayout { get; set; }
    public byte ReactiveStyle { get; set; }
    public byte ReactiveWidth { get; set; }
    public byte ReactiveRate { get; set; }
    public byte HighlightMode { get; set; }
    public byte HighlightDimPercent { get; set; }
    [JsonRequired] public Hsv[] Keys { get; set; } = Enumerable.Repeat(new Hsv(0,0,64),109).ToArray();
    [JsonRequired] public Hsv[] Effects { get; set; } = Defaults.EffectColors.ToArray();
}
public sealed class StudioConfig
{
    [JsonRequired] public int Schema { get; set; } = 1;
    [JsonRequired] public string Board { get; set; } = "RDBLFS01";
    [JsonRequired] public int Active { get; set; }
    [JsonRequired] public OnboardProfile[] Profiles { get; set; } = Enumerable.Range(1,4).Select(n=>new OnboardProfile{Name=$"Profile {n}",GeneratedName=true}).ToArray();
    public static StudioConfig Default() => new();
}
internal static class Defaults
{
    internal static readonly int[] TurboRates = Enumerable.Range(1,20).Select(n=>n*5).Concat(new[]{125,150,175,200,225,250}).ToArray();
    internal static readonly string[] Speeds = {"0.5×","0.75×","1×","1.25×","1.5×","1.75×","2×","4×","8×","Hyper"};
    internal static readonly string[] ModuleNames = {"Startup wave","Media fallback","Idle fallback","Reactive typing","Fn focus","Keyboard Turbo","Mouse Turbo","Recorder","Forward hold","Windows lock"};
    internal static readonly string[] ModulesFr = {"Vague au démarrage","Repli multimédia","Repli sans hôte","Frappe réactive","Focus Fn","Turbo clavier","Turbo souris","Enregistreur","Marche avant","Verrou Windows"};
    internal static readonly string[] EffectNames = {"Startup wave","Reactive peak","Accent / Red","Media fallback","Fn idle","Fn held","Keyboard Turbo","Forward hold","Locked Windows","Fn Game Mode","Left-click Turbo","Right-click Turbo","Caps / Num active","Scroll active","Recorder recording","Recorder one-shot","Recorder loop","Selection / error","Fn lighting hints","Fn game hints","Fn Turbo hints","Fn Host hint","Fn accent hint","Enabled option hints"};
    internal static readonly string[] EffectsFr = {"Vague au démarrage","Pic réactif","Accent / Rouge","Repli multimédia","Fn au repos","Fn maintenue","Turbo clavier","Marche avant","Windows verrouillées","Fn mode jeu","Turbo clic gauche","Turbo clic droit","Verr. Maj / Num","Arrêt défil.","Enregistrement","Lecture simple","Lecture en boucle","Sélection / erreur","Aides Fn éclairage","Aides Fn jeu","Aides Fn Turbo","Aide Fn hôte","Aide Fn accent","Options actives"};
    internal static readonly Hsv[] EffectColors = {new(0,255,255),new(0,255,255),new(0,255,255),new(0,0,64),new(170,255,24),new(128,255,96),new(0,255,128),new(85,255,96),new(0,255,128),new(200,255,80),new(32,255,128),new(200,255,128),new(85,255,128),new(32,255,128),new(0,255,128),new(85,255,128),new(128,255,128),new(32,255,128),new(0,0,80),new(85,255,80),new(0,255,96),new(170,255,80),new(0,255,80),new(85,255,64)};
}
internal static class ConfigCodec
{
    internal const int Length=1696, ProfileLength=416;
    internal static void Validate(StudioConfig config)
    {
        if(config.Schema!=1 || config.Board!="RDBLFS01" || config.Active is <0 or >3 || config.Profiles?.Length!=4) throw new InvalidDataException("Expected schema 1, Redblade FS and four profiles.");
        foreach(var p in config.Profiles)
            if(p is null || p.Name is null || p.Name.Length is <1 or >64 || p.Name.Any(char.IsControl) || p.Modules>4095 || p.DefaultMode>3 || p.TimeoutSeconds is <1 or >60 || !Defaults.TurboRates.Contains(p.TurboRate) || p.RecorderSpeed>9 || p.TrailFrames is <1 or >15 || (p.SafetyTimeoutSeconds!=0&&p.SafetyTimeoutSeconds is <5 or >120) || p.CapsWordLayout>1 || p.ReactiveStyle>4 || p.ReactiveRate>8 || p.HighlightMode>2 || p.HighlightDimPercent>100 || (p.ReactiveWidth!=0&&p.ReactiveWidth is <4 or >64) || p.Keys?.Length!=109 || p.Effects?.Length!=24)
                throw new InvalidDataException("Invalid profile fields or palette dimensions.");
    }
    internal static byte[] Encode(StudioConfig config)
    {
        Validate(config); var bytes=new byte[Length]; bytes[0]=(byte)config.Active;
        for(int n=0;n<4;n++)
        {
            int b=32+n*416; var p=config.Profiles[n]; Wire.Put16(bytes,b,p.Modules); bytes[b+2]=p.Master; bytes[b+3]=p.DefaultMode; bytes[b+4]=p.TimeoutSeconds; bytes[b+5]=p.TurboRate; bytes[b+6]=p.RecorderSpeed; bytes[b+7]=p.TrailFrames;bytes[b+8]=p.SafetyTimeoutSeconds;bytes[b+9]=p.CapsWordLayout;bytes[b+10]=p.ReactiveStyle;bytes[b+11]=p.ReactiveWidth;bytes[b+12]=p.ReactiveRate;bytes[b+13]=p.HighlightMode;bytes[b+14]=p.HighlightDimPercent;
            foreach(var (colors,offset) in new[]{(p.Keys,16),(p.Effects,343)}) for(int i=0;i<colors.Length;i++) { bytes[b+offset+i*3]=colors[i].H;bytes[b+offset+i*3+1]=colors[i].S;bytes[b+offset+i*3+2]=colors[i].V; }
        }
        return bytes;
    }
    internal static StudioConfig Decode(byte[] bytes)
    {
        if(bytes.Length!=Length || bytes.AsSpan(1,31).ContainsAnyExcept((byte)0)) throw new InvalidDataException("Invalid configuration length/reserved fields.");
        var config=StudioConfig.Default(); config.Active=bytes[0];
        for(int n=0;n<4;n++)
        {
            int b=32+n*416; if(bytes[b+15]!=0||bytes[b+415]!=0) throw new InvalidDataException("Nonzero reserved profile bytes.");
            var p=config.Profiles[n];p.Modules=Wire.U16(bytes,b);p.Master=bytes[b+2];p.DefaultMode=bytes[b+3];p.TimeoutSeconds=bytes[b+4];p.TurboRate=bytes[b+5];p.RecorderSpeed=bytes[b+6];p.TrailFrames=bytes[b+7];p.SafetyTimeoutSeconds=bytes[b+8];p.CapsWordLayout=bytes[b+9];p.ReactiveStyle=bytes[b+10];p.ReactiveWidth=bytes[b+11];p.ReactiveRate=bytes[b+12];p.HighlightMode=bytes[b+13];p.HighlightDimPercent=bytes[b+14];
            for(int i=0;i<109;i++) p.Keys[i]=new(bytes[b+16+i*3],bytes[b+17+i*3],bytes[b+18+i*3]);
            for(int i=0;i<24;i++) p.Effects[i]=new(bytes[b+343+i*3],bytes[b+344+i*3],bytes[b+345+i*3]);
        }
        Validate(config);return config;
    }
    internal static uint Crc32(ReadOnlySpan<byte> data) { uint crc=uint.MaxValue;foreach(byte b in data){crc^=b;for(int n=0;n<8;n++) crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}return ~crc; }
}
internal static class ProfileFile
{
    internal const int MaximumBytes=131072;
    static readonly JsonSerializerOptions Options=new(){WriteIndented=true,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,MaxDepth=12};
    internal static byte[] Save(StudioConfig config) { ConfigCodec.Validate(config);return JsonSerializer.SerializeToUtf8Bytes(config,Options); }
    internal static StudioConfig Load(byte[] bytes)
    {
        if(bytes.Length>MaximumBytes)throw new InvalidDataException("Profile file exceeds 128 KiB.");
        var config=JsonSerializer.Deserialize<StudioConfig>(bytes,Options)??throw new InvalidDataException("Empty profile file.");ConfigCodec.Validate(config);return config;
    }
    internal static StudioConfig LoadFile(string path)=>Load(BoundedFile.Read(path,MaximumBytes));
}
internal static class BoundedFile
{
    // Readers retain a stable file handle while the worker atomically replaces its result.
    internal static FileStream OpenSnapshot(string path)=>new(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
    internal static byte[] Read(string path,int maximum,bool snapshot=false)
    {
        using var stream=snapshot?OpenSnapshot(path):new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(stream.Length>maximum)throw new InvalidDataException("File exceeds the allowed size.");
        var bytes=new byte[checked((int)stream.Length)];stream.ReadExactly(bytes);return bytes;
    }
}
internal static class Wire
{
    internal static ushort U16(byte[] data,int offset)=>BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset,2));
    internal static uint U32(byte[] data,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset,4));
    internal static void Put16(byte[] data,int offset,int value)=>BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset,2),checked((ushort)value));
    internal static void Put32(byte[] data,int offset,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset,4),value);
    internal static void Sign(byte[] data)=>Encoding.ASCII.GetBytes("RDBLFS01").CopyTo(data,24);
    internal static void CheckSignature(byte[] data) { if(!data.AsSpan(24,8).SequenceEqual("RDBLFS01"u8))throw new InvalidDataException("Wrong Redblade identity."); }
    internal static byte[] Packet(byte operation) { var p=new byte[32];p[0]=0xfe;p[1]=0x90;p[2]=operation;return p; }
}
internal interface IHidTransport : IDisposable { Task<byte[]> ExchangeAsync(byte[] request,CancellationToken cancel); }
internal sealed record Capabilities(uint Generation,byte Selected,bool Valid,bool Busy);
internal sealed record ReadConfiguration(byte[] Bytes,Capabilities Caps);
internal sealed class ConfigProtocol(IHidTransport transport)
{
    internal bool SaveMayHaveBegun{get;private set;}
    async Task<byte[]> Send(byte[] packet,CancellationToken cancel,bool accepted=false)
    {
        var r=await transport.ExchangeAsync(packet,cancel);
        if(r.Length!=32||!r.AsSpan(0,3).SequenceEqual(packet.AsSpan(0,3)))throw new InvalidDataException("Mismatched or incomplete configuration response. Close competing HID clients.");
        if(r[3]!=0 && !(accepted&&r[3]==1)){var error=new InvalidDataException("Keyboard rejected operation: "+r[3]+" (9: release physical keys first).");error.Data["ConfigStatus"]=r[3];throw error;}return r;
    }
    internal async Task<Capabilities> Capabilities(CancellationToken cancel)
    {
        var r=await Send(Wire.Packet(0),cancel);Wire.CheckSignature(r);
        if(r[4]!=1||r[5]!=4||r[6]!=109||r[7]!=24||Wire.U16(r,8)!=1696||Wire.U16(r,10)!=416||r[16]>3||(r[17]&~3)!=0)throw new InvalidDataException("Unsupported configuration schema or dimensions.");
        return new(Wire.U32(r,12),r[16],(r[17]&1)!=0,(r[17]&2)!=0);
    }
    internal async Task<ReadConfiguration> Read(CancellationToken cancel)
    {
        var caps=await Capabilities(cancel);if(caps.Busy)throw new InvalidDataException("Keyboard storage is busy.");var bytes=new byte[1696];
        for(int offset=0;offset<bytes.Length;offset+=24){var p=Wire.Packet(1);Wire.Put16(p,4,offset);p[6]=(byte)Math.Min(24,bytes.Length-offset);var r=await Send(p,cancel);if(Wire.U16(r,4)!=offset||r[6]!=p[6])throw new InvalidDataException("Mismatched configuration chunk.");r.AsSpan(8,p[6]).CopyTo(bytes.AsSpan(offset));}
        ConfigCodec.Decode(bytes);var after=await Capabilities(cancel);if(after.Generation!=caps.Generation||after.Busy||after.Valid!=caps.Valid||bytes[0]!=after.Selected)throw new InvalidDataException("Configuration changed while being read. Read again.");return new(bytes,caps);
    }
    async Task<byte[]> Status(CancellationToken cancel){var r=await Send(Wire.Packet(3),cancel);Wire.CheckSignature(r);if(r[4]>4||Wire.U16(r,8)!=1696)throw new InvalidDataException("Invalid transaction status.");if(r[4]==4||r[5]!=0)throw new InvalidDataException("Storage transaction failed: "+r[5]);return r;}
    async Task WaitReady(CancellationToken cancel)
    {
        var until=DateTime.UtcNow.AddMinutes(2);while(DateTime.UtcNow<until){var s=await Status(cancel);if(s[4]==2&&Wire.U16(s,6)==0)return;if(s[4]!=1)throw new InvalidDataException("Unexpected save phase.");await Task.Delay(20,cancel);}throw new TimeoutException("Configuration erase timed out.");
    }
    internal async Task Save(StudioConfig config,uint expectedGeneration,Action<string>? progress,CancellationToken cancel)
    {
        SaveMayHaveBegun=false;
        var bytes=ConfigCodec.Encode(config);var caps=await Capabilities(cancel);if(caps.Busy||caps.Generation!=expectedGeneration)throw new InvalidDataException("Keyboard changed since the last Read. Read it again before saving.");
        await Send(Wire.Packet(8),cancel);var begin=Wire.Packet(2);Wire.Put32(begin,4,ConfigCodec.Crc32(bytes));Wire.Put32(begin,8,expectedGeneration);Wire.Put16(begin,12,1696);begin[14]=1;bool begun=false;
        try
        {
            begun=true;SaveMayHaveBegun=true;
            try{await Send(begin,cancel,true);}catch(InvalidDataException error) when(error.Data.Contains("ConfigStatus")){begun=false;SaveMayHaveBegun=false;throw;}
            await WaitReady(cancel);
            for(int offset=0;offset<bytes.Length;offset+=24){var p=Wire.Packet(4);Wire.Put16(p,4,offset);p[6]=(byte)Math.Min(24,bytes.Length-offset);bytes.AsSpan(offset,p[6]).CopyTo(p.AsSpan(8));var r=await Send(p,cancel);if(Wire.U16(r,4)!=offset||r[6]!=p[6]||Wire.U16(r,8)!=offset+p[6])throw new InvalidDataException("Mismatched save acknowledgement; transaction stopped.");progress?.Invoke($"Saving onboard profiles · {100*(offset+p[6])/1696}%");}
            await Send(Wire.Packet(5),cancel,true);var until=DateTime.UtcNow.AddMinutes(2);uint target=unchecked(expectedGeneration+1);bool committed=false;
            while(DateTime.UtcNow<until){var s=await Status(cancel);if(Wire.U32(s,10)==target&&s[15]==1){committed=true;break;}if(s[4]!=3)throw new InvalidDataException("Unexpected commit phase.");await Task.Delay(20,cancel);}
            if(!committed)throw new TimeoutException("Configuration commit did not complete.");
            var readback=await Read(cancel);if(!readback.Caps.Valid||readback.Caps.Generation!=target||!readback.Bytes.SequenceEqual(bytes))throw new InvalidDataException("Saved profiles did not pass complete readback.");SaveMayHaveBegun=false;progress?.Invoke("All four profiles saved and verified.");
        }
        catch { if(begun)try{using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(3));await Send(Wire.Packet(6),cleanup.Token);SaveMayHaveBegun=false;}catch{}throw; }
    }
    internal Task<byte[]> Temporary(byte mode,byte master,byte flags,CancellationToken cancel){var p=Wire.Packet(7);p[4]=mode;p[5]=master;p[6]=(byte)(flags&7);return Send(p,cancel);}
    internal Task<byte[]> Abort(CancellationToken cancel)=>Send(Wire.Packet(6),cancel);
}
internal static class Embedded
{
    internal static byte[] Read(string name){using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)??throw new InvalidDataException("Bundled resource missing: "+name);using var output=new MemoryStream();stream.CopyTo(output);return output.ToArray();}
}
internal sealed class PhysicalKey
{
    public int Led{get;set;} public int Row{get;set;} public int Column{get;set;} public float X{get;set;} public float Y{get;set;} public float W{get;set;}=1; public float H{get;set;}=1;public string Label{get;set;}="";public string AzertyLabel{get;set;}="";public string Kind{get;set;}="key";
}
internal static class KeyboardLayout
{
    internal static PhysicalKey[] Load()=>JsonSerializer.Deserialize<PhysicalKey[]>(Embedded.Read("layout.json"))??throw new InvalidDataException("Missing keyboard geometry.");
    internal static string Legend(PhysicalKey key,bool azerty)=>azerty&&!string.IsNullOrEmpty(key.AzertyLabel)?key.AzertyLabel:key.Label;
}
