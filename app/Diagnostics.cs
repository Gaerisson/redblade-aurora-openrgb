// SPDX-License-Identifier: GPL-2.0-or-later
using System.Reflection;
namespace RedBladePlus.Studio;

internal static class Diagnostics
{
    internal static string DirectoryPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RedBladePlusStudio","Logs");
    internal static string Record(Exception error,string operation)
    {
        string path=Path.Combine(DirectoryPath,"studio-"+DateTime.UtcNow.ToString("yyyyMMdd")+".log");
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            // Do not serialize hardware requests, captured arguments, keymaps or recordings.
            string report=$"{DateTime.UtcNow:O} · {operation} · {Assembly.GetExecutingAssembly().GetName().Version}\n{error}\n\n";
            if(File.Exists(path)&&new FileInfo(path).Length>1024*1024)path=Path.Combine(DirectoryPath,"studio-"+Guid.NewGuid().ToString("N")+".log");
            File.AppendAllText(path,report);
        }
        catch{ /* Diagnostic storage must never cause another UI failure. */ }
        return path;
    }
    internal static void UiError(Exception error)
    {
        string path=Record(error,"UI event");
        MessageBox.Show("Studio a rencontré une erreur. Le rapport a été enregistré ici :\n"+path+"\n\nUne opération de flash déjà démarrée continue dans son processus séparé. Gardez le clavier branché.","RedBlade+ Studio",MessageBoxButtons.OK,MessageBoxIcon.Error);
    }
}
