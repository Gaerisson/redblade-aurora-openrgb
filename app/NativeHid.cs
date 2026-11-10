// SPDX-License-Identifier: GPL-2.0-or-later
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;
namespace RedBladePlus.Studio;

internal sealed record HidEndpoint(string Path,ushort Vid,ushort Pid,ushort UsagePage,ushort Usage,int InputLength,int OutputLength,string Manufacturer,string Product);
internal sealed class NativeHid : IHidTransport
{
    readonly FileStream stream; readonly SemaphoreSlim serial=new(1,1); bool disposed;
    NativeHid(HidEndpoint endpoint)
    {
        if(endpoint.InputLength!=33||endpoint.OutputLength!=33)throw new InvalidDataException("Unexpected Raw HID report lengths.");
        var handle=Native.CreateFile(endpoint.Path,0xc0000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero);
        if(handle.IsInvalid){handle.Dispose();throw new IOException("Cannot open keyboard. Close VIA and other keyboard tools.");}
        try{stream=new FileStream(handle,FileAccess.ReadWrite,33,true);}catch{handle.Dispose();throw;}
    }
    internal static NativeHid OpenKeyboard()
    {
        var endpoints=Enumerate(0x320f,0x5136).Where(e=>e.UsagePage==0xff60&&e.Usage==0x61).ToArray();
        if(endpoints.Length!=1)throw new IOException($"Expected one Redblade FS Raw HID interface; found {endpoints.Length}.");
        var endpoint=endpoints[0];if(endpoint.Manufacturer!="DesignedbyGG"||endpoint.Product!="Redblade FS")throw new InvalidDataException("Unexpected keyboard model; access refused.");return new(endpoint);
    }
    internal static int CountKeyboards()=>Enumerate(0x320f,0x5136).Count(e=>e.UsagePage==0xff60&&e.Usage==0x61&&e.Manufacturer=="DesignedbyGG"&&e.Product=="Redblade FS");
    internal static string[] UsbLocations(string vidPid)
    {
        var guid=new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");var list=Native.SetupDiGetClassDevs(ref guid,null,IntPtr.Zero,0x12);
        if(list==new IntPtr(-1))throw new IOException("Unable to enumerate USB locations.");var locations=new List<string>();
        try
        {
            for(uint index=0;;index++)
            {
                var data=new Native.InterfaceData{Size=Marshal.SizeOf<Native.InterfaceData>()};
                if(!Native.SetupDiEnumDeviceInterfaces(list,IntPtr.Zero,ref guid,index,ref data)){if(Marshal.GetLastWin32Error()==259)break;throw new IOException("USB enumeration failed.");}
                Native.SetupDiGetDeviceInterfaceDetail(list,ref data,IntPtr.Zero,0,out var size,IntPtr.Zero);if(size<8||size>65536)throw new IOException("Invalid USB interface size.");
                var detail=Marshal.AllocHGlobal((int)size);var device=Marshal.AllocHGlobal(Marshal.SizeOf<Native.DeviceInfoData>());
                try
                {
                    Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);Marshal.StructureToPtr(new Native.DeviceInfoData{Size=Marshal.SizeOf<Native.DeviceInfoData>()},device,false);
                    if(!Native.SetupDiGetDeviceInterfaceDetail(list,ref data,detail,size,out _,device))throw new IOException("Unable to inspect USB interface.");
                    string path=Marshal.PtrToStringUni(detail+4)??"";if(!path.Contains("#vid_"+vidPid+"#",StringComparison.OrdinalIgnoreCase))continue;
                    var info=Marshal.PtrToStructure<Native.DeviceInfoData>(device);var buffer=new byte[8192];
                    if(!Native.SetupDiGetDeviceRegistryProperty(list,ref info,0x23,out _,buffer,(uint)buffer.Length,out var required)||required<4)throw new IOException("USB physical location unavailable; flashing refused.");
                    string location=Encoding.Unicode.GetString(buffer,0,(int)required).Split('\0',StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??throw new IOException("USB physical location unavailable; flashing refused.");
                    locations.Add(location);
                }
                finally{Marshal.FreeHGlobal(device);Marshal.FreeHGlobal(detail);}
            }
        }
        finally{Native.SetupDiDestroyDeviceInfoList(list);}return locations.ToArray();
    }
    internal static int CountBootloaders()
    {
        // Count physical USB device interfaces, independently of their HID/libusb driver binding.
        var guid=new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");var list=Native.SetupDiGetClassDevs(ref guid,null,IntPtr.Zero,0x12);
        if(list==new IntPtr(-1))throw new IOException("Unable to enumerate USB bootloader interfaces.");var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            for(uint index=0;;index++)
            {
                var data=new Native.InterfaceData{Size=Marshal.SizeOf<Native.InterfaceData>()};
                if(!Native.SetupDiEnumDeviceInterfaces(list,IntPtr.Zero,ref guid,index,ref data)){if(Marshal.GetLastWin32Error()==259)break;throw new IOException("USB enumeration failed.");}
                Native.SetupDiGetDeviceInterfaceDetail(list,ref data,IntPtr.Zero,0,out var size,IntPtr.Zero);if(size<8||size>65536)throw new IOException("Invalid USB interface path size.");var detail=Marshal.AllocHGlobal((int)size);
                try
                {
                    Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);if(!Native.SetupDiGetDeviceInterfaceDetail(list,ref data,detail,size,out _,IntPtr.Zero))throw new IOException("Unable to inspect USB bootloader path.");
                    string path=Marshal.PtrToStringUni(detail+4)??"";if(path.Contains("#vid_0c45&pid_7040#",StringComparison.OrdinalIgnoreCase))paths.Add(path);
                }
                finally{Marshal.FreeHGlobal(detail);}
            }
        }
        finally{Native.SetupDiDestroyDeviceInfoList(list);}return paths.Count;
    }
    internal static List<HidEndpoint> Enumerate(ushort vid,ushort pid)
    {
        Native.HidD_GetHidGuid(out var guid);var list=Native.SetupDiGetClassDevs(ref guid,null,IntPtr.Zero,0x12);
        if(list==new IntPtr(-1))throw new IOException("Unable to enumerate HID interfaces.");
        var found=new List<HidEndpoint>();
        try
        {
            for(uint index=0;;index++)
            {
                var data=new Native.InterfaceData{Size=Marshal.SizeOf<Native.InterfaceData>()};
                if(!Native.SetupDiEnumDeviceInterfaces(list,IntPtr.Zero,ref guid,index,ref data)){if(Marshal.GetLastWin32Error()==259)break;throw new IOException("HID enumeration failed.");}
                Native.SetupDiGetDeviceInterfaceDetail(list,ref data,IntPtr.Zero,0,out var size,IntPtr.Zero);
                if(size<8||size>65536)throw new IOException("Invalid HID interface path size.");var detail=Marshal.AllocHGlobal((int)size);
                try
                {
                    Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);
                    if(!Native.SetupDiGetDeviceInterfaceDetail(list,ref data,detail,size,out _,IntPtr.Zero))throw new IOException("Unable to read HID interface path.");
                    string path=Marshal.PtrToStringUni(detail+4)??"";
                    using var handle=Native.CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero);if(handle.IsInvalid)continue;
                    var attributes=new Native.Attributes{Size=Marshal.SizeOf<Native.Attributes>()};
                    if(!Native.HidD_GetAttributes(handle,ref attributes)||attributes.Vendor!=vid||attributes.Product!=pid)continue;
                    if(!Native.HidD_GetPreparsedData(handle,out var preparsed))continue;
                    try
                    {
                        if(Native.HidP_GetCaps(preparsed,out var caps)!=0x110000)continue;
                        var manufacturer=new StringBuilder(256);var product=new StringBuilder(256);
                        Native.HidD_GetManufacturerString(handle,manufacturer,512);Native.HidD_GetProductString(handle,product,512);
                        found.Add(new(path,vid,pid,caps.UsagePage,caps.Usage,caps.InputReportByteLength,caps.OutputReportByteLength,manufacturer.ToString(),product.ToString()));
                    }
                    finally{Native.HidD_FreePreparsedData(preparsed);}
                }
                finally{Marshal.FreeHGlobal(detail);}
            }
        }
        finally{Native.SetupDiDestroyDeviceInfoList(list);}
        return found;
    }
    public async Task<byte[]> ExchangeAsync(byte[] request,CancellationToken cancel)
    {
        if(request.Length!=32)throw new ArgumentException("Exactly 32 payload bytes required.");
        await serial.WaitAsync(cancel);
        try
        {
            ObjectDisposedException.ThrowIf(disposed,this);using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel);timeout.CancelAfter(TimeSpan.FromSeconds(1));
            var output=new byte[33];request.CopyTo(output,1);var input=new byte[33];
            try
            {
                await stream.WriteAsync(output,timeout.Token);int read=await stream.ReadAsync(input,timeout.Token);
                if(read!=33||input[0]!=0)throw new IOException("Incomplete or unexpected HID report.");return input.AsSpan(1).ToArray();
            }
            catch(OperationCanceledException) when(!cancel.IsCancellationRequested){Dispose();throw new IOException("HID response timed out. The write may have reached the keyboard; it was not retried.");}
        }
        finally{serial.Release();}
    }
    public void Dispose(){if(disposed)return;disposed=true;stream.Dispose();}
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]internal struct InterfaceData{internal int Size;internal Guid ClassGuid;internal uint Flags;internal IntPtr Reserved;}
        [StructLayout(LayoutKind.Sequential)]internal struct DeviceInfoData{internal int Size;internal Guid ClassGuid;internal uint DevInst;internal IntPtr Reserved;}
        [StructLayout(LayoutKind.Sequential)]internal struct Attributes{internal int Size;internal ushort Vendor,Product,Version;}
        [StructLayout(LayoutKind.Sequential)]internal struct Caps
        {
            internal ushort Usage,UsagePage,InputReportByteLength,OutputReportByteLength,FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray,SizeConst=17)]internal ushort[] Reserved;
            internal ushort LinkNodes,InputButtons,InputValues,InputIndices,OutputButtons,OutputValues,OutputIndices,FeatureButtons,FeatureValues,FeatureIndices;
        }
        [DllImport("hid.dll")]internal static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("hid.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.U1)]internal static extern bool HidD_GetAttributes(SafeFileHandle handle,ref Attributes attributes);
        [DllImport("hid.dll")][return:MarshalAs(UnmanagedType.U1)]internal static extern bool HidD_GetPreparsedData(SafeFileHandle handle,out IntPtr data);
        [DllImport("hid.dll")][return:MarshalAs(UnmanagedType.U1)]internal static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")]internal static extern int HidP_GetCaps(IntPtr data,out Caps caps);
        [DllImport("hid.dll",CharSet=CharSet.Unicode,ExactSpelling=true)][return:MarshalAs(UnmanagedType.U1)]internal static extern bool HidD_GetManufacturerString(SafeFileHandle handle,StringBuilder buffer,int length);
        [DllImport("hid.dll",CharSet=CharSet.Unicode,ExactSpelling=true)][return:MarshalAs(UnmanagedType.U1)]internal static extern bool HidD_GetProductString(SafeFileHandle handle,StringBuilder buffer,int length);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]internal static extern IntPtr SetupDiGetClassDevs(ref Guid guid,string? enumerator,IntPtr window,uint flags);
        [DllImport("setupapi.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr list,IntPtr device,ref Guid guid,uint index,ref InterfaceData data);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr list,ref InterfaceData data,IntPtr detail,uint size,out uint required,IntPtr device);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool SetupDiGetDeviceRegistryProperty(IntPtr list,ref DeviceInfoData device,uint property,out uint type,byte[] buffer,uint size,out uint required);
        [DllImport("setupapi.dll")][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr list);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]internal static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    }
}

