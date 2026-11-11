// SPDX-License-Identifier: GPL-2.0-or-later
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
namespace RedBladePlus.Studio;
internal sealed partial class StudioForm
{
    internal void ShowPreviewPage(string name,bool useFrench)
    {
        if(!preview)throw new InvalidOperationException("Preview mode required.");
        previewPage=name.ToLowerInvariant() switch{"keyboard"=>"keyboard","functions"=>"functions","effects" or "colors"=>"effects","firmware" or "updates" or "guide"=>"guide",_=>throw new ArgumentException("Unknown preview page.")};french=useFrench;
    }
    async Task SelectPage(string page)
    {
        if(page is not("keyboard" or "effects" or "functions" or "guide"))throw new ArgumentException("Unknown page.");
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(45));
        await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-page=\""+page+"\"]').click()");
    }
    internal async Task CapturePreviewToFile(string path)
    {
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(45));await Task.Delay(400);await SelectPage(previewPage);await Task.Delay(150);
        using var output=File.Create(Path.GetFullPath(path));await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
    }
    async Task<JsonElement> Script(string script)=>JsonDocument.Parse(await web.CoreWebView2.ExecuteScriptAsync(script)).RootElement.Clone();
    internal async Task RunUiTests()
    {
        if(!preview)throw new InvalidOperationException("UI tests require hardware-disabled mode.");
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(45));await Task.Delay(300);
        if((await Script("document.querySelectorAll('#keyboardCanvas .key').length")).GetInt32()!=109)throw new Exception("Web UI did not render all 109 keys.");
        for(int cycle=0;cycle<10;cycle++)
        {
            foreach(string page in new[]{"keyboard","effects","functions","guide"}){await SelectPage(page);await Task.Delay(30);}
            await Script("document.getElementById('language').click()");await Task.Delay(30);
        }
        await SelectPage("keyboard");await Script("document.getElementById('selectAll').click();document.getElementById('keyColor').value='#7b2d42';document.getElementById('paint').click()");await Task.Delay(150);
        if(Current.Keys[0]!=Hsv.FromColor(Color.FromArgb(123,45,66))||!dirty)throw new Exception("Web key-color action failed to reach the C# model.");
        await SelectPage("functions");await Task.Delay(80);await Script("document.querySelector('#advancedOptions input[type=checkbox]').click()");await Task.Delay(100);
        if((Current.Modules&1024)==0)throw new Exception("Optional module action failed to reach firmware configuration.");
        var savedMaster=Current.Master;
        var pending=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var preparation=PerformHardware(async()=>{await pending.Task;return null;});
        bool rejectedEdit=false,rejectedSecond=false;
        try{await Dispatch(StudioBridge.Parse("{\"id\":\"race-edit\",\"action\":\"patchProfile\",\"args\":{\"master\":123}}"));}catch(InvalidOperationException){rejectedEdit=true;}
        try{await PerformHardware(()=>Task.FromResult<JobRequest?>(null));}catch(InvalidOperationException){rejectedSecond=true;}
        pending.SetResult(true);await preparation;
        if(!rejectedEdit||!rejectedSecond||Current.Master!=savedMaster)throw new Exception("Preparation allowed competing edits or keyboard operations.");
        var restoration=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);busy=true;effectActive=true;runningJob=restoration.Task;
        var stopping=PerformHardware(()=>Task.FromResult<JobRequest?>(null));
        rejectedEdit=false;
        try{await Dispatch(StudioBridge.Parse("{\"id\":\"race-stop\",\"action\":\"patchProfile\",\"args\":{\"master\":124}}"));}catch(InvalidOperationException){rejectedEdit=true;}
        busy=false;effectActive=false;restoration.SetResult(true);await stopping;
        if(!rejectedEdit||Current.Master!=savedMaster)throw new Exception("Effect restoration allowed edits before the save transition completed.");
        await SelectPage("guide");await Script("document.getElementById('zoomIn').click();document.getElementById('search').value='turbo';document.getElementById('search').dispatchEvent(new Event('input'))");
        var errors=await Script("window.__studioErrors");if(errors.GetArrayLength()!=0)throw new Exception("Web UI error: "+errors.GetRawText());
        if(!(await Script("document.getElementById('connect').disabled&&document.getElementById('save').disabled&&document.getElementById('flash').disabled")).GetBoolean())throw new Exception("Preview enabled a hardware action.");
    }
}
