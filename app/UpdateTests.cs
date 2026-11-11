using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RedBladePlus.Studio;

internal static class UpdateTests
{
    sealed class Fake : HttpMessageHandler
    {
        internal readonly Dictionary<string, byte[]> Files = new();
        internal string Api = "[]";
        internal HttpStatusCode Status = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            string url = request.RequestUri!.ToString();
            if (url.StartsWith("https://api.github.com/", StringComparison.Ordinal)) return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Api) });
            return Task.FromResult(Files.TryGetValue(url, out var bytes) ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) } : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception("Updater test: " + message); }
    static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    static string Url(string tag, string name) => "https://github.com/Gaerisson/redblade-aurora-openrgb/releases/download/" + tag + "/" + name;
    static object Asset(string tag, string name, byte[] bytes) => new { name, browser_download_url = Url(tag, name), size = bytes.Length, digest = "sha256:" + Hash(bytes) };
    internal static async Task Run()
    {
        var fake = new Fake();
        byte[] bin = new byte[65536]; bin[^4] = 0x55; bin[^3] = 0x55; bin[^2] = 0xaa; bin[^1] = 0xaa;
        byte[] manifest = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { release = "v0.9.0", edition = "full", board = "Designed by GG Redblade FS ISO", mcu = "SN32F248B", usb_vid = "320f", usb_pid = "5136", led_count = 109, binary_bytes = 65536, sha256 = Hash(bin) }));
        const string tag = "v0.10.0", binName = "redblade-fs-aurora-via.bin", manifestName = "manifest.json";
        byte[] sums = Encoding.UTF8.GetBytes(Hash(bin) + "  " + binName + "\n" + Hash(manifest) + "  " + manifestName + "\n");
        fake.Files[Url(tag, binName)] = bin; fake.Files[Url(tag, manifestName)] = manifest; fake.Files[Url(tag, "SHA256SUMS")] = sums;
        fake.Api = JsonSerializer.Serialize(new object[] { new { draft = false, prerelease = true, tag_name = tag, assets = new[] { Asset(tag, binName, bin), Asset(tag, manifestName, manifest), Asset(tag, "SHA256SUMS", sums) } }, new { draft = false, prerelease = false, tag_name = "v0.8.0", assets = Array.Empty<object>() } });
        using var service = new UpdateService(fake);
        var preview = await service.CheckAsync(UpdateChannel.Preview, new Version(0, 10, 0));
        Check(preview.Offer?.Tag == tag && !preview.SoftwareNewer, "current software keeps firmware release");
        Check((await service.CheckAsync(UpdateChannel.Stable, new Version(0, 7, 0))).Offer?.Tag == "v0.8.0", "stable channel");
        var downloaded = await service.DownloadFirmwareAsync(preview.Offer!, "full");
        Check(downloaded.Version == new Version(0, 9, 0) && downloaded.Bytes.Length == 65536, "firmware version independent of software");
        var assets = new Dictionary<string, UpdateAsset>(preview.Offer!.Assets);
        var original = assets[binName];
        assets[binName] = original with { Url = new Uri("https://example.com/payload") };
        try { await service.DownloadFirmwareAsync(preview.Offer with { Assets = assets }, "full"); throw new Exception("wrong origin accepted"); } catch (InvalidDataException) { }
        assets[binName] = original with { Sha256 = new string('0', 64) };
        try { await service.DownloadFirmwareAsync(preview.Offer with { Assets = assets }, "full"); throw new Exception("wrong digest accepted"); } catch (InvalidDataException) { }
        assets[binName] = original with { Size = 65537 };
        try { await service.DownloadFirmwareAsync(preview.Offer with { Assets = assets }, "full"); throw new Exception("oversize accepted"); } catch (InvalidDataException) { }
        fake.Files[Url(tag, binName)] = new byte[1];
        try { await service.DownloadFirmwareAsync(preview.Offer!, "full"); throw new Exception("truncated payload accepted"); } catch (InvalidDataException) { }
        fake.Files[Url(tag, binName)] = bin; fake.Files[Url(tag, "SHA256SUMS")] = Encoding.UTF8.GetBytes("tampered");
        try { await service.DownloadFirmwareAsync(preview.Offer!, "full"); throw new Exception("tampered sums accepted"); } catch (InvalidDataException) { }
        async Task RejectFirmware(byte[] candidate, string board, string release)
        {
            byte[] meta = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { release, edition = "full", board, mcu = "SN32F248B", usb_vid = "320f", usb_pid = "5136", led_count = 109, binary_bytes = 65536, sha256 = Hash(candidate) }));
            byte[] checksums = Encoding.UTF8.GetBytes(Hash(candidate) + "  " + binName + "\n" + Hash(meta) + "  " + manifestName + "\n");
            fake.Files[Url(tag, binName)] = candidate; fake.Files[Url(tag, manifestName)] = meta; fake.Files[Url(tag, "SHA256SUMS")] = checksums;
            var a = new Dictionary<string, UpdateAsset>();
            foreach (var (name, content) in new[] { (binName, candidate), (manifestName, meta), ("SHA256SUMS", checksums) }) a[name] = new(name, new Uri(Url(tag, name)), Hash(content), content.Length);
            try { await service.DownloadFirmwareAsync(preview.Offer! with { Assets = a }, "full"); throw new Exception("invalid firmware accepted"); } catch (InvalidDataException) { }
        }
        await RejectFirmware(bin, "wrong board", "v0.9.0");
        await RejectFirmware(bin, "Designed by GG Redblade FS ISO", "v99.0.0");
        byte[] badFlag = (byte[])bin.Clone(); badFlag[^1] = 0;
        await RejectFirmware(badFlag, "Designed by GG Redblade FS ISO", "v0.9.0");
        using var errorService = new UpdateService(new Fake { Status = HttpStatusCode.ServiceUnavailable });
        Check((await errorService.CheckAsync(UpdateChannel.Preview, new Version(0, 0, 0))).Offer is null, "API error nonfatal");
        using var cts = new CancellationTokenSource(); cts.Cancel();
        try { await errorService.CheckAsync(UpdateChannel.Stable, new Version(0, 0, 0), cts.Token); throw new Exception("cancellation ignored"); } catch (OperationCanceledException) { }
    }
}
