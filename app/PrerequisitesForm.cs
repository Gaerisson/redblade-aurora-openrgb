// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace RedBladePlus.Studio;

// WinForms only: this window also works before the WebView2 runtime exists.
internal sealed class PrerequisitesForm : Form
{
    const string VcHash="cc0ff0eb1dc3f5188ae6300faef32bf5beeba4bdd6e8e445a9184072096b713b";
    const int VcSize=25635768;
    const string VcUrl="https://download.visualstudio.microsoft.com/download/pr/e9604e21-4b79-4096-a4b3-1b63d4b55b4d/CC0FF0EB1DC3F5188AE6300FAEF32BF5BEEBA4BDD6E8E445A9184072096B713B/VC_redist.x64.exe";
    static readonly Version MinimumVc=new(14,44,35211,0);
    static readonly Color Background=Color.FromArgb(14,15,19), Card=Color.FromArgb(22,23,28), Accent=Color.FromArgb(221,0,43), Muted=Color.FromArgb(185,186,195);
    readonly bool french;
    readonly Label runtime=new(){AutoSize=true},visualCpp=new(){AutoSize=true},resources=new(){AutoSize=true,MaximumSize=new Size(560,0)},message=new(){AutoSize=true,MaximumSize=new Size(560,0)};
    readonly TextBox details=new(){Multiline=true,ReadOnly=true,Visible=false,Width=550,Height=65,ScrollBars=ScrollBars.Vertical};
    readonly Button install=new(){AutoSize=true},installVc=new(){AutoSize=true},refresh=new(){AutoSize=true},close=new(){AutoSize=true},showDetails=new(){AutoSize=true,Visible=false};
    readonly ProgressBar progress=new(){Width=540,Height=8,Visible=false,Style=ProgressBarStyle.Marquee};
    bool installing;
    string T(string en,string fr)=>french?fr:en;
    internal static bool RuntimeAvailable()
    {
        try{return !string.IsNullOrWhiteSpace(CoreWebView2Environment.GetAvailableBrowserVersionString());}
        catch(WebView2RuntimeNotFoundException){return false;}
    }
    internal static bool VisualCppAvailable()
    {
        try
        {
            using var baseKey=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
            using var key=baseKey.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
            return key?.GetValue("Installed") is int installed&&installed==1&&Version.TryParse((key.GetValue("Version") as string)?.TrimStart('v','V'),out var version)&&version>=MinimumVc;
        }
        catch(Exception e) when(e is UnauthorizedAccessException or System.Security.SecurityException or IOException){return false;}
    }
    internal static bool ComponentsReadyForOpenRgb()=>VisualCppAvailable();
    static void StyleButton(Button button,bool primary=false)
    {
        button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=primary?0:1;button.FlatAppearance.BorderColor=Color.FromArgb(75,76,85);
        button.BackColor=primary?Accent:Card;button.ForeColor=Color.White;button.Padding=new Padding(12,6,12,6);button.Margin=new Padding(0,8,10,0);button.Cursor=Cursors.Hand;
    }
    FlowLayoutPanel MakeCard(params Control[] children)
    {
        var card=new FlowLayoutPanel{AutoSize=true,Width=580,MinimumSize=new Size(580,0),BackColor=Card,Padding=new Padding(14),Margin=new Padding(0,0,0,10),FlowDirection=FlowDirection.TopDown,WrapContents=false};
        foreach(var child in children)card.Controls.Add(child);return card;
    }
    internal PrerequisitesForm(bool? useFrench=null)
    {
        french=useFrench??CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("fr",StringComparison.OrdinalIgnoreCase);
        Text=T("Studio prerequisites","Prérequis de Studio");StartPosition=FormStartPosition.CenterParent;BackColor=Background;ForeColor=Color.White;
        Font=new Font("Segoe UI",9F);MinimumSize=new Size(650,540);Size=new Size(690,690);MaximizeBox=false;MinimizeBox=false;
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,BackColor=Background};
        var title=new Label{Text=Text,Font=new Font("Segoe UI",16,FontStyle.Bold),AutoSize=true,Margin=new Padding(0,0,0,8)};
        var intro=new Label{Text=T("Check the components Studio needs. Installation requires an internet connection.","Vérifiez les composants nécessaires à Studio. Leur installation nécessite une connexion Internet."),ForeColor=Muted,AutoSize=true,MaximumSize=new Size(560,0),Margin=new Padding(0,0,0,15)};
        var openRgb=new Label{Text=T("OpenRGB: downloaded and verified automatically when needed.","OpenRGB : téléchargé et vérifié automatiquement si nécessaire."),ForeColor=Muted,AutoSize=true,MaximumSize=new Size(550,0),Margin=new Padding(0,8,0,0)};
        install.Text=T("Install WebView2","Installer WebView2");installVc.Text=T("Install Visual C++","Installer Visual C++");refresh.Text=T("Check again","Vérifier à nouveau");close.Text=T("Close","Fermer");showDetails.Text=T("Technical details","Détails techniques");
        foreach(var b in new[]{install,installVc})StyleButton(b,true);foreach(var b in new[]{refresh,close,showDetails})StyleButton(b);
        details.BackColor=Background;details.ForeColor=Muted;details.BorderStyle=BorderStyle.FixedSingle;
        var buttons=new FlowLayoutPanel{AutoSize=true,WrapContents=false,BackColor=Background};buttons.Controls.AddRange(new Control[]{refresh,close,showDetails});
        panel.Controls.AddRange(new Control[]{title,intro,MakeCard(runtime,install),MakeCard(visualCpp,installVc,openRgb),MakeCard(resources),message,progress,buttons,details});Controls.Add(panel);
        install.Click+=async(_,_)=>await InstallRuntime();installVc.Click+=async(_,_)=>await InstallVisualCpp();refresh.Click+=(_,_)=>RefreshChecks();close.Click+=(_,_)=>Close();showDetails.Click+=(_,_)=>details.Visible=!details.Visible;
        FormClosing+=(_,e)=>{if(installing)e.Cancel=true;};Shown+=(_,_)=>RefreshChecks();
    }
    void Error(string friendly,Exception? error=null)
    {
        message.Text=friendly;
        if(error!=null){Diagnostics.Record(error,"Prerequisites");details.Text=error.ToString();showDetails.Visible=true;}
    }
    void RefreshChecks()
    {
        try{bool ready=RuntimeAvailable();runtime.Text=T("WebView2: ","WebView2 : ")+(ready?T("ready","prêt"):T("required for Studio","nécessaire pour Studio"));install.Enabled=!ready;}
        catch(Exception e){runtime.Text=T("WebView2: check unavailable","WebView2 : vérification impossible");install.Enabled=false;Error(T("Could not check WebView2.","Impossible de vérifier WebView2."),e);}
        bool vc=VisualCppAvailable();visualCpp.Text=T("Visual C++ x64: ","Visual C++ x64 : ")+(vc?T("ready","prêt"):T("required for OpenRGB","nécessaire pour OpenRGB"));installVc.Enabled=!vc;
        try
        {
            FirmwareImage.ValidateHelpers();FirmwareImage.Load("full");FirmwareImage.Load("core");
            if(KeyboardLayout.Load().Length!=109)throw new InvalidDataException("Invalid keyboard layout.");
            resources.Text=T("Bundled firmware, flasher and keyboard layout: verified","Firmware, outil de flash et disposition du clavier inclus : vérifiés");
        }
        catch(Exception e){resources.Text=T("Bundled component verification failed.","Échec de vérification des composants inclus.");Error(T("Some bundled components could not be verified.","Certains composants inclus n’ont pas pu être vérifiés."),e);}
    }
    void SetBusy(bool busy,string text)
    {
        installing=busy;install.Enabled=installVc.Enabled=refresh.Enabled=close.Enabled=!busy;progress.Visible=busy;message.Text=text;
        if(!busy)RefreshChecks();
    }
    static string TemporaryDirectory()
    {
        string path=Path.Combine(Path.GetTempPath(),"RedBlade-Prerequisite-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked installer directory refused.");return path;
    }
    static void Cleanup(string? directory,bool safe)
    {
        if(directory!=null&&safe)try{Directory.Delete(directory,true);}catch(IOException){}catch(UnauthorizedAccessException){}
    }
    async Task InstallRuntime()
    {
        SetBusy(true,T("Installing WebView2. Please wait…","Installation de WebView2. Veuillez patienter…"));
        string? directory=null;bool finished=false,started=false;
        try
        {
            byte[] bytes=Embedded.Read("webview2-bootstrapper.exe");string expected=Encoding.ASCII.GetString(Embedded.Read("webview2-bootstrapper.sha256")).Trim();
            if(expected.Length!=64||!expected.All(Uri.IsHexDigit)||!Convert.ToHexString(SHA256.HashData(bytes)).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("WebView2 bootstrapper SHA-256 mismatch.");
            directory=TemporaryDirectory();string path=Path.Combine(directory,"MicrosoftEdgeWebview2Setup.exe");using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))await output.WriteAsync(bytes);
            var start=new ProcessStartInfo(path){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=directory};start.ArgumentList.Add("/silent");start.ArgumentList.Add("/install");
            using var process=Process.Start(start)??throw new IOException("Could not start WebView2 installer.");started=true;var wait=process.WaitForExitAsync();
            if(await Task.WhenAny(wait,Task.Delay(TimeSpan.FromMinutes(5)))!=wait){message.Text=T("The installer is still running. Check again later.","L’installation est toujours en cours. Vérifiez à nouveau plus tard.");return;}
            await wait;finished=true;
            if(RuntimeAvailable())message.Text=T("WebView2 is ready.","WebView2 est prêt.");
            else Error(T("WebView2 is still unavailable. Check your connection and retry.","WebView2 reste indisponible. Vérifiez votre connexion et réessayez."),new IOException("WebView2 installer exit code: "+process.ExitCode));
        }
        catch(Exception e){Error(T("Installation failed. Check your connection and retry.","Échec de l’installation. Vérifiez votre connexion et réessayez."),e);}
        finally{Cleanup(directory,!started||finished);SetBusy(false,message.Text);}
    }
    async Task InstallVisualCpp()
    {
        if(VisualCppAvailable()){RefreshChecks();return;}
        SetBusy(true,T("Downloading Visual C++ from Microsoft…","Téléchargement de Visual C++ depuis Microsoft…"));
        string? directory=null;bool finished=false,started=false;
        try
        {
            directory=TemporaryDirectory();string path=Path.Combine(directory,"vc_redist.x64.exe");
            using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var handler=new HttpClientHandler{AllowAutoRedirect=false};using var client=new HttpClient(handler){Timeout=TimeSpan.FromMinutes(5)};
            using var response=await client.GetAsync(VcUrl,HttpCompletionOption.ResponseHeadersRead,timeout.Token);response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength is long length&&length!=VcSize)throw new InvalidDataException("Visual C++ download size mismatch.");
            using(var source=await response.Content.ReadAsStreamAsync(timeout.Token))using(var target=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                var buffer=new byte[81920];int n,total=0;while((n=await source.ReadAsync(buffer,timeout.Token))>0){total+=n;if(total>VcSize)throw new InvalidDataException("Visual C++ download exceeds expected size.");await target.WriteAsync(buffer.AsMemory(0,n),timeout.Token);}
                if(total!=VcSize)throw new InvalidDataException("Incomplete Visual C++ download.");
            }
            using var verifiedFile=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(!Convert.ToHexString(SHA256.HashData(verifiedFile)).Equals(VcHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Visual C++ download SHA-256 mismatch.");
            message.Text=T("Starting Microsoft’s installer. Windows may ask for permission.","Démarrage de l’installateur Microsoft. Windows peut demander une autorisation.");
            var start=new ProcessStartInfo(path){UseShellExecute=true,WorkingDirectory=directory};start.ArgumentList.Add("/install");start.ArgumentList.Add("/passive");start.ArgumentList.Add("/norestart");
            using var process=Process.Start(start)??throw new IOException("Could not start Visual C++ installer.");started=true;var wait=process.WaitForExitAsync();
            if(await Task.WhenAny(wait,Task.Delay(TimeSpan.FromMinutes(10)))!=wait){message.Text=T("The installer is still running. Check again later.","L’installation est toujours en cours. Vérifiez à nouveau plus tard.");return;}
            await wait;finished=true;
            if(VisualCppAvailable())message.Text=T("Visual C++ is ready.","Visual C++ est prêt.");
            else Error(T("Visual C++ is still unavailable. Restart Windows or retry.","Visual C++ reste indisponible. Redémarrez Windows ou réessayez."),new IOException("Visual C++ installer exit code: "+process.ExitCode));
        }
        catch(Exception e){Error(T("Installation failed. Check your connection and retry.","Échec de l’installation. Vérifiez votre connexion et réessayez."),e);}
        finally{Cleanup(directory,!started||finished);SetBusy(false,message.Text);}
    }
}
