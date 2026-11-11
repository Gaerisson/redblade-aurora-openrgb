namespace RedBladePlus.Studio;
internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--self-test" })) return SelfTests.Run().GetAwaiter().GetResult();
        if(args.SequenceEqual(new[]{"--diagnose-rgb"}))return HardwareDiagnostics.Run();
        if (args.Length == 3 && args[0] == "--worker") return HardwareJobs.RunWorker(args[1],args[2]).GetAwaiter().GetResult();
        if(args.Length==4&&args[0]=="--finish-update")
        {
            try
            {
                if(!int.TryParse(args[2],System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int previous))return 2;
                var installed=SoftwareUpdate.Finish(args[1],previous,args[3]);
                using var restarted=System.Diagnostics.Process.Start(SoftwareUpdate.InstalledStartInfo(installed));
                return restarted==null?1:0;
            }
            catch(Exception error){Diagnostics.UiError(error);return 1;}
        }
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException+=(_,e)=>{if(e.ExceptionObject is Exception error)Diagnostics.Record(error,"Fatal background exception");};
        ApplicationConfiguration.Initialize();
        if(args.SequenceEqual(new[]{"--ui-self-test"}))return UiSelfTest();
        if (args.Length is >=2 and <=4 && args[0] == "--preview")
        {
            using var form=new StudioForm(preview:true);Exception? failure=null;
            form.ShowPreviewPage(args.Length>=3?args[2]:"Keyboard",args.Length==4&&args[3]=="fr");
            form.Shown+=async(_,_)=>{try{await form.CapturePreviewToFile(args[1]);}catch(Exception error){failure=error;Diagnostics.Record(error,"UI preview");}finally{form.Close();}};
            Application.Run(form);return failure==null?0:1;
        }
        if (args.Length != 0) return 2;
        Application.ThreadException+=(_,e)=>Diagnostics.UiError(e.Exception);
        try
        {
            using var form=new StudioForm(preview:false);
            using var updates=new UpdateSession(form);
            form.AttachUpdates(updates);
            form.Shown+=async(_,_)=>await updates.Start();
            Application.Run(form);
        }
        catch(Exception error){Diagnostics.UiError(error);return 1;}
        return 0;
    }
    static int UiSelfTest()
    {
        Exception? failure=null;
        using var form=new StudioForm(preview:true);
        ThreadExceptionEventHandler handler=(_,e)=>{failure=e.Exception;form.BeginInvoke((Action)(()=>form.Close()));};
        Application.ThreadException+=handler;
        form.Shown+=async(_,_)=>
        {
            try
            {
                await form.RunUiTests();
                bool original=form.Enabled;
                using(var interaction=new UpdateInteractionLock(form))
                {
                    if(form.Enabled)throw new Exception("Update handshake left editing enabled.");
                    form.Close();
                    if(form.IsDisposed)throw new Exception("Update handshake allowed early closure.");
                }
                if(form.Enabled!=original)throw new Exception("Failed handoff did not restore editing.");
            }
            catch(Exception error){failure=error;}
            finally{form.Close();}
        };
        try{Application.Run(form);}finally{Application.ThreadException-=handler;}
        string report=failure==null?"PASS: actual WebView2 navigation, 109 keys, mouse drag/marquee, gradient undo/redo, hex/swatch focus, reaction controls, advanced effect bridge, visible EN/FR shortcut maps with persistent zoom, modal wheel/pan/Escape and selected intensity undo, and updater guard. No HID, UAC, service or external network operations.":failure.ToString();
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"ui-test-results.txt"),report+Environment.NewLine);
        return failure==null?0:1;
    }
}
