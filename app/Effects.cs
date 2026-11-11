// SPDX-License-Identifier: GPL-2.0-or-later
using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
namespace RedBladePlus.Studio;

internal sealed record EffectSettings([property: JsonRequired] string Preset, [property: JsonRequired] string Color1, [property: JsonRequired] string Color2, [property: JsonRequired] int Speed, [property: JsonRequired] int Brightness)
{
    internal void Validate()
    {
        if (Preset is not ("solid" or "breathing" or "wave" or "gradient" or "sweep" or "rain")) throw new ArgumentException("Unknown effect preset.");
        if (Speed is < 1 or > 100 || Brightness is < 0 or > 255) throw new ArgumentException("Effect speed or brightness out of range.");
        if (!Regex.IsMatch(Color1 ?? "", "^#[0-9a-fA-F]{6}$") || !Regex.IsMatch(Color2 ?? "", "^#[0-9a-fA-F]{6}$")) throw new ArgumentException("Invalid effect color.");
    }
}
internal static class EffectRenderer
{
    internal static byte[] Render(EffectSettings settings, long timeMs, IReadOnlyList<PhysicalKey> layout)
    {
        settings.Validate();
        if (layout.Count != 109 || layout.Select(k => k.Led).Distinct().Count()!=109 || layout.Any(k=>k.Led is <0 or >108)) throw new InvalidDataException("Keyboard geometry must map all 109 LEDs exactly once.");
        var a = Parse(settings.Color1); var b = Parse(settings.Color2); var result = new byte[327];
        double minX=layout.Min(k=>k.X), maxX=layout.Max(k=>k.X), minY=layout.Min(k=>k.Y), maxY=layout.Max(k=>k.Y);
        double phase = timeMs / (11000.0 - settings.Speed * 100.0);
        foreach (var k in layout)
        {
            double x=(k.X-minX)/Math.Max(1,maxX-minX), y=(k.Y-minY)/Math.Max(1,maxY-minY);
            double mix=0, light=1;
            switch(settings.Preset)
            {
                case "breathing": light=(1-Math.Cos(2*Math.PI*phase))/2; break;
                case "gradient": mix=x; break;
                case "wave": mix=(1+Math.Sin(2*Math.PI*(x-phase)))/2; break;
                case "sweep": light=Math.Pow(Math.Max(0,Math.Cos(Math.PI*(x-((phase%1)*1.6-.3)))), 8); break;
                case "rain": double drop=((phase*.65 + ((k.Column*37%101)/101.0))%1+1)%1; light=Math.Exp(-Math.Pow((y-drop)*9,2)); mix=(k.Column*17%101)/100.0; break;
            }
            for(int c=0;c<3;c++) result[k.Led*3+c]=(byte)Math.Clamp((int)Math.Round((a[c]*(1-mix)+b[c]*mix)*light*settings.Brightness/255.0),0,255);
        }
        return result;
    }
    static byte[] Parse(string s)=>new[]{Convert.ToByte(s.Substring(1,2),16),Convert.ToByte(s.Substring(3,2),16),Convert.ToByte(s.Substring(5,2),16)};
}

