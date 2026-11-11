// SPDX-License-Identifier: GPL-2.0-or-later
using System.Runtime.InteropServices;
using System.Text;
namespace RedBladePlus.Studio;

internal readonly record struct KeyActivity(int Led, long TimestampMs);
internal static class KeyboardScanCodes
{
    // Set-1 make codes describe physical positions, independent of the active Windows layout.
    static readonly int[] Normal=Build();
    static int[] Build()
    {
        var a=Enumerable.Repeat(-1,128).ToArray();
        void Put(int code, int led)=>a[code]=led;
        Put(0x01,0); for(int i=0;i<10;i++)Put(0x3b+i,1+i);Put(0x57,11);Put(0x58,12);Put(0x46,14);
        Put(0x29,20);for(int i=0;i<10;i++)Put(0x02+i,21+i);Put(0x0c,31);Put(0x0d,32);Put(0x0e,33);
        Put(0x0f,41);for(int i=0;i<10;i++)Put(0x10+i,42+i);Put(0x1a,52);Put(0x1b,53);Put(0x2b,73);Put(0x1c,74);
        Put(0x3a,61);for(int i=0;i<9;i++)Put(0x1e+i,62+i);Put(0x27,71);Put(0x28,72);
        Put(0x2a,78);Put(0x56,79);for(int i=0;i<7;i++)Put(0x2c+i,80+i);Put(0x33,87);Put(0x34,88);Put(0x35,89);Put(0x36,90);
        Put(0x1d,96);Put(0x38,98);Put(0x39,99);Put(0x45,37);Put(0x37,39);Put(0x4a,40);Put(0x4e,60);
        Put(0x47,57);Put(0x48,58);Put(0x49,59);Put(0x4b,75);Put(0x4c,76);Put(0x4d,77);Put(0x4f,92);Put(0x50,93);Put(0x51,94);Put(0x52,107);Put(0x53,108);
        return a;
    }
    internal static int ToLed(int code, bool e0, bool e1)
    {
        if(e1)return code==0x45?15:-1;
        if(e0)return code switch {0x37=>13,0x52=>34,0x47=>35,0x49=>36,0x35=>38,0x53=>54,0x4f=>55,0x51=>56,0x48=>91,0x1c=>95,0x5b=>97,0x38=>100,0x5c=>101,0x1d=>103,0x4b=>104,0x50=>105,0x4d=>106,0x21=>16,0x20=>17,0x2e=>18,0x30=>19,_=>-1};
        return code>=0 && code<Normal.Length ? Normal[code] : -1;
    }
}