internal sealed record KeyboardIdentity(Version Version,bool Core,int ViaProtocol);
internal static class KeyboardProtocol
{
    internal static async Task<byte[]> Via(IHidTransport hid,byte[] payload,CancellationToken cancel,int echo=1)
    {
        var packet=new byte[32];payload.CopyTo(packet,0);var response=await hid.ExchangeAsync(packet,cancel);
        if(response.Length!=32||!response.AsSpan(0,echo).SequenceEqual(packet.AsSpan(0,echo)))throw new InvalidDataException("Unexpected VIA reply; close competing clients.");return response;
    }
    internal static async Task<KeyboardIdentity> Identify(IHidTransport hid,CancellationToken cancel)
    {
        var via=await Via(hid,new byte[]{1},cancel);int protocol=(via[1]<<8)|via[2];if(protocol is <9 or >13)throw new InvalidDataException("Unsupported VIA protocol.");
        var packet=new byte[32];packet[0]=0xfe;packet[1]=0x81;var info=await hid.ExchangeAsync(packet,cancel);
        if(info.Length==32&&info[0]==0xff)return new(new Version(0,0,0),false,protocol);
        if(info.Length!=32||info[0]!=0xfe||info[1]!=0x81||info[2]!=1)throw new InvalidDataException("Mismatched keyboard identity response. Close competing clients and read again.");
        Wire.CheckSignature(info);return new(new Version(info[3],info[4],info[5]),(info[19]&128)!=0,protocol);
    }
}
