// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text;
namespace RedBladePlus.Studio;
internal static class HardwareBrokerTests
{
    static void Check(bool value,string message){if(!value)throw new Exception("Broker test: "+message);}
    static void Reject(Action action){try{action();}catch(InvalidDataException){return;}throw new Exception("Broker test: expected rejection");}
    internal static void Run()
    {
        string id=Guid.NewGuid().ToString("N"),nonce=Guid.NewGuid().ToString("N"),other=Guid.NewGuid().ToString("N");
        byte[] entry=Encoding.ASCII.GetBytes(HardwareBroker.QueueContents(id,nonce));
        Check(HardwareBroker.ParseQueue(id+".job",entry,nonce)==id,"valid queue entry");
        Reject(()=>HardwareBroker.ParseQueue(other+".job",entry,nonce));
        Reject(()=>HardwareBroker.ParseQueue(id+".job",entry,other));
        Reject(()=>HardwareBroker.ParseQueue(id+".job",new byte[4097],nonce));
        Reject(()=>HardwareBroker.QueueContents("../arbitrary",nonce));
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Check(HardwareBroker.AcceptOnce(seen,id)&&!HardwareBroker.AcceptOnce(seen,id.ToUpperInvariant()),"replay rejected");
        string path=Environment.ProcessPath!;
        Check(StartupIntegration.Command(@"C:\Program Files\RedBladePlus\Studio.exe")=="\"C:\\Program Files\\RedBladePlus\\Studio.exe\"","startup executable is quoted without shell arguments");
        foreach(string invalid in new[]{"relative.exe",@"C:\Studio.exe\"+"\" --worker",@"C:\Studio.cmd","C:\\Studio\n.exe"})Reject(()=>StartupIntegration.Command(invalid));
        Check(HardwareBroker.SameOwner(1,123,"S-1-5-21",path,1,123,"S-1-5-21",path),"matching owner");
        Check(!HardwareBroker.SameOwner(2,123,"S-1-5-21",path,1,123,"S-1-5-21",path),"session mismatch");
        Check(!HardwareBroker.SameOwner(1,124,"S-1-5-21",path,1,123,"S-1-5-21",path),"PID reuse mismatch");
        Check(!HardwareBroker.SameOwner(1,123,"S-1-5-22",path,1,123,"S-1-5-21",path),"SID mismatch");
        Check(!HardwareBroker.SameOwner(1,123,"S-1-5-21",path,1,123,"S-1-5-21",path+"other"),"executable mismatch");
    }
}
