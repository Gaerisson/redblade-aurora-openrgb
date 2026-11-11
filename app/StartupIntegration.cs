// SPDX-License-Identifier: GPL-2.0-or-later
using Microsoft.Win32;
namespace RedBladePlus.Studio;

internal static class StartupIntegration
{
    const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run",Name="RedBladePlusStudio";
    internal static string Command(string executable)
    {
        if(!Path.IsPathFullyQualified(executable)||!executable.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||executable.Contains('"')||executable.Any(char.IsControl))throw new InvalidDataException("Invalid Studio startup path.");
        return "\""+executable+"\"";
    }
    internal static bool Enabled
    {
        get
        {
            try{using var key=Registry.CurrentUser.OpenSubKey(Key);return string.Equals(key?.GetValue(Name) as string,Command(Environment.ProcessPath!),StringComparison.OrdinalIgnoreCase);}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){return false;}
        }
    }
    internal static void Set(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(Key,true)??throw new IOException("Cannot open the Windows startup settings.");
        if(enabled)key.SetValue(Name,Command(Environment.ProcessPath??throw new IOException("Cannot locate Studio executable.")),RegistryValueKind.String);
        else key.DeleteValue(Name,false);
    }
}
