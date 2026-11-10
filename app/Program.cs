namespace RedBladePlus.Studio;
internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--self-test" })) return SelfTests.Run().GetAwaiter().GetResult();
        if (args.Length == 3 && args[0] == "--worker") return HardwareJobs.RunWorker(args[1],args[2]).GetAwaiter().GetResult();
        ApplicationConfiguration.Initialize();
        if (args.Length is >=2 and <=4 && args[0] == "--preview")
        {
            using var form = new StudioForm(preview: true);
            form.Show();if(args.Length>=3)form.ShowPreviewPage(args[2],args.Length==4&&args[3]=="fr");Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.GetFullPath(args[1]), System.Drawing.Imaging.ImageFormat.Png);
            return 0;
        }
        if (args.Length != 0) return 2;
        Application.Run(new StudioForm(preview: false));
        return 0;
    }
}
