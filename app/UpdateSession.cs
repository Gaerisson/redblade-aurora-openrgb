// SPDX-License-Identifier: GPL-2.0-or-later
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace RedBladePlus.Studio;

internal sealed class UpdatePreferences
{
    public bool CheckAutomatically{get;set;}=true;
    public bool DownloadAutomatically{get;set;}=true;
    public bool RestartWhenIdle{get;set;}=true;
    public bool IncludePreviews{get;set;}=true;
    static string FilePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RedBladePlusStudio","updates.json");
    internal static UpdatePreferences Load(){try{return JsonSerializer.Deserialize<UpdatePreferences>(BoundedFile.Read(FilePath,4096))??new();}catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException){return new();}}
    internal void Save(){Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);File.WriteAllBytes(FilePath,JsonSerializer.SerializeToUtf8Bytes(this));}
}

internal sealed class UpdateSession:IDisposable
{
    readonly StudioForm owner;readonly UpdateService service=new();readonly CancellationTokenSource lifetime=new();
    readonly System.Windows.Forms.Timer timer=new(){Interval=5000};bool working,applying,disposed,automaticInstallFailed,handoffComplete;
    internal UpdatePreferences Preferences{get;}
    internal bool Testing{get;}
    internal static Version CurrentVersion{get; }=Normalize(Assembly.GetExecutingAssembly().GetName().Version!);
    static Version Normalize(Version value)=>new(value.Major,value.Minor,value.Build);
    internal UpdateCheck? Check{get;private set;}
    internal PreparedSoftware? Software{get;private set;}
    internal Dictionary<string,DownloadedFirmware> Firmware{get;}=new();
    internal bool HardwareBusy{get;set;}
    internal bool WizardOpen{get;set;}
    internal bool Busy=>working||applying||HardwareBusy||owner.IsHardwareBusy;
    internal bool Installing=>applying&&!handoffComplete;
    internal string Status{get;private set;}="";
    DateTimeOffset lastAttempt=DateTimeOffset.MinValue;
    internal event Action? Changed;
    internal UpdateSession(StudioForm owner,bool testing=false)
    {
        this.owner=owner;Testing=testing;Preferences=testing?new():UpdatePreferences.Load();owner.FormClosing+=PreventClosing;timer.Tick+=async(_,_)=>
        {
            if(!disposed&&Preferences.CheckAutomatically&&!Busy&&DateTimeOffset.UtcNow-lastAttempt>TimeSpan.FromMinutes(10))await Refresh(false);
            if(!disposed&&!automaticInstallFailed&&Preferences.RestartWhenIdle&&Software!=null&&!Busy&&!WizardOpen&&!owner.HasUnsavedChanges&&IdleMilliseconds()>30000)
                await InstallSoftware();
        };
        if(!testing)timer.Start();
    }
    string T(string en,string fr)=>owner.IsFrench?fr:en;
    void State(string text){Status=text;Changed?.Invoke();}
    internal async Task Start(){if(!Testing&&Preferences.CheckAutomatically)await Refresh(false);}
    internal async Task Refresh(bool manual=true)
    {
        if(Testing||working||disposed)return;working=true;lastAttempt=DateTimeOffset.UtcNow;
        try
        {
            State(T("Checking GitHub releases…","Recherche des mises à jour GitHub…"));
            Check=await service.CheckAsync(Preferences.IncludePreviews?UpdateChannel.Preview:UpdateChannel.Stable,CurrentVersion,lifetime.Token,manual);
            State(Check.Offer==null?Check.Diagnostic:Check.SoftwareNewer?T($"Studio {Check.Offer.Version} is available.",$"Studio {Check.Offer.Version} est disponible."):T("Studio is up to date.","Studio est à jour."));
            if(Check.SoftwareNewer&&Preferences.DownloadAutomatically&&Check.Offer!=null)
            {
                Software=await service.DownloadSoftwareAsync(Check.Offer,lifetime.Token);
                State(T("Studio update ready. It will restart after 30 seconds of inactivity, with no unsaved changes or keyboard operation.","Mise à jour prête. Redémarrage après 30 secondes d’inactivité, sans modification non enregistrée ni opération clavier."));
            }
            // Tiny firmware assets are prepared automatically; flashing always uses the guided flow.
            if(Check.Offer!=null)
            {
                foreach(string edition in new[]{"full","core"})
                    try{Firmware[edition]=await service.DownloadFirmwareAsync(Check.Offer,edition,lifetime.Token);}
                    catch(Exception e)when(e is IOException or HttpRequestException or InvalidOperationException or JsonException){Diagnostics.Record(e,"Firmware update discovery");}
            }
        }
        catch(OperationCanceledException)when(lifetime.IsCancellationRequested){}
        catch(Exception error){Diagnostics.Record(error,"Update check");State(T("Update check unavailable. Offline editing and bundled firmware remain available. ","Recherche indisponible. L’édition hors ligne et le firmware intégré restent disponibles. ")+error.Message);}
        finally{working=false;Changed?.Invoke();}
    }
    internal async Task DownloadSoftware()
    {
        if(Testing||Busy||Check?.Offer==null||!Check.SoftwareNewer)return;working=true;
        try{State(T("Downloading and verifying Studio…","Téléchargement et vérification de Studio…"));Software=await service.DownloadSoftwareAsync(Check.Offer,lifetime.Token);State(T("Update ready to restart.","Mise à jour prête au redémarrage."));}
        catch(Exception e){Diagnostics.Record(e,"Software download");State(e.Message);}
        finally{working=false;Changed?.Invoke();}
    }
    internal async Task InstallSoftware()
    {
        if(Testing||Busy||Software==null||owner.HasUnsavedChanges)return;applying=true;timer.Stop();
        using var interaction=new UpdateInteractionLock(owner);
        try
        {
            State(T("Starting the verified update…","Démarrage de la mise à jour vérifiée…"));
            await Task.Run(()=>SoftwareUpdate.Launch(Software));
            handoffComplete=true;interaction.Complete();
            foreach(Form form in owner.OwnedForms)form.Close();
            owner.Close();
        }
        catch(Exception e)
        {
            applying=false;automaticInstallFailed=true;
            Diagnostics.Record(e,"Software installation");State(e.Message);timer.Start();
        }
    }
    void PreventClosing(object? sender,FormClosingEventArgs e){if(Installing)e.Cancel=true;}
    [StructLayout(LayoutKind.Sequential)]struct LastInput{internal uint Size,Tick;}
    [DllImport("user32.dll")]static extern bool GetLastInputInfo(ref LastInput input);
    static uint IdleMilliseconds(){var input=new LastInput{Size=(uint)Marshal.SizeOf<LastInput>()};return GetLastInputInfo(ref input)?unchecked((uint)Environment.TickCount-input.Tick):0;}
    public void Dispose(){if(disposed)return;disposed=true;owner.FormClosing-=PreventClosing;lifetime.Cancel();timer.Dispose();service.Dispose();lifetime.Dispose();}
}
