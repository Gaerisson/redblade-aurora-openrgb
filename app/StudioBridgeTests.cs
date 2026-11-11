// SPDX-License-Identifier: GPL-2.0-or-later
namespace RedBladePlus.Studio;
internal static class StudioBridgeTests
{
    internal static void Run()
    {
        if(!StudioBridge.AllowedOrigin("https://redblade.local/index.html#keyboard"))throw new Exception("Local document rejected.");
        foreach(string origin in new[]{"https://redblade.local.evil/index.html","http://redblade.local/index.html","https://user@redblade.local/index.html","https://redblade.local/index.html?remote=1","file:///index.html"})if(StudioBridge.AllowedOrigin(origin))throw new Exception("Untrusted bridge origin accepted.");
        foreach(string json in new[]{"{\"id\":\"a\",\"action\":\"execute\",\"args\":{}}","{\"id\":\"a\",\"action\":\"getState\",\"args\":{},\"path\":\"x\"}","{\"id\":\"a\",\"id\":\"b\",\"action\":\"getState\",\"args\":{}}"})
        {
            bool rejected=false;try{StudioBridge.Parse(json);}catch(System.IO.InvalidDataException){rejected=true;}if(!rejected)throw new Exception("Invalid bridge input accepted.");
        }
        var request=StudioBridge.Parse("{\"id\":\"first-1\",\"action\":\"setKeys\",\"args\":{\"color\":\"#DD002B\",\"leds\":[0,108]}}");
        if(request.Action!="setKeys"||StudioBridge.Color(request.Args,"color").R!=221)throw new Exception("Typed color action failed.");
    }
}
