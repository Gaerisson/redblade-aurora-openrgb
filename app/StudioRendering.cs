// SPDX-License-Identifier: GPL-2.0-or-later
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
namespace RedBladePlus.Studio;
internal sealed partial class StudioForm
{
    internal void ShowPreviewPage(string name,bool useFrench)
    {
        if(!preview)throw new InvalidOperationException("Preview mode required.");
        previewMap=name.Equals("map",StringComparison.OrdinalIgnoreCase);previewFn=name.Equals("fn",StringComparison.OrdinalIgnoreCase);
        previewPage=name.ToLowerInvariant() switch{"keyboard" or "fn"=>"keyboard","functions"=>"functions","effects" or "colors"=>"effects","firmware" or "setup"=>"setup","updates" or "guide" or "map"=>"guide",_=>throw new ArgumentException("Unknown preview page.")};french=useFrench;
    }
    async Task SelectPage(string page)
    {
        if(page is not("keyboard" or "effects" or "functions" or "guide" or "setup"))throw new ArgumentException("Unknown page.");
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(45));
        await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-page=\""+page+"\"]').click()");
    }
    internal async Task CapturePreviewToFile(string path)
    {
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(45));await Task.Delay(400);await SelectPage(previewPage);await Task.Delay(150);
        if(previewPage=="keyboard")
        {
            await Script("document.getElementById('selectAll').click();document.getElementById('gradientMode').value='linear';document.getElementById('keyColor').value='#dd002b';document.getElementById('keyColor2').value='#773dff';document.getElementById('gradientAngle').value=0;document.getElementById('paint').click()");await Task.Delay(120);
            await Script("document.getElementById('groupSelect').value='gaming';document.getElementById('groupSelect').dispatchEvent(new Event('change'))");
        }
        if(previewFn)await Script("document.getElementById('fnGuideToggle').click();document.querySelector('.key[data-led=\"21\"]').click();document.getElementById('fnHelpShift').click()");
        if(previewPage=="functions")await Dispatch(StudioBridge.Parse("{\"id\":\"preview-config\",\"action\":\"patchProfile\",\"args\":{\"reactiveStyle\":3,\"reactiveRate\":4,\"highlightMode\":1}}"));
        if(previewPage=="effects")await Script("document.querySelectorAll('#presets button')[2].click()");
        if(previewMap){await Script("document.getElementById('openShortcutMap').click()");await Task.Delay(100);}
        await Script("window.scrollTo(0,0)");await Task.Delay(80);
        using var output=File.Create(Path.GetFullPath(path));await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
    }
    async Task<JsonElement> Script(string script)=>JsonDocument.Parse(await web.CoreWebView2.ExecuteScriptAsync(script)).RootElement.Clone();
    internal async Task CaptureUiFailure()
    {
        if(!preview||web.CoreWebView2==null)return;
        string folder=AppContext.BaseDirectory;
        using(var output=File.Create(Path.Combine(folder,"ui-test-failure.png")))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
        var context=await Script("JSON.stringify({width:innerWidth,height:innerHeight,scale:devicePixelRatio,page:document.querySelector('.page.active')?.id,fnText:document.getElementById('fnHelpText')?.textContent,fnKeys:document.getElementById('fnHelpKeys')?.textContent,errors:window.__studioErrors})");
        File.WriteAllText(Path.Combine(folder,"ui-test-context.json"),context.GetString());
    }
    async Task Mouse(string type,double x,double y,bool down=false)
        =>await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type,x,y,button=type=="mouseMoved"?"none":"left",buttons=down?1:0,clickCount=type=="mouseMoved"?0:1}));
    async Task Drag(double sx,double sy,double ex,double ey)
    {
        await Mouse("mouseMoved",sx,sy);await Mouse("mousePressed",sx,sy,true);
        for(int n=1;n<=12;n++)await Mouse("mouseMoved",sx+(ex-sx)*n/12,sy+(ey-sy)*n/12,true);
        await Mouse("mouseReleased",ex,ey);await Task.Delay(70);
    }
    internal async Task RunUiTests()
    {
        if(!preview)throw new InvalidOperationException("UI tests require hardware-disabled mode.");
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(45));await Task.Delay(300);
        await SelectPage("guide");
        for(int language=0;language<2;language++)
        {
            JsonElement map=default;
            for(int attempt=0;attempt<40;attempt++)
            {
                map=await Script("(()=>{const i=document.getElementById('shortcutImage'),r=i.getBoundingClientRect();return {complete:i.complete,width:i.naturalWidth,height:i.naturalHeight,top:r.top,viewport:innerHeight,src:i.getAttribute('src'),statusHidden:document.getElementById('mapStatus').hidden}})()");
                if(map.GetProperty("complete").GetBoolean()&&map.GetProperty("width").GetInt32()>0)break;
                await Task.Delay(75);
            }
            var expectedPng=Embedded.Read(french?"shortcuts.fr.png":"shortcuts.png");
            int expectedWidth=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(expectedPng.AsSpan(16,4)),expectedHeight=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(expectedPng.AsSpan(20,4));
            if(map.GetProperty("width").GetInt32()!=expectedWidth||map.GetProperty("height").GetInt32()!=expectedHeight||!map.GetProperty("statusHidden").GetBoolean())throw new Exception("Shortcut image failed to load: "+map.GetRawText());
            if(map.GetProperty("src").GetString()!=(french?"shortcuts.fr.png":"shortcuts.png"))throw new Exception("Wrong shortcut image language.");
            if(map.GetProperty("top").GetDouble()>=map.GetProperty("viewport").GetDouble()-100)throw new Exception("Shortcut image is below the initial guide viewport: "+map.GetRawText());
            if(!(await Script("document.querySelector('.update-card').parentElement.parentElement.classList.contains('guide-grid')")).GetBoolean())throw new Exception("Guide columns are not in the same grid.");
            await Script("window.__mapReloads=0;document.getElementById('shortcutImage').addEventListener('load',()=>window.__mapReloads++);document.getElementById('zoomIn').click()");
            var zoomed=await Script("document.getElementById('shortcutImage').style.width");
            for(int repeat=0;repeat<3;repeat++){SendState();await Task.Delay(60);}
            if((await Script("window.__mapReloads")).GetInt32()!=0||(await Script("document.getElementById('shortcutImage').style.width")).GetString()!=zoomed.GetString())throw new Exception("State refresh reloaded the map or reset zoom.");
            await Script("document.getElementById('zoomOut').click();document.getElementById('language').click()");await Task.Delay(80);
        }
        await Script("document.getElementById('openShortcutMap').click()");await Task.Delay(100);
        if(!(await Script("document.getElementById('shortcutViewer').open&&document.activeElement.classList.contains('map-viewport')")).GetBoolean())throw new Exception("Clicking the shortcut map did not open the focused viewer.");
        await Script("document.querySelector('[data-map=actual]').click()");
        var viewerPoint=await Script("(()=>{const r=document.querySelector('.map-viewport').getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2}})()");
        double vx=viewerPoint.GetProperty("x").GetDouble(),vy=viewerPoint.GetProperty("y").GetDouble();
        await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type="mouseWheel",x=vx,y=vy,deltaX=0,deltaY=-300}));await Task.Delay(80);
        if((await Script("parseInt(document.querySelector('#shortcutViewer output').textContent)")).GetInt32()<=100)throw new Exception("Mouse wheel did not zoom the shortcut map.");
        string priorPan=(await Script("document.querySelector('.map-viewport img').style.transform")).GetString()!;
        await Drag(vx,vy,vx-140,vy-100);
        if((await Script("document.querySelector('.map-viewport img').style.transform")).GetString()==priorPan)throw new Exception("Mouse dragging did not pan the enlarged map.");
        SendState();await Task.Delay(60);
        if(!(await Script("document.getElementById('shortcutViewer').open")).GetBoolean())throw new Exception("State refresh closed the map viewer.");
        await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"Escape\",\"code\":\"Escape\",\"windowsVirtualKeyCode\":27}");
        await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyUp\",\"key\":\"Escape\",\"code\":\"Escape\",\"windowsVirtualKeyCode\":27}");await Task.Delay(60);
        if((await Script("document.getElementById('shortcutViewer').open")).GetBoolean())throw new Exception("Escape did not close the shortcut viewer.");
        await Script("document.getElementById('expandMap').click();document.querySelector('[data-map=fit]').click();document.querySelector('[data-map=close]').click()");
        await SelectPage("keyboard");
        if((await Script("document.querySelectorAll('#keyboardCanvas .key').length")).GetInt32()!=109)throw new Exception("Web UI did not render all 109 keys.");
        for(int cycle=0;cycle<10;cycle++)
        {
            foreach(string page in new[]{"keyboard","effects","functions","guide","setup"}){await SelectPage(page);await Task.Delay(30);}
            await Script("document.getElementById('language').click()");await Task.Delay(30);
        }
        await SelectPage("keyboard");await Script("document.getElementById('selectAll').click();document.getElementById('keyColor').value='#7b2d42';document.getElementById('paint').click()");await Task.Delay(150);
        if(Current.Keys[0]!=Hsv.FromColor(Color.FromArgb(123,45,66))||!dirty)throw new Exception("Web key-color action failed to reach the C# model.");
        await Script("document.getElementById('clearSelection').click()");
        var points=await Script("(()=>{const a=document.querySelector('.key[data-led=\"1\"]').getBoundingClientRect(),b=document.querySelector('.key[data-led=\"4\"]').getBoundingClientRect();return {sx:a.x+a.width/2,sy:a.y+a.height/2,ex:b.x+b.width/2,ey:b.y+b.height/2}})()");
        await Drag(points.GetProperty("sx").GetDouble(),points.GetProperty("sy").GetDouble(),points.GetProperty("ex").GetDouble(),points.GetProperty("ey").GetDouble());
        if((await Script("JSON.stringify([...document.querySelectorAll('#keyboardCanvas .key.selected')].map(e=>Number(e.dataset.led)).sort((a,b)=>a-b))")).GetString()!="[1,2,3,4]")throw new Exception("Real drag selection did not select F1 through F4.");
        await Script("document.getElementById('gradientMode').value='linear';document.getElementById('keyColor').value='#ff0000';document.getElementById('keyColor2').value='#0000ff';document.getElementById('gradientAngle').value=0;document.getElementById('paint').click()");await Task.Delay(130);
        if(Current.Keys[1]!=new Hsv(0,255,255)||Current.Keys[4]!=Hsv.FromColor(Color.Blue))throw new Exception("Selected gradient did not reach the keyboard model.");
        await Script("document.getElementById('undo').click()");await Task.Delay(100);
        if(Current.Keys[1]!=Hsv.FromColor(Color.FromArgb(123,45,66)))throw new Exception("Undo did not restore the prior key colors.");
        await Script("document.getElementById('redo').click()");await Task.Delay(100);
        if(Current.Keys[4]!=Hsv.FromColor(Color.Blue))throw new Exception("Redo did not restore the gradient batch.");
        await Script("document.getElementById('clearSelection').click()");
        points=await Script("(()=>{const a=document.querySelector('.key[data-led=\"1\"]').getBoundingClientRect(),b=document.querySelector('.key[data-led=\"4\"]').getBoundingClientRect();return {sx:a.left-3,sy:a.top-3,ex:b.right+2,ey:b.bottom+2}})()");
        await Drag(points.GetProperty("sx").GetDouble(),points.GetProperty("sy").GetDouble(),points.GetProperty("ex").GetDouble(),points.GetProperty("ey").GetDouble());
        if((await Script("JSON.stringify([...document.querySelectorAll('#keyboardCanvas .key.selected')].map(e=>Number(e.dataset.led)).sort((a,b)=>a-b))")).GetString()!="[1,2,3,4]")throw new Exception("Real marquee selection did not respect keyboard geometry.");
        points=await Script("(()=>{const a=document.querySelector('.key[data-led=\"1\"]').getBoundingClientRect();return {x:a.x+a.width/2,y:a.y+a.height/2}})()");
        double singleX=points.GetProperty("x").GetDouble(),singleY=points.GetProperty("y").GetDouble();
        await Mouse("mouseMoved",singleX,singleY);await Mouse("mousePressed",singleX,singleY,true);await Mouse("mouseReleased",singleX,singleY);
        await Script("(()=>{const h=document.getElementById('keyColorValue');h.focus();h.value='#22cc88';h.dispatchEvent(new Event('change',{bubbles:true}));h.blur()})()");await Task.Delay(100);
        if((await Script("document.getElementById('keyColor').value")).GetString()!="#22cc88")throw new Exception("Inspector erased a typed hex color on blur.");
        await Script("document.getElementById('paint').click()");await Task.Delay(100);
        if(Current.Keys[1]!=Hsv.FromColor(Color.FromArgb(34,204,136)))throw new Exception("Hex color was not applied to the selected key.");
        await Script("document.querySelector('#swatches button[title=\"#30bfff\"]').click();document.getElementById('paint').focus()");await Task.Delay(100);
        if((await Script("document.getElementById('keyColor').value")).GetString()!="#30bfff")throw new Exception("Inspector erased a swatch choice on focus change.");
        await Script("document.getElementById('paint').click()");await Task.Delay(100);
        if(Current.Keys[1]!=Hsv.FromColor(Color.FromArgb(48,191,255)))throw new Exception("Palette swatch was not applied to the selected key.");
        await Script("document.getElementById('keyColor').value='#103020';document.getElementById('paint').click()");await Task.Delay(100);
        var dimColor=Current.Keys[1];await Script("document.getElementById('maximizeIntensity').click()");await Task.Delay(100);
        if(Current.Keys[1]!=(dimColor with {V=255}))throw new Exception("Selected intensity did not preserve hue and saturation.");
        await Script("document.getElementById('undo').click()");await Task.Delay(80);
        if(Current.Keys[1]!=dimColor)throw new Exception("Maximum intensity did not participate in undo.");
        if((await Script("getComputedStyle(document.querySelector('.key[data-led=\"1\"]')).backgroundImage")).GetString()!="none")throw new Exception("Keyboard color preview still applies an internal gradient.");
        await Script("document.getElementById('groupSelect').value='gaming';document.getElementById('groupSelect').dispatchEvent(new Event('change'))");
        if((await Script("document.querySelectorAll('#keyboardCanvas .key.selected').length")).GetInt32()!=4)throw new Exception("Gaming selection did not contain exactly four physical keys.");
        var beforeHelp=ConfigCodec.Encode(config);
        await Script("document.getElementById('fnGuideToggle').click()");
        await Script("document.querySelector('.key[data-led=\"21\"]').scrollIntoView({block:'center'})");await Task.Delay(80);
        var helpPoint=await Script("(()=>{const r=document.querySelector('.key[data-led=\"21\"]').getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2}})()");
        await Mouse("mouseMoved",helpPoint.GetProperty("x").GetDouble(),helpPoint.GetProperty("y").GetDouble());await Task.Delay(80);
        string once=(await Script("document.getElementById('fnHelpText').textContent")).GetString()!;
        await Script("document.getElementById('fnHelpShift').click()");await Task.Delay(60);
        if((await Script("document.getElementById('fnHelpPanel').hidden")).GetBoolean()||(await Script("document.getElementById('fnHelpText').textContent")).GetString()==once)throw new Exception("Fn explorer did not explain the Shift alternative: "+once+" -> "+(await Script("JSON.stringify({text:document.getElementById('fnHelpText').textContent,keys:document.getElementById('fnHelpKeys').textContent,shift:document.getElementById('fnHelpShift').checked})")).GetString());
        if(!ConfigCodec.Encode(config).SequenceEqual(beforeHelp))throw new Exception("Fn explorer changed keyboard settings.");
        await Script("document.getElementById('fnHelpClose').click()");
        await SelectPage("functions");await Task.Delay(80);await Script("document.querySelector('#advancedOptions input[type=checkbox]').click()");await Task.Delay(100);
        if((Current.Modules&1024)==0)throw new Exception("Optional module action failed to reach firmware configuration.");
        await Script("(()=>{const panel=document.getElementById('typingReaction');const color=panel.querySelector('input[type=color]');color.value='#26ffba';color.dispatchEvent(new Event('change'));const style=panel.querySelector('select');style.value='4';style.dispatchEvent(new Event('change'));const duration=panel.querySelector('input[type=range]');duration.value=1980;duration.dispatchEvent(new Event('change'))})()");await Task.Delay(140);
        if(Current.ReactiveStyle!=4||Current.TrailFrames!=15||Current.ReactiveRate!=4||Current.Effects[1]!=Hsv.FromColor(Color.FromArgb(38,255,186)))throw new Exception("Typing reaction controls did not reach persistent configuration.");
        await Dispatch(StudioBridge.Parse("""{"id":"effect-contract","action":"updateEffect","args":{"preset":"reactive","color1":"#ff0000","color2":"#0000ff","speed":55,"brightness":200,"angle":90,"scale":125,"reverse":true,"palette":["#ff0000","#00ff00","#0000ff"],"targetLeds":[0,108],"background":"#202020","reactiveEnabled":true,"reactiveStyle":"ripple","reactiveColor":"#00ff99","reactiveDurationMs":1200,"reactiveWidth":3,"reactiveOpacity":80}}"""));
        if(effect.Angle!=90||effect.Palette.Length!=3||effect.TargetLeds?.Length!=2||effect.ReactiveDurationMs!=1200||!effect.ReactiveEnabled)throw new Exception("Advanced effect arguments did not reach the native model.");
        await Script("(()=>{const select=document.querySelector('#modifierHighlights select');select.value='1';select.dispatchEvent(new Event('change'))})()");await Task.Delay(100);
        if(Current.HighlightMode!=1)throw new Exception("Modifier highlight settings did not reach persistent configuration.");
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
        await SelectPage("guide");await Script("document.querySelector('.shortcut-text').open=true;document.getElementById('zoomIn').click();document.getElementById('search').value='turbo';document.getElementById('search').dispatchEvent(new Event('input'))");
        var errors=await Script("window.__studioErrors");if(errors.GetArrayLength()!=0)throw new Exception("Web UI error: "+errors.GetRawText());
        await SelectPage("setup");
        if(!(await Script("['connect','save','flash','setupOpenRgb','testOpenRgb','testLighting'].every(id=>document.getElementById(id).disabled)")).GetBoolean())throw new Exception("Preview enabled a hardware action.");
        using(var notificationTest=new UpdateSession(this,testing:true))
        {
            updates=notificationTest;
            try
            {
                await Dispatch(StudioBridge.Parse("{\"id\":\"notice-open\",\"action\":\"updateNotice\",\"args\":{\"open\":true}}"));
                await Script("document.getElementById('updateDialog').showModal();document.getElementById('deferUpdate').click()");
                for(int attempt=0;attempt<40&&!notificationTest.DeferredUntilRestart;attempt++)await Task.Delay(50);
                for(int attempt=0;attempt<40&&(notificationTest.NoticeOpen||(await Script("document.getElementById('updateDialog').open")).GetBoolean());attempt++)await Task.Delay(50);
                if(!notificationTest.DeferredUntilRestart||notificationTest.NoticeOpen||(await Script("document.getElementById('updateDialog').open")).GetBoolean())throw new Exception("Later did not defer the updater and close its real dialog.");
            }
            finally{updates=null;}
        }
    }
}