// OpenRGB SDK protocol 0–3: https://gitlab.com/CalcProgrammer1/OpenRGB/-/blob/master/Documentation/OpenRGBSDK.md
internal sealed class OpenRgbSdk : IDisposable
{
    TcpClient client = new(); NetworkStream stream = null!; int version; const int MaxPacket=1024*1024;
    public void Dispose()=>client.Dispose();
    internal async Task ConnectAsync(int port, CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(2000);
        await client.ConnectAsync(IPAddress.Loopback,port,timeout.Token); client.NoDelay=true; stream=client.GetStream();
        await SendAsync(0,40,U32(3),timeout.Token);
        try
        {
            var response=await ReadAsync(timeout.Token); if(response.Id!=40 || response.Data.Length!=4) throw new InvalidDataException("Invalid OpenRGB protocol negotiation.");
            version=(int)Math.Min(3,BinaryPrimitives.ReadUInt32LittleEndian(response.Data));
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested)
        {
            // Protocol 0 servers never answer the version request. Reconnect so no partial packet remains.
            client.Dispose(); client=new TcpClient(); using var retry=CancellationTokenSource.CreateLinkedTokenSource(ct); retry.CancelAfter(2000);
            await client.ConnectAsync(IPAddress.Loopback,port,retry.Token); client.NoDelay=true; stream=client.GetStream(); version=0;
        }
        await SendAsync(0,50,Encoding.UTF8.GetBytes("RedBlade+ Studio\0"),ct);
    }
    internal async Task<uint> FindKeyboardAsync(CancellationToken ct)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            while(true)
            {
                bool changed=false; void Changed()=>changed=true;
                await SendAsync(0,0,Array.Empty<byte>(),deadline.Token);var countPacket=await ReadExpectedAsync(0,0,deadline.Token,Changed);
                if(countPacket.Length!=4)throw new InvalidDataException("Invalid OpenRGB controller count.");
                uint count=BinaryPrimitives.ReadUInt32LittleEndian(countPacket);if(count>256)throw new InvalidDataException("Too many OpenRGB controllers.");
                if(changed)continue;
                var matches=new List<uint>();
                for(uint i=0;i<count;i++)
                {
                    await SendAsync(i,1,version==0?Array.Empty<byte>():U32((uint)version),deadline.Token);
                    var data=await ReadExpectedAsync(i,1,deadline.Token,Changed);
                    if(changed)break;
                    var c=ParseController(data,version);
                    string identity=(c.Name+" "+c.Description).ToLowerInvariant(),location=c.Location.ToLowerInvariant();
                    if((identity.Contains("redblade")||identity.Contains("vialrgb")||identity.Contains("vial rgb"))&&location.Contains("320f")&&location.Contains("5136"))
                    {
                        if(c.Leds!=109||c.Colors!=109)throw new InvalidDataException("The identified RedBlade controller does not expose exactly 109 LEDs and colors.");
                        matches.Add(i);
                    }
                }
                if(changed)continue;
                if(matches.Count>1)throw new InvalidOperationException("Multiple matching RedBlade controllers were found; effects were not started.");
                if(matches.Count==1)return matches[0];
                await Task.Delay(250,deadline.Token);
            }
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested&&deadline.IsCancellationRequested)
        {throw new TimeoutException("The RedBlade controller was not found in OpenRGB within 10 seconds. Check the keyboard and OpenRGB device detection.");}
    }
    internal async Task DirectAsync(uint device,CancellationToken ct)=>await SendAsync(device,1100,Array.Empty<byte>(),ct);
    internal async Task FrameAsync(uint device,byte[] rgb,CancellationToken ct)
    {
        if(rgb.Length!=327) throw new ArgumentException("Expected 109 RGB colors.");
        byte[] data=new byte[6+109*4]; BinaryPrimitives.WriteUInt32LittleEndian(data,(uint)data.Length); BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4),109);
        for(int i=0;i<109;i++) Array.Copy(rgb,i*3,data,6+i*4,3);
        await SendAsync(device,1050,data,ct);
    }
    internal async Task WatchAsync(CancellationToken ct)
    {
        while(true) { var p=await ReadAsync(ct); if(p.Id==100) throw new IOException("OpenRGB device list changed; streaming stopped."); }
    }
    async Task<byte[]> ReadExpectedAsync(uint device,uint id,CancellationToken ct,Action? deviceListChanged=null)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(2000);
        while(true) { var p=await ReadAsync(timeout.Token); if(p.Id==100) { if(deviceListChanged==null)throw new IOException("OpenRGB device list changed."); deviceListChanged(); continue; } if(p.Id==id && p.Device==device) return p.Data; throw new InvalidDataException("Unexpected OpenRGB packet."); }
    }
    async Task SendAsync(uint device,uint id,byte[] data,CancellationToken ct)
    {
        var packet=new byte[16+data.Length]; Encoding.ASCII.GetBytes("ORGB").CopyTo(packet,0); BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4),device); BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8),id); BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12),(uint)data.Length); data.CopyTo(packet,16);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(2000); await stream.WriteAsync(packet,timeout.Token);
    }
    async Task<(uint Device,uint Id,byte[] Data)> ReadAsync(CancellationToken ct)
    {
        byte[] h=new byte[16]; await stream.ReadExactlyAsync(h,ct); if(!h.AsSpan(0,4).SequenceEqual("ORGB"u8)) throw new InvalidDataException("Invalid OpenRGB header.");
        uint len=BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(12)); if(len>MaxPacket) throw new InvalidDataException("OpenRGB packet too large."); byte[] data=new byte[len]; await stream.ReadExactlyAsync(data,ct);
        return (BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(4)),BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(8)),data);
    }
    static byte[] U32(uint value) { byte[] b=new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b,value); return b; }
    internal sealed record Controller(string Name,string Description,string Location,int Leds,int Colors);
    internal static Controller ParseController(byte[] data,int version)
    {
        var r=new PacketReader(data); if(r.U32()!=data.Length) throw new InvalidDataException("Invalid controller data size."); r.Skip(4); string name=r.Str(); if(version>=1) r.Str(); string description=r.Str(); r.Str(); r.Str(); string location=r.Str();
        int modes=r.U16(); r.Skip(4); for(int i=0;i<modes;i++) { r.Str(); r.Skip(version>=3?48:36); int colors=r.U16(); r.Skip(colors*4); }
        int zones=r.U16(); for(int i=0;i<zones;i++) { r.Str(); r.Skip(16); int matrix=r.U16(); r.Skip(matrix); }
        int leds=r.U16(); for(int i=0;i<leds;i++) { r.Str(); r.Skip(4); } int colorsCount=r.U16(); r.Skip(colorsCount*4); if(!r.AtEnd) throw new InvalidDataException("Trailing controller data.");
        return new(name,description,location,leds,colorsCount);
    }
    sealed class PacketReader(byte[] data)
    {
        int p; internal bool AtEnd=>p==data.Length;
        internal void Skip(int n) { if(n<0 || n>data.Length-p) throw new InvalidDataException("Truncated OpenRGB controller data."); p+=n; }
        internal ushort U16() { int start=p; Skip(2); return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(start,2)); }
        internal uint U32() { int start=p; Skip(4); return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(start,4)); }
        internal string Str() { int n=U16(),start=p; Skip(n); if(n==0 || data[p-1]!=0) throw new InvalidDataException("Invalid OpenRGB string."); return Encoding.UTF8.GetString(data,start,n-1); }
    }
}
internal static class OpenRgbEffects
{
    internal static async Task RunAsync(int port,Func<EffectSettings> settingsProvider,IProgress<string>? progress,CancellationToken ct)
    {
        if(port is <1 or >65535) throw new ArgumentOutOfRangeException(nameof(port));
        var layout=KeyboardLayout.Load(); settingsProvider().Validate(); using var sdk=new OpenRgbSdk(); await sdk.ConnectAsync(port,ct); uint device=await sdk.FindKeyboardAsync(ct); await sdk.DirectAsync(device,ct);
        using var watcherCts=new CancellationTokenSource(); var watch=sdk.WatchAsync(watcherCts.Token); var clock=Stopwatch.StartNew(); bool safe=true;
        try
        {
            progress?.Report("OpenRGB connected");
            while(true)
            {
                ct.ThrowIfCancellationRequested(); if(watch.IsCompleted) { safe=false; await watch; throw new IOException("OpenRGB disconnected."); }
                await sdk.FrameAsync(device,EffectRenderer.Render(settingsProvider(),clock.ElapsedMilliseconds,layout),ct);
                var delay=Task.Delay(34,ct); if(await Task.WhenAny(delay,watch)==watch) { safe=false; await watch; throw new IOException("OpenRGB disconnected."); } await delay;
            }
        }
        finally
        {
            if(safe && !watch.IsCompleted) { try { using var timeout=new CancellationTokenSource(500); await sdk.FrameAsync(device,new byte[327],timeout.Token); } catch(Exception e) when(e is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { } }
            watcherCts.Cancel(); try { await watch; } catch(Exception e) when(e is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
        }
    }
}



