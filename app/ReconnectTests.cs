// SPDX-License-Identifier: GPL-2.0-or-later
namespace RedBladePlus.Studio;
internal static class ReconnectTests
{
    sealed class Hid : IHidTransport
    {
        readonly bool fail; readonly byte version;readonly bool malformed; internal bool Disposed; internal int Requests;
        internal Hid(bool fail, byte version=10,bool malformed=false){this.fail=fail;this.version=version;this.malformed=malformed;}
        public Task<byte[]> ExchangeAsync(byte[] packet,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Requests++;
            if(fail)throw new IOException("synthetic HID timeout");
            var response=new byte[32];response[0]=packet[0];
            if(packet[0]==1){response[2]=13;return Task.FromResult(response);}
            if(packet[0]!=0xfe||packet[1]!=0x81)throw new Exception("Unexpected write in reconnect");
            if(malformed)return Task.FromResult(response);
            response[1]=0x81;response[2]=1;response[3]=0;response[4]=version;response[5]=0;"RDBLFS01"u8.CopyTo(response.AsSpan(24));return Task.FromResult(response);
        }
        public void Dispose()=>Disposed=true;
    }
    internal static async Task Run()
    {
        var first=new Hid(true);var second=new Hid(false);int attempts=0;
        var connected=await HardwareJobs.ReconnectVerifiedAsync(()=>++attempts==1?first:second,new Version(0,10,0),false,TimeSpan.FromSeconds(2),TimeSpan.Zero,CancellationToken.None);
        if(attempts!=2||!first.Disposed||second.Disposed||connected.Identity.Version!=new Version(0,10,0)||second.Requests!=2)throw new Exception("fresh reconnect after timeout failed");
        connected.Hid.Dispose();
        var stale=new Hid(false,malformed:true);var fresh=new Hid(false);attempts=0;
        connected=await HardwareJobs.ReconnectVerifiedAsync(()=>++attempts==1?stale:fresh,new Version(0,10,0),false,TimeSpan.FromSeconds(2),TimeSpan.Zero,CancellationToken.None);
        if(attempts!=2||!stale.Disposed||fresh.Disposed)throw new Exception("stale identity reply was not retried on a fresh handle");connected.Hid.Dispose();
        var mismatch=new Hid(false,9);attempts=0;
        try{await HardwareJobs.ReconnectVerifiedAsync(()=>{attempts++;return mismatch;},new Version(0,10,0),false,TimeSpan.FromSeconds(2),TimeSpan.Zero,CancellationToken.None);throw new Exception("wrong version accepted");}
        catch(InvalidDataException){if(attempts!=1||!mismatch.Disposed)throw new Exception("version mismatch retried or leaked");}
        attempts=0;var failed=new List<Hid>();
        try{await HardwareJobs.ReconnectVerifiedAsync(()=>{attempts++;var h=new Hid(true);failed.Add(h);return h;},new Version(0,10,0),false,TimeSpan.FromMilliseconds(30),TimeSpan.FromMilliseconds(5),CancellationToken.None);throw new Exception("timeout accepted");}
        catch(TimeoutException){if(attempts<1||failed.Any(h=>!h.Disposed))throw new Exception("timeout leaked HID");}
    }
}
