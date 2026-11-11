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
        var oldVersion=new Version(0,12,0);var newVersion=new Version(0,13,0);
        foreach(byte blank in new byte[]{0,1})
        {
            var source=new ViaBackup{Layers=2,Keymap=new byte[504],Macros=new byte[]{9,8}};
            source.Keymap[353]=blank;source.Keymap[100]=42;
            var upgraded=source.ForFirmware(oldVersion,newVersion,false);
            if(upgraded.Keymap[352]!=0x7e||upgraded.Keymap[353]!=0x62||upgraded.Keymap[100]!=42||!upgraded.Macros.SequenceEqual(source.Macros)||source.Keymap[352]!=0||source.Keymap[353]!=blank)
                throw new Exception("Fn+I migration failed or changed the original backup.");
            if(source.ForFirmware(oldVersion,newVersion,true).Keymap[353]!=blank||source.ForFirmware(newVersion,newVersion,false).Keymap[353]!=blank||source.ForFirmware(newVersion,oldVersion,false).Keymap[353]!=blank)
                throw new Exception("Fn+I migration applied to Core, same version or downgrade.");
        }
        var custom=new ViaBackup{Layers=2,Keymap=new byte[504],Macros=new byte[]{7}};custom.Keymap[352]=0x12;custom.Keymap[353]=0x34;
        if(!custom.ForFirmware(oldVersion,newVersion,false).Keymap.SequenceEqual(custom.Keymap))throw new Exception("Custom Fn+I mapping was overwritten.");
        custom.Keymap[352]=0;custom.Keymap[353]=2;
        if(!custom.ForFirmware(oldVersion,newVersion,false).Keymap.SequenceEqual(custom.Keymap))throw new Exception("Nonblank Fn+I mapping was overwritten.");
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
