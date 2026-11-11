// SPDX-License-Identifier: GPL-2.0-or-later
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace RedBladePlus.Studio;

internal sealed partial class StudioForm:Form
{
    readonly bool preview;bool previewMap,previewFn;readonly WebView2 web=new(){Dock=DockStyle.Fill,DefaultBackgroundColor=Color.FromArgb(14,15,19)};
    readonly TaskCompletionSource<bool> ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly SemaphoreSlim operationGate=new(1,1);bool cancelPreparation;
    bool Transitioning=>operationGate.CurrentCount==0;
    StudioConfig config=StudioConfig.Default();bool dirty,french,busy,effectActive,closeAfterEffect;uint? generation;
    readonly EditHistory history=new();
    string firmware="",connection="offline",currentPage="keyboard",previewPage="keyboard",lastError="",openRgbStatus="";bool redbladeFound;int sdkPort=6742;Version? installedVersion;
    static readonly Version BundledVersion=FirmwareImage.Load("full").Version;
    UpdateSession? updates;JobResult? job;string? jobId;Task? runningJob;EffectSettings effect=new("wave","#DD002B","#FFFFFF",40,180);
    internal bool IsHardwareBusy=>busy||Transitioning;
    internal bool HasUnsavedChanges=>dirty;
    internal bool IsFrench=>french;
    OnboardProfile Current=>config.Profiles[config.Active];
    string T(string en,string fr)=>french?fr:en;
    internal StudioForm(bool preview)
    {
        this.preview=preview;french=System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="fr";
        Text="RedBlade+ Studio";ClientSize=new(1400,880);MinimumSize=new(1080,720);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(14,15,19);
        using var icon=typeof(StudioForm).Assembly.GetManifestResourceStream("app.ico");if(icon!=null)Icon=new Icon(icon);
        Controls.Add(web);Shown+=async(_,_)=>await InitializeWeb();
        FormClosing+=(_,e)=>
        {
            if(Transitioning){e.Cancel=true;return;}
            if(!busy)return;e.Cancel=true;
            if(effectActive){closeAfterEffect=true;if(jobId!=null)HardwareJobs.Cancel(jobId);}
            else MessageBox.Show(this,T("Wait for the keyboard operation and RGB restoration to finish.","Attendez la fin de l’opération clavier et le rétablissement du RGB."),Text);
        };
        if(!preview)LoadDraft();
    }
    internal void AttachUpdates(UpdateSession session){updates=session;updates.Changed+=SendState;}
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);try{int enabled=1,caption=0x130F0E,text=0xF3EFEF;DwmSetWindowAttribute(Handle,20,ref enabled,4);DwmSetWindowAttribute(Handle,35,ref caption,4);DwmSetWindowAttribute(Handle,36,ref text,4);}catch(DllNotFoundException){}
    }
    [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    async Task InitializeWeb()
    {
        try
        {
            string cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RedBladePlusStudio","Browser");
            var environment=await CoreWebView2Environment.CreateAsync(userDataFolder:cache);
            await web.EnsureCoreWebView2Async(environment);var core=web.CoreWebView2;
            core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.IsStatusBarEnabled=false;
            core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;core.Settings.AreHostObjectsAllowed=false;
            if(preview)await core.AddScriptToExecuteOnDocumentCreatedAsync("window.__studioErrors=[];window.addEventListener('error',e=>window.__studioErrors.push(e.message));window.addEventListener('unhandledrejection',e=>window.__studioErrors.push(String(e.reason)));");
            core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
            core.WebResourceRequested+=(_,e)=>ServeResource(environment,e);
            core.NavigationStarting+=(_,e)=>{if(!StudioBridge.AllowedOrigin(e.Uri))e.Cancel=true;};
            core.NewWindowRequested+=(_,e)=>e.Handled=true;
            core.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
            core.DownloadStarting+=(_,e)=>e.Cancel=true;
            core.WebMessageReceived+=async(_,e)=>
            {
                if(!StudioBridge.AllowedOrigin(e.Source))return;StudioRequest? request=null;
                try{request=StudioBridge.Parse(e.WebMessageAsJson);object? result=await Dispatch(request);Post(new{id=request.Id,ok=true,data=result});}
                catch(Exception error){Diagnostics.Record(error,"Studio action "+request?.Action);Post(new{id=request?.Id??"",ok=false,error=StudioText.Localize(error.Message,french,true)});}
                finally{SendState();}
            };
            core.NavigationCompleted+=async(_,e)=>
            {
                if(!e.IsSuccess){ready.TrySetException(new IOException("Web interface could not load."));return;}
                SendState();ready.TrySetResult(true);
                if(preview&&previewPage!="keyboard"){await Task.Delay(150);await SelectPage(previewPage);}
            };
            core.ProcessFailed+=(_,e)=>{Diagnostics.Record(new IOException(e.ProcessFailedKind.ToString()),"Web renderer");connection="error";};
            core.Navigate("https://redblade.local/index.html");
        }
        catch(Exception error)
        {
            Diagnostics.Record(error,"WebView2 initialization");ready.TrySetException(error);
            if(!preview)MessageBox.Show(this,T("Install or repair Microsoft Edge WebView2 Runtime, then restart Studio.\n", "Installez ou réparez Microsoft Edge WebView2 Runtime, puis relancez Studio.\n")+error.Message,Text);
        }
    }
    static readonly Dictionary<string,(string Resource,string Mime)> Resources=new(StringComparer.Ordinal){["index.html"]=("web.index.html","text/html"),["styles.css"]=("web.styles.css","text/css"),["app.js"]=("web.app.js","text/javascript"),["map-viewer.js"]=("web.map-viewer.js","text/javascript"),["fn-guide.js"]=("web.fn-guide.js","text/javascript"),["brand.png"]=("brand.png","image/png"),["shortcuts.png"]=("shortcuts.png","image/png"),["shortcuts.fr.png"]=("shortcuts.fr.png","image/png"),["app.ico"]=("app.ico","image/x-icon"),["layout.json"]=("layout.json","application/json")};
    static void ServeResource(CoreWebView2Environment environment,CoreWebView2WebResourceRequestedEventArgs e)
    {
        var uri=new Uri(e.Request.Uri);string name=uri.AbsolutePath.TrimStart('/');
        bool allowed=uri.Scheme=="https"&&uri.Host=="redblade.local"&&uri.IsDefaultPort&&string.IsNullOrEmpty(uri.UserInfo)&&string.IsNullOrEmpty(uri.Query);
        if(!allowed||!Resources.TryGetValue(name,out var file)){e.Response=environment.CreateWebResourceResponse(new MemoryStream(),404,"Not Found","Content-Type: text/plain");return;}
        const string policy="default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'";
        e.Response=environment.CreateWebResourceResponse(new MemoryStream(Embedded.Read(file.Resource)),200,"OK","Content-Type: "+file.Mime+"; charset=utf-8\r\nCache-Control: no-store\r\nContent-Security-Policy: "+policy);
    }
    void Post(object message){if(!IsDisposed&&web.CoreWebView2!=null)web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message,StudioBridge.Json));}
    object Snapshot()=>new
    {
        app=new{version=UpdateSession.CurrentVersion.ToString(),language=french?"fr":"en",dirty,busy=Transitioning||busy&&!effectActive||updates?.Installing==true,preview,error=StudioText.Localize(lastError,french,true)},keyboard=new{status=connection,firmware=firmware.StartsWith("Stock / legacy",StringComparison.Ordinal)?T("Stock / legacy (version unavailable)","D’origine / ancien (version indisponible)"):firmware,version=installedVersion?.ToString()},
        setup=new{openRgbStatus,redbladeFound,sdkPort,startWithWindows=!preview&&StartupIntegration.Enabled,hardwareSessionReady=!preview&&(HardwareBroker.Ready||!HardwareJobs.RequiresElevation())},
        firmware=new{bundledVersion=BundledVersion.ToString(),latestVersion=updates?.Firmware.GetValueOrDefault("full")?.Version.ToString(),recommendedSource=updates?.Firmware.GetValueOrDefault("full")?.Version>=BundledVersion?"latest":"bundled",edition="full"},
        capabilities=new{studio=connection=="connected",extendedOptions=preview||installedVersion==null||installedVersion>=new Version(0,10,0),reactiveStyles=preview||installedVersion==null||installedVersion>=new Version(0,11,0),extendedReaction=preview||installedVersion==null||installedVersion>=new Version(0,12,0),softwareEffects=!preview},
        editor=new{history.CanUndo,history.CanRedo},
        profile=new{Current.Modules,Current.Master,Current.DefaultMode,Current.TimeoutSeconds,Current.TurboRate,Current.RecorderSpeed,Current.TrailFrames,Current.CapsWordLayout,Current.ReactiveStyle,Current.ReactiveWidth,Current.ReactiveRate,Current.HighlightMode,safetyTimeoutSeconds=Current.SafetyTimeoutSeconds==0?30:Current.SafetyTimeoutSeconds,keys=Current.Keys,effects=Current.Effects},
        layout=KeyboardLayout.Load(),updates=new{status=updates?.Status??"",availableVersion=updates?.Check?.SoftwareNewer==true?updates.Check.Offer?.Version.ToString():null,softwareReady=updates?.Software!=null,preferences=updates?.Preferences??new UpdatePreferences()},
        job=job==null?null:new{job.Stage,job.Done,job.Success,job.Programming,message=StudioText.Localize(job.Message,french,job.Done&&!job.Success),diagnostic=job.Message,job.Firmware,job.BackupDirectory},softwareEffect=new{active=effectActive,settings=effect,effect.Preset,effect.Color1,effect.Color2,effect.Speed,effect.Brightness}
    };
    internal void SendState(){if(IsDisposed)return;if(InvokeRequired){BeginInvoke((Action)SendState);return;}Post(new{type="state",state=Snapshot()});}
    void Changed(){ConfigCodec.Validate(config);dirty=true;SaveDraft();SendState();}
    void AcceptEdit(StudioConfig next)
    {
        ConfigCodec.Validate(next);
        if(ProfileFile.Save(config).SequenceEqual(ProfileFile.Save(next)))return;
        history.Record(config);config=next;Changed();
    }
    void EnsureIdle(){if(Transitioning||busy&&!effectActive||updates?.Installing==true)throw new InvalidOperationException(T("A keyboard operation is already running.","Une opération clavier est déjà en cours."));}
    bool ConfirmReplacement()=>!dirty||MessageBox.Show(this,T("Replace unsaved local edits with the keyboard settings?","Remplacer les modifications locales non enregistrées par les réglages du clavier ?"),Text,MessageBoxButtons.YesNo)==DialogResult.Yes;
    protected override void Dispose(bool disposing){if(disposing){if(updates!=null)updates.Changed-=SendState;web.Dispose();}base.Dispose(disposing);}
}
