// SPDX-License-Identifier: GPL-2.0-or-later
using System.Security.Cryptography;
using System.Text;

namespace RedBladePlus.Studio;
internal static class SoftwareUpdateTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception("Software update test: " + message); }
    internal static void Run()
    {
        string dir = Path.Combine(Path.GetTempPath(), "redblade-update-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            SoftwareUpdate.ValidateName(Path.Combine(dir, "RedBladePlus-Studio (1).exe"));
            foreach (string invalid in new[] { "relative.exe", Path.Combine(dir, "wrong.txt") })
            { try { SoftwareUpdate.ValidateName(invalid); throw new Exception("invalid path accepted"); } catch (InvalidDataException) { } }
            foreach (string invalid in new[] { Path.Combine(dir, "RedBladePlus-Studio.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RedBladePlus", "updates", "test", "wrong.exe") })
            { try { SoftwareUpdate.ValidateCachePath(invalid); throw new Exception("invalid cache accepted"); } catch (InvalidDataException) { } }
            string source = Path.Combine(dir, "download.exe"), target = Path.Combine(dir, "installed.exe");
            byte[] old = Encoding.UTF8.GetBytes("previous executable"), next = Encoding.UTF8.GetBytes("verified update");
            File.WriteAllBytes(source, next); File.WriteAllBytes(target, old);
            string hash = Convert.ToHexString(SHA256.HashData(next));
            var start = SoftwareUpdate.BuildStartInfo(source, target, 1234, hash);
            Check(!start.UseShellExecute && start.FileName == source && start.ArgumentList.SequenceEqual(new[] { "--finish-update", target, "1234", hash }), "safe launch arguments");
            var installedStart=SoftwareUpdate.InstalledStartInfo(new SoftwareUpdateResult(target,target+".bak",source));
            Check(installedStart.FileName==target&&!installedStart.UseShellExecute,"restart installed portable rather than cache so later updates retain the original destination");
            bool launched = false;
            using (var ready = new ManualResetEvent(true)) SoftwareUpdate.StartAndConfirm(start, ready, info => { launched = ReferenceEquals(info, start); return new MemoryStream(); }, TimeSpan.FromSeconds(1));
            Check(launched, "launch callback and handshake");
            using (var unready = new ManualResetEvent(false))
            {
                try { SoftwareUpdate.StartAndConfirm(start, unready, _ => new MemoryStream(), TimeSpan.Zero); throw new Exception("missing handshake accepted"); } catch (IOException) { }
            }
            try { SoftwareUpdate.ReplaceVerified(source, target, new string('0', 64)); throw new Exception("wrong hash accepted"); } catch (InvalidDataException) { }
            Check(File.ReadAllBytes(target).SequenceEqual(old) && !File.Exists(target + ".bak"), "hash failure leaves original intact");
            using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { SoftwareUpdate.ReplaceVerified(source, target, hash); throw new Exception("locked target replaced"); } catch (IOException) { }
            }
            Check(File.ReadAllBytes(target).SequenceEqual(old), "locked target preserved");
            SoftwareUpdate.ReplaceVerified(source, target, hash);
            Check(File.ReadAllBytes(target).SequenceEqual(next) && File.ReadAllBytes(target + ".bak").SequenceEqual(old), "atomic replacement and rollback");
            File.WriteAllBytes(source, old); SoftwareUpdate.ReplaceVerified(source, target, Convert.ToHexString(SHA256.HashData(old)));
            Check(File.ReadAllBytes(target + ".bak").SequenceEqual(old) && Directory.GetFiles(dir, "installed.exe.*.bak").Length == 1, "existing backup retained across updates");
        }
        finally { Directory.Delete(dir, true); }
    }
}
