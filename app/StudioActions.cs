// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics;
using System.Text.Json;
namespace RedBladePlus.Studio;
internal sealed partial class StudioForm
{
    async Task<object?> Dispatch(StudioRequest request)
    {
        var a=request.Args;
        switch(request.Action)
        {
            case "getState":StudioBridge.Fields(a);return Snapshot();
            case "view":StudioBridge.Fields(a,"page");currentPage=StudioBridge.Text(a,"page");if(currentPage is not("keyboard" or "effects" or "functions" or "guide"))throw new InvalidDataException("Unknown page.");if(updates!=null)updates.WizardOpen=currentPage=="guide";return null;
            case "language":StudioBridge.Fields(a,"language");string language=StudioBridge.Text(a,"language");if(language is not("en" or "fr"))throw new InvalidDataException("Unknown language.");french=language=="fr";return Snapshot();
            case "setKeys":
                EnsureIdle();StudioBridge.Fields(a,"leds","color");string selectedColor=StudioBridge.Text(a,"color",7);
                AcceptEdit(KeyboardEditing.WithColors(config,a.GetProperty("leds").EnumerateArray().Select(v=>new KeyColorChange(v.GetInt32(),selectedColor)).ToArray()));return null;
            case "maximizeKeyIntensity":
                EnsureIdle();StudioBridge.Fields(a,"leds");
                AcceptEdit(KeyboardEditing.WithMaximumIntensity(config,a.GetProperty("leds").EnumerateArray().Select(v=>v.GetInt32()).ToArray()));return null;
            case "setKeyColors":
                EnsureIdle();StudioBridge.Fields(a,"colors");var rows=a.GetProperty("colors");
                if(rows.ValueKind!=JsonValueKind.Array||rows.GetArrayLength() is <1 or >109)throw new InvalidDataException("Invalid color batch.");
                var colors=new List<KeyColorChange>();foreach(var row in rows.EnumerateArray()){StudioBridge.Fields(row,"led","color");colors.Add(new(StudioBridge.Number(row,"led",0,108),StudioBridge.Text(row,"color",7)));}
                AcceptEdit(KeyboardEditing.WithColors(config,colors));return null;
            case "undo":case "redo":
                EnsureIdle();StudioBridge.Fields(a);var prior=request.Action=="undo"?history.Undo(config):history.Redo(config);if(prior!=null){config=prior;Changed();}return null;
            case "setEffectColor":
                EnsureIdle();StudioBridge.Fields(a,"index","color");int index=StudioBridge.Number(a,"index",0,23);var nextColor=KeyboardEditing.Clone(config);nextColor.Profiles[nextColor.Active].Effects[index]=Hsv.FromColor(StudioBridge.Color(a,"color"));AcceptEdit(nextColor);return null;
            case "patchProfile":EnsureIdle();Patch(a);return null;
            case "connect":StudioBridge.Fields(a);await ReadPreservingEdits();return null;
            case "save":StudioBridge.Fields(a);await PerformHardware(()=>{RequireConfiguration();return Task.FromResult<JobRequest?>(new(){Operation=HardwareOperation.Save,ExpectedGeneration=generation!.Value,Payload=Convert.ToBase64String(ConfigCodec.Encode(config))});});return null;
            case "preview":StudioBridge.Fields(a);await PerformHardware(()=>{RequireConfiguration();return Task.FromResult<JobRequest?>(new(){Operation=HardwareOperation.Preview,Payload=Convert.ToBase64String(ConfigCodec.Encode(config))});});return null;
            case "control":
                StudioBridge.Fields(a,"control");RequireConfiguration();var command=StudioBridge.Text(a,"control") switch{"host"=>HardwareControl.Host,"local"=>HardwareControl.Local,"off"=>HardwareControl.Off,"stop"=>HardwareControl.StopHelpers,_=>throw new InvalidDataException("Unknown control.")};
                await PerformHardware(()=>Task.FromResult<JobRequest?>(new(){Operation=HardwareOperation.Control,Control=command}));return null;
            case "importProfile":StudioBridge.Fields(a);EnsureIdle();Import();return null;
            case "exportProfile":StudioBridge.Fields(a);Export();return null;
            case "resetProfile":StudioBridge.Fields(a);EnsureIdle();if(MessageBox.Show(this,T("Reset the local profile?","Réinitialiser le profil local ?"),Text,MessageBoxButtons.YesNo)==DialogResult.Yes){var reset=KeyboardEditing.Clone(config);reset.Profiles[reset.Active]=new();AcceptEdit(reset);}return null;
            case "checkUpdates":StudioBridge.Fields(a);if(updates!=null)await updates.Refresh();return null;
            case "downloadUpdate":StudioBridge.Fields(a);if(updates!=null)await updates.DownloadSoftware();return null;
            case "installUpdate":StudioBridge.Fields(a);if(updates!=null)await updates.InstallSoftware();return null;
            case "updatePreferences":SetPreferences(a);return null;
            case "flash":
                OnboardProfile? flashEdits=null;await PerformHardware(()=>PrepareFlash(a,p=>flashEdits=p),afterSuccess:()=>{if(flashEdits!=null)AcceptEdit(KeyboardEditing.WithActiveProfile(config,flashEdits));});return null;
            case "cancelJob":case "stopEffect":StudioBridge.Fields(a);await StopEffectsOrCancel(explicitCancel:true);return null;
            case "startEffect":EnsureIdle();effect=ReadEffect(a);if(effectActive){if(jobId!=null)HardwareJobs.UpdateEffect(jobId,effect);return null;}await PerformHardware(()=>Task.FromResult<JobRequest?>(new(){Operation=HardwareOperation.Effects,EffectSettings=effect}),isEffect:true);return null;
            case "updateEffect":EnsureIdle();effect=ReadEffect(a);if(effectActive&&jobId!=null)HardwareJobs.UpdateEffect(jobId,effect);return null;
            case "openLink":
                StudioBridge.Fields(a,"link");string url=StudioBridge.Text(a,"link") switch{"github"=>"https://github.com/Gaerisson/redblade-aurora-openrgb","releases"=>"https://github.com/Gaerisson/redblade-aurora-openrgb/releases","guide"=>"https://github.com/Gaerisson/redblade-aurora-openrgb/blob/main/docs/SHORTCUTS"+(french?".fr":"")+".md",_=>throw new InvalidDataException("Unknown link.")};
                Process.Start(new ProcessStartInfo(url){UseShellExecute=true});return null;
        }
        throw new InvalidDataException("Unsupported action.");
    }
    async Task<JobRequest?> PrepareFlash(JsonElement a,Action<OnboardProfile> keepLocal)
    {
        StudioBridge.Fields(a,"edition","source");string edition=StudioBridge.Text(a,"edition"),source=StudioBridge.Text(a,"source");
        if(edition is not("full" or "core")||source is not("latest" or "bundled"))throw new InvalidDataException("Invalid firmware source.");
        if(dirty)
        {
            var answer=MessageBox.Show(this,T("Keep your local edits after firmware installation?\nYes: keep edits. No: load the restored keyboard settings. Cancel: do not install.","Conserver vos modifications locales après l’installation du firmware ?\nOui : conserver les modifications. Non : charger les réglages restaurés du clavier. Annuler : ne pas installer."),Text,MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
            if(answer==DialogResult.Cancel)return null;
            if(answer==DialogResult.Yes)keepLocal(KeyboardEditing.Clone(config).Profiles[config.Active]);
        }
        var request=new JobRequest{Operation=HardwareOperation.Flash,Edition=edition};
        if(source=="latest")
        {
            if(updates==null)throw new InvalidOperationException("Update service is unavailable.");
            if(!updates.Firmware.ContainsKey(edition))await updates.Refresh();
            if(!updates.Firmware.TryGetValue(edition,out var image))throw new IOException(T("Latest firmware could not be verified. Select the bundled firmware or retry.","Le dernier firmware n’a pas pu être vérifié. Choisissez la version incluse ou réessayez."));
            request.FirmwareBase64=Convert.ToBase64String(image.Bytes);request.FirmwareManifestBase64=Convert.ToBase64String(image.Manifest);request.FirmwareSha256=image.Sha256;
        }
        return request;
    }
    void RequireConfiguration()
    {
        if(preview)throw new InvalidOperationException("Hardware disabled in preview.");
        if(connection!="connected"||!generation.HasValue)throw new InvalidOperationException(T("Connect the keyboard and install Full 0.9 or later before applying settings.","Connectez le clavier et installez Full 0.9 ou ultérieur pour appliquer les réglages."));
        if(installedVersion<new Version(0,11,0)&&(Current.ReactiveStyle!=0||Current.ReactiveWidth!=0))throw new InvalidOperationException(T("Autonomous ripple and wave settings require Full 0.11.","Les ondes et vagues autonomes nécessitent Full 0.11."));
        if(installedVersion<new Version(0,10,0)&&((Current.Modules&0xC00)!=0||Current.SafetyTimeoutSeconds!=0||Current.CapsWordLayout!=0))throw new InvalidOperationException(T("These optional features require Full 0.10.","Ces options nécessitent Full 0.10."));
    }
    void Patch(JsonElement a)
    {
        StudioBridge.Fields(a,"modules","master","defaultMode","timeoutSeconds","turboRate","recorderSpeed","trailFrames","safetyTimeoutSeconds","capsWordLayout","reactiveStyle","reactiveWidth");
        var next=ProfileFile.Load(ProfileFile.Save(config));var p=next.Profiles[next.Active];
        foreach(var property in a.EnumerateObject())
        {
            int value=StudioBridge.Number(a,property.Name,0,4095);
            switch(property.Name){case "modules":p.Modules=(ushort)value;break;case "master":p.Master=checked((byte)value);break;case "defaultMode":p.DefaultMode=checked((byte)value);break;case "timeoutSeconds":p.TimeoutSeconds=checked((byte)value);break;case "turboRate":p.TurboRate=checked((byte)value);break;case "recorderSpeed":p.RecorderSpeed=checked((byte)value);break;case "trailFrames":p.TrailFrames=checked((byte)value);break;case "safetyTimeoutSeconds":p.SafetyTimeoutSeconds=checked((byte)value);break;case "capsWordLayout":p.CapsWordLayout=checked((byte)value);break;case "reactiveStyle":p.ReactiveStyle=checked((byte)value);break;case "reactiveWidth":p.ReactiveWidth=checked((byte)value);break;}
        }
        AcceptEdit(next);
    }
    static EffectSettings ReadEffect(JsonElement a)
    {
        StudioBridge.Fields(a,"preset","color1","color2","speed","brightness","angle","scale","reverse","palette","targetLeds","background","reactiveEnabled","reactiveStyle","reactiveColor","reactiveDurationMs","reactiveWidth","reactiveOpacity");
        var value=a.Deserialize<EffectSettings>(StudioBridge.Json)??throw new InvalidDataException("Missing effect settings.");value.Validate();return value;
    }
    void SetPreferences(JsonElement a)
    {
        StudioBridge.Fields(a,"checkAutomatically","downloadAutomatically","restartWhenIdle","includePreviews");if(updates==null)return;
        foreach(var property in a.EnumerateObject()){bool value=property.Value.GetBoolean();switch(property.Name){case "checkAutomatically":updates.Preferences.CheckAutomatically=value;break;case "downloadAutomatically":updates.Preferences.DownloadAutomatically=value;break;case "restartWhenIdle":updates.Preferences.RestartWhenIdle=value;break;case "includePreviews":updates.Preferences.IncludePreviews=value;break;}}
        if(!preview)updates.Preferences.Save();
    }
    void Import(){using var dialog=new OpenFileDialog{Filter="RedBlade+ (*.json)|*.json"};if(dialog.ShowDialog(this)==DialogResult.OK&&ConfirmReplacement()){var imported=ProfileFile.LoadFile(dialog.FileName);AcceptEdit(KeyboardEditing.WithActiveProfile(config,imported.Profiles[imported.Active]));}}
    void Export(){using var dialog=new SaveFileDialog{Filter="RedBlade+ (*.json)|*.json",FileName="redblade-profile.json",DefaultExt="json",AddExtension=true};if(dialog.ShowDialog(this)==DialogResult.OK)File.WriteAllBytes(dialog.FileName,ProfileFile.Save(config));}
    static string DraftPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RedBladePlusStudio","draft.json");
    void SaveDraft(){if(preview)return;try{Directory.CreateDirectory(Path.GetDirectoryName(DraftPath)!);File.WriteAllText(DraftPath,JsonSerializer.Serialize(new{dirty,data=Convert.ToBase64String(ProfileFile.Save(config))},StudioBridge.Json));}catch(Exception e)when(e is IOException or UnauthorizedAccessException){Diagnostics.Record(e,"Local draft");}}
    void LoadDraft(){try{using var doc=JsonDocument.Parse(BoundedFile.Read(DraftPath,200000));config=ProfileFile.Load(Convert.FromBase64String(doc.RootElement.GetProperty("data").GetString()!));dirty=doc.RootElement.GetProperty("dirty").GetBoolean();}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or KeyNotFoundException or FormatException){}}
}
