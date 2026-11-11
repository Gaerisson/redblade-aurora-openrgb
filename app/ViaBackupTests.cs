// SPDX-License-Identifier: GPL-2.0-or-later
namespace RedBladePlus.Studio;

internal static class ViaBackupTests
{
    sealed class Hid(int layers,int macroSize):IHidTransport
    {
        internal readonly byte[] Keys=new byte[layers*252],Macros=new byte[macroSize];
        internal int Writes;
        public Task<byte[]> ExchangeAsync(byte[] request,CancellationToken ct)
        {
            var reply=(byte[])request.Clone();
            switch(request[0])
            {
                case 0x11:reply[1]=(byte)layers;break;
                case 0x0d:reply[1]=(byte)(macroSize>>8);reply[2]=(byte)macroSize;break;
                case 0x12:case 0x0e:
                    (request[0]==0x12?Keys:Macros).AsSpan((request[1]<<8)|request[2],request[3]).CopyTo(reply.AsSpan(4));break;
                case 0x13:case 0x0f:
                    Writes++;request.AsSpan(4,request[3]).CopyTo((request[0]==0x13?Keys:Macros).AsSpan((request[1]<<8)|request[2]));break;
                default:throw new Exception("Unexpected VIA backup request.");
            }
            return Task.FromResult(reply);
        }
        public void Dispose(){}
    }
    internal static async Task Run()
    {
        var identity=new KeyboardIdentity(new Version(0,11,0),false,13);
        using var original=new Hid(1,4);original.Keys[0]=42;original.Macros[0]=1;
        var backup=await ViaBackup.Read(original,identity,CancellationToken.None);
        foreach(var dimensions in new[]{(2,4,13),(1,8,13),(1,4,12)})
        {
            using var changed=new Hid(dimensions.Item1,dimensions.Item2);bool refused=false;
            try{await backup.RestoreIfChanged(changed,identity with{ViaProtocol=dimensions.Item3},_=>{},CancellationToken.None);}
            catch(InvalidDataException e){refused=e.Message.Contains("backup protocol 13",StringComparison.Ordinal)&&e.Message.Contains("keyboard protocol",StringComparison.Ordinal);}
            if(!refused||changed.Writes!=0)throw new Exception("Changed VIA dimensions allowed writes or lost diagnostic metadata.");
        }
        using var compatible=new Hid(1,4);await backup.RestoreIfChanged(compatible,identity,_=>{},CancellationToken.None);
        if(!compatible.Keys.SequenceEqual(original.Keys)||!compatible.Macros.SequenceEqual(original.Macros)||compatible.Writes!=2)throw new Exception("Compatible VIA backup failed verified restoration.");
    }
}
