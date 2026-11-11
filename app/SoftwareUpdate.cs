// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;

namespace RedBladePlus.Studio;

internal sealed record SoftwareUpdateResult(string InstalledPath, string BackupPath, string RunningPath);
internal static class SoftwareUpdate
{
    const string ExeName = "RedBladePlus-Studio.exe";
    static int launching;
    static bool Elevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
    static string Hash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    static void CheckHash(string path, string expected)
    {
        if (expected.Length != 64 || !expected.All(Uri.IsHexDigit) || !Hash(path).Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Downloaded executable SHA-256 mismatch.");
    }
    static Version ExecutableVersion(string path)
    {
        var info = FileVersionInfo.GetVersionInfo(path);
        if (info.FileMajorPart < 0 || info.FileMinorPart < 0 || info.FileBuildPart < 0) throw new InvalidDataException("Executable version unavailable.");
        return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
    }
    static string EventName(int pid, string sha) => @"Local\RedBladePlus.Update." + pid + "." + sha.ToUpperInvariant();
    internal static void ValidateName(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid portable executable path.");
    }
    internal static void ValidateCachePath(string path)
    {
        ValidateName(path);
        if (!Path.GetFileName(path).Equals(ExeName, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid update executable name.");
        string root = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RedBladePlus", "updates")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update executable is outside the update cache.");
    }
    internal static void Launch(PreparedSoftware prepared)
    {
        if (Interlocked.Exchange(ref launching, 1) != 0) throw new InvalidOperationException("An update launch is already in progress.");
        try
        {
            if (Elevated()) throw new InvalidOperationException("Software updates must run as a normal user.");
            string current = Environment.ProcessPath ?? throw new InvalidOperationException("Current executable path unavailable.");
            ValidateName(current); ValidateCachePath(prepared.Executable);
            if (!Path.GetFullPath(current).Equals(Path.GetFullPath(prepared.CurrentExecutable), StringComparison.OrdinalIgnoreCase) || Path.GetFullPath(current).Equals(Path.GetFullPath(prepared.Executable), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update source or destination is invalid.");
            CheckHash(prepared.Executable, prepared.Sha256);
            if (ExecutableVersion(prepared.Executable) != prepared.Version || prepared.Version <= ExecutableVersion(current)) throw new InvalidDataException("Update executable version is not newer than the running version.");
            int pid = Environment.ProcessId;
            using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, EventName(pid, prepared.Sha256), out bool created);
            if (!created) throw new InvalidOperationException("An update handshake already exists.");
            var start = BuildStartInfo(prepared.Executable, current, pid, prepared.Sha256);
            StartAndConfirm(start, ready, s => Process.Start(s), TimeSpan.FromSeconds(10));
        }
        catch { Volatile.Write(ref launching, 0); throw; }
    }
    internal static void StartAndConfirm(ProcessStartInfo start, WaitHandle ready, Func<ProcessStartInfo, IDisposable?> launch, TimeSpan timeout)
    {
        using var child = launch(start) ?? throw new IOException("Could not start the verified update.");
        if (!ready.WaitOne(timeout)) throw new IOException("The update process did not confirm its identity. The current application remains open.");
    }
    internal static ProcessStartInfo BuildStartInfo(string executable, string current, int pid, string sha)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! };
        start.ArgumentList.Add("--finish-update"); start.ArgumentList.Add(current); start.ArgumentList.Add(pid.ToString(System.Globalization.CultureInfo.InvariantCulture)); start.ArgumentList.Add(sha);
        return start;
    }
    internal static ProcessStartInfo InstalledStartInfo(SoftwareUpdateResult result)=>new(result.InstalledPath){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(result.InstalledPath)!};
    internal static SoftwareUpdateResult Finish(string originalPath, int oldPid, string sha)
    {
        if (Elevated()) throw new InvalidOperationException("Software updates must run as a normal user.");
        string self = Environment.ProcessPath ?? throw new InvalidOperationException("Update executable path unavailable.");
        ValidateCachePath(self); ValidateName(originalPath); CheckHash(self, sha);
        if (oldPid <= 0 || oldPid == Environment.ProcessId) throw new InvalidDataException("Invalid previous process.");
        using var previous = Process.GetProcessById(oldPid);
        string previousPath = previous.MainModule?.FileName ?? throw new IOException("Cannot verify previous process path.");
        if (!Path.GetFullPath(previousPath).Equals(Path.GetFullPath(originalPath), StringComparison.OrdinalIgnoreCase) || Path.GetFullPath(self).Equals(Path.GetFullPath(originalPath), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Previous process does not match the portable executable.");
        if (ExecutableVersion(self) <= ExecutableVersion(originalPath)) throw new InvalidDataException("The update is not newer than the portable executable.");
        using var ready = EventWaitHandle.OpenExisting(EventName(oldPid, sha));
        ready.Set();
        if (!previous.WaitForExit(30000)) throw new TimeoutException("The previous application did not exit within 30 seconds. The verified update remains at " + self);
        string backup = ReplaceVerified(self, originalPath, sha);
        return new(originalPath, backup, self);
    }
    // Internal seam used by hardware-free tests; caller validates process identity and version first.
    internal static string ReplaceVerified(string source, string destination, string sha)
    {
        CheckHash(source, sha);
        string directory = Path.GetDirectoryName(destination) ?? throw new InvalidDataException("Destination has no directory.");
        string temporary = Path.Combine(directory, ".redblade-update-" + Guid.NewGuid().ToString("N") + ".tmp");
        string backup = destination + ".bak";
        if (File.Exists(backup)) backup = destination + "." + Guid.NewGuid().ToString("N") + ".bak";
        try
        {
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { input.CopyTo(output); output.Flush(true); }
            CheckHash(temporary, sha);
            File.Replace(temporary, destination, backup, false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new IOException("Could not replace the portable application. The verified update remains at " + source + ". " + ex.Message, ex); }
        finally { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        return backup;
    }
}
