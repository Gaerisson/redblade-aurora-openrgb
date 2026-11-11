// SPDX-License-Identifier: GPL-2.0-or-later
namespace RedBladePlus.Studio;

internal static class IdentityTests
{
    sealed class Hid(bool stale) : IHidTransport
    {
        public Task<byte[]> ExchangeAsync(byte[] request,CancellationToken cancel)
        {
            var reply=(byte[])request.Clone();
            if(request[0]==1)reply[2]=13;
            else { reply[0]=0xff; if(stale)reply[1]=0x82; }
            return Task.FromResult(reply);
        }
        public void Dispose(){}
    }
    internal static async Task Run()
    {
        using var legacy=new Hid(false);var identity=await KeyboardProtocol.Identify(legacy,CancellationToken.None);
        if(!identity.Legacy||identity.DisplayName.Contains("Full 0.0.0",StringComparison.Ordinal))throw new Exception("Legacy firmware was presented as a verified Full version.");
        using var stale=new Hid(true);
        try { await KeyboardProtocol.Identify(stale,CancellationToken.None); }
        catch(InvalidDataException) { return; }
        throw new Exception("An unrelated FF reply was accepted as Full 0.0.0.");
    }
}