internal sealed class KeyboardActivity : IDisposable
{
    readonly Func<long> now; readonly object gate=new(); readonly KeyActivity[] recent=new KeyActivity[32]; int count;
    readonly List<KeyActivity> snapshot=new(32); Thread? thread; InputWindow? window; Exception? startupError; volatile bool stopping; readonly ManualResetEventSlim ready=new(false);
    internal KeyboardActivity(Func<long> now) => this.now=now;
    internal IReadOnlyList<KeyActivity> Events { get { lock(gate) { snapshot.Clear(); long t=now(); for(int i=0;i<count;i++) if(t-recent[i].TimestampMs>=0 && t-recent[i].TimestampMs<8000) snapshot.Add(recent[i]); return snapshot; } } }
    internal void Add(int led,long timestamp) { if(led is <0 or >108)return; lock(gate) { if(count==recent.Length){Array.Copy(recent,1,recent,0,--count);} recent[count++]=new(led,timestamp); } }
    internal void SetEnabled(bool enabled)
    {
        if(!enabled) { Stop(); lock(gate){count=0;snapshot.Clear();} return; }
        if(thread!=null)return;
        ready.Reset(); startupError=null; stopping=false; thread=new Thread(ThreadMain){IsBackground=true,Name="RedBlade raw input"}; thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if(!ready.Wait(2000)){Stop();throw new TimeoutException("Raw Input startup timed out.");}
        if(startupError!=null){Stop();throw new InvalidOperationException("Unable to start keyboard input.",startupError);}
    }
    void ThreadMain()
    {
        try { using var w=new InputWindow(this); window=w; ready.Set(); if(!stopping) Application.Run(); }
        catch(Exception e){startupError=e;ready.Set();}
        finally{window=null;}
    }
    void Stop() { var t=thread;if(t==null)return; stopping=true; var w=window; if(w!=null) PostMessage(w.Handle,0x0010,IntPtr.Zero,IntPtr.Zero); if(!t.Join(2000)) throw new TimeoutException("Raw Input thread did not stop."); thread=null; }
    public void Dispose(){Stop();ready.Dispose();}
    sealed class InputWindow : NativeWindow, IDisposable
    {
        readonly KeyboardActivity owner; readonly IntPtr buffer=Marshal.AllocHGlobal(64);
        internal InputWindow(KeyboardActivity owner)
        {
            this.owner=owner; CreateHandle(new CreateParams { Parent=new IntPtr(-3), Caption="RedBlade input" });
            var devices=new[]{new RawDevice { UsagePage=1,Usage=6,Flags=0x100,Target=Handle }};
            if(!RegisterRawInputDevices(devices,1,(uint)Marshal.SizeOf<RawDevice>())) {DestroyHandle();Marshal.FreeHGlobal(buffer);throw new System.ComponentModel.Win32Exception();}
        }
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==0x00ff) Read(m.LParam);
            if(m.Msg==0x0010){Application.ExitThread();return;}
            base.WndProc(ref m);
        }
        void Read(IntPtr handle)
        {
            uint size=64; uint read=GetRawInputData(handle,0x10000003,buffer,ref size,(uint)Marshal.SizeOf<RawHeader>());
            if(read==uint.MaxValue || read<(uint)Marshal.SizeOf<RawInput>())return; var input=Marshal.PtrToStructure<RawInput>(buffer);
            if(input.Header.Type!=1 || (input.Keyboard.Flags&1)!=0) return;
            uint length=0; if(GetRawInputDeviceInfo(input.Header.Device,0x20000007,null,ref length)==uint.MaxValue || length==0 || length>4096)return;
            var name=new StringBuilder((int)length); if(GetRawInputDeviceInfo(input.Header.Device,0x20000007,name,ref length)==uint.MaxValue)return;
            string path=name.ToString(); if(!System.Text.RegularExpressions.Regex.IsMatch(path,@"(?i)^\\\\\?\\hid#vid_320f&pid_5136(?:&|#)"))return;
            int led=KeyboardScanCodes.ToLed(input.Keyboard.MakeCode,(input.Keyboard.Flags&2)!=0,(input.Keyboard.Flags&4)!=0); owner.Add(led,owner.now());
        }
        public void Dispose(){var devices=new[]{new RawDevice{UsagePage=1,Usage=6,Flags=1,Target=IntPtr.Zero}};RegisterRawInputDevices(devices,1,(uint)Marshal.SizeOf<RawDevice>());DestroyHandle();Marshal.FreeHGlobal(buffer);}
    }
    [StructLayout(LayoutKind.Sequential)] struct RawDevice { public ushort UsagePage,Usage; public uint Flags; public IntPtr Target; }
    [StructLayout(LayoutKind.Sequential)] struct RawHeader { public uint Type,Size; public IntPtr Device,WParam; }
    [StructLayout(LayoutKind.Sequential)] struct RawKeyboard { public ushort MakeCode,Flags,Reserved,VKey; public uint Message,Extra; }
    [StructLayout(LayoutKind.Sequential)] struct RawInput { public RawHeader Header; public RawKeyboard Keyboard; }
    [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterRawInputDevices(RawDevice[] devices,uint count,uint size);
    [DllImport("user32.dll",SetLastError=true)] static extern uint GetRawInputData(IntPtr input,uint command,IntPtr data,ref uint size,uint headerSize);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="GetRawInputDeviceInfoW",SetLastError=true)] static extern uint GetRawInputDeviceInfo(IntPtr device,uint command,StringBuilder? data,ref uint size);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
}
