using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RedBladePlus.Studio;

internal enum UpdateChannel { Stable, Preview }
internal sealed record UpdateAsset(string Name, Uri Url, string Sha256, long Size);
internal sealed record UpdateOffer(string Tag, Version Version, bool Prerelease, IReadOnlyDictionary<string, UpdateAsset> Assets);
internal sealed record UpdateCheck(UpdateOffer? Offer, string Diagnostic, DateTimeOffset CheckedAt, bool SoftwareNewer = false);
internal sealed record DownloadedFirmware(byte[] Bytes, byte[] Manifest, string Edition, Version Version, string Sha256);
internal sealed record PreparedSoftware(string Executable, string Sha256, string CurrentExecutable, Version Version);

// Release metadata and hashes are supplied by GitHub over HTTPS. This is integrity checking,
// not an independent signature: compromise of the repository/release account remains in scope.
internal sealed class UpdateService : IDisposable
{
    const string Api = "https://api.github.com/repos/Gaerisson/redblade-aurora-openrgb/releases?per_page=30";
    const string ReleasePrefix = "/Gaerisson/redblade-aurora-openrgb/releases/download/";
    const int MaxMetadata = 2 * 1024 * 1024, MaxSoftware = 200 * 1024 * 1024, MaxManifest = 128 * 1024;
    readonly HttpClient http;
    UpdateCheck? cached; UpdateChannel cachedChannel; Version? cachedVersion;
    internal UpdateService(HttpMessageHandler? handler = null)
    {
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }, true) { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RedBladePlus-Studio-Updater/1.0");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }
    internal async Task<UpdateCheck> CheckAsync(UpdateChannel channel, Version currentVersion, CancellationToken cancel = default, bool force = false)
    {
        if (!force && cached is not null && cachedChannel == channel && cachedVersion == currentVersion && DateTimeOffset.UtcNow - cached.CheckedAt < TimeSpan.FromHours(6)) return cached;
        try
        {
            using var response = await http.GetAsync(Api, HttpCompletionOption.ResponseHeadersRead, cancel);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await ReadBounded(response, MaxMetadata, cancel));
            UpdateOffer? best = null;
            foreach (var release in json.RootElement.EnumerateArray())
            {
                if (release.GetProperty("draft").GetBoolean() || (channel == UpdateChannel.Stable && release.GetProperty("prerelease").GetBoolean())) continue;
                string tag = release.GetProperty("tag_name").GetString() ?? "";
                if (!Regex.IsMatch(tag, @"^v?\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$", RegexOptions.CultureInvariant)) continue;
                if (!Version.TryParse(tag.TrimStart('v').Split('-')[0], out var version)) continue;
                var assets = new Dictionary<string, UpdateAsset>(StringComparer.Ordinal);
                foreach (var a in release.GetProperty("assets").EnumerateArray())
                {
                    string name = a.GetProperty("name").GetString() ?? "";
                    string digest = a.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
                    string url = a.GetProperty("browser_download_url").GetString() ?? "";
                    long size = a.GetProperty("size").GetInt64();
                    if (!Regex.IsMatch(digest, "^sha256:[0-9a-fA-F]{64}$") || size <= 0 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsAssetUri(uri, tag, name)) continue;
                    if (!assets.TryAdd(name, new(name, uri, digest[7..], size))) throw new InvalidDataException("Duplicate release asset.");
                }
                var offer = new UpdateOffer(tag, version, release.GetProperty("prerelease").GetBoolean(), assets);
                if (best is null || version > best.Version || (version == best.Version && best.Prerelease && !offer.Prerelease)) best = offer;
            }
            cachedChannel = channel; cachedVersion = currentVersion;
            return cached = new(best, best is null ? "No compatible release found." : "Release metadata retrieved from GitHub.", DateTimeOffset.UtcNow, best is not null && best.Version > currentVersion);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException or FormatException or KeyNotFoundException or TaskCanceledException)
        { return new(null, "Update check unavailable: " + ex.Message, DateTimeOffset.UtcNow); }
    }
    static bool IsAssetUri(Uri uri, string tag, string name) => uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath == ReleasePrefix + Uri.EscapeDataString(tag) + "/" + Uri.EscapeDataString(name);
    static UpdateAsset Asset(UpdateOffer offer, string name)
    {
        if (!offer.Assets.TryGetValue(name, out var a) || a.Name != name || !IsAssetUri(a.Url, offer.Tag, name) || !Regex.IsMatch(a.Sha256, "^[0-9a-fA-F]{64}$")) throw new InvalidDataException("Required release asset or GitHub SHA-256 digest is missing: " + name);
        return a;
    }
    async Task<byte[]> Download(UpdateAsset asset, int max, CancellationToken cancel)
    {
        if (asset.Size > max) throw new InvalidDataException("Release asset exceeds size limit.");
        Uri uri = asset.Url;
        for (int redirects = 0; redirects < 4; redirects++)
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancel);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var location = response.Headers.Location ?? throw new InvalidDataException("Missing redirect location.");
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) || !(uri.Host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.StartsWith(ReleasePrefix, StringComparison.Ordinal))) throw new InvalidDataException("Untrusted asset redirect.");
                continue;
            }
            response.EnsureSuccessStatusCode();
            byte[] bytes = await ReadBounded(response, max, cancel);
            if (bytes.LongLength != asset.Size || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Release asset length or SHA-256 mismatch.");
            return bytes;
        }
        throw new InvalidDataException("Too many asset redirects.");
    }
    static async Task<byte[]> ReadBounded(HttpResponseMessage response, int max, CancellationToken cancel)
    {
        if (response.Content.Headers.ContentLength > max) throw new InvalidDataException("Response exceeds size limit.");
        using var input = await response.Content.ReadAsStreamAsync(cancel); using var output = new MemoryStream(); byte[] buffer = new byte[81920];
        int count; while ((count = await input.ReadAsync(buffer, cancel)) != 0) { if (output.Length + count > max) throw new InvalidDataException("Response exceeds size limit."); output.Write(buffer, 0, count); }
        return output.ToArray();
    }
    async Task VerifyPublishedSum(UpdateOffer offer, UpdateAsset target, CancellationToken cancel)
    {
        byte[] data = await Download(Asset(offer, "SHA256SUMS"), MaxManifest, cancel);
        string[] matches = System.Text.Encoding.UTF8.GetString(data).Split('\n').Select(line => line.TrimEnd('\r')).Where(line => Regex.IsMatch(line, @"^[0-9a-fA-F]{64} [ *]" + Regex.Escape(target.Name) + "$" )).ToArray();
        if (matches.Length != 1 || !matches[0][..64].Equals(target.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA256SUMS does not match release asset.");
    }
    internal async Task<DownloadedFirmware> DownloadFirmwareAsync(UpdateOffer offer, string edition, CancellationToken cancel = default)
    {
        if (edition is not ("full" or "core")) throw new InvalidDataException("Unsupported firmware edition.");
        string binary = edition == "full" ? "redblade-fs-aurora-via.bin" : "redblade-fs-openrgb-core.bin";
        string manifest = edition == "full" ? "manifest.json" : "manifest-core.json";
        var binAsset = Asset(offer, binary); var manifestAsset = Asset(offer, manifest);
        await VerifyPublishedSum(offer, binAsset, cancel); await VerifyPublishedSum(offer, manifestAsset, cancel);
        byte[] bytes = await Download(binAsset, 65536, cancel), metadata = await Download(manifestAsset, MaxManifest, cancel);
        using var json = JsonDocument.Parse(metadata); var m = json.RootElement;
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!Version.TryParse((m.GetProperty("release").GetString() ?? "").TrimStart('v'), out var firmwareVersion) || firmwareVersion > offer.Version || bytes.Length != 65536 || !bytes.AsSpan(65532, 4).SequenceEqual(new byte[] { 0x55, 0x55, 0xaa, 0xaa }) || m.GetProperty("binary_bytes").GetInt32() != 65536 || m.GetProperty("edition").GetString() != edition || m.GetProperty("board").GetString() != "Designed by GG Redblade FS ISO" || m.GetProperty("mcu").GetString() != "SN32F248B" || m.GetProperty("usb_vid").GetString() != "320f" || m.GetProperty("usb_pid").GetString() != "5136" || m.GetProperty("led_count").GetInt32() != 109 || !hash.Equals(m.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Firmware identity, version, edition, length or SHA-256 mismatch.");
        return new(bytes, metadata, edition, firmwareVersion, hash);
    }
    internal async Task<PreparedSoftware> DownloadSoftwareAsync(UpdateOffer offer, CancellationToken cancel = default)
    {
        const string name = "RedBladePlus-Studio.exe";
        var asset = Asset(offer, name); await VerifyPublishedSum(offer, asset, cancel); byte[] bytes = await Download(asset, MaxSoftware, cancel);
        string current = Environment.ProcessPath ?? throw new InvalidOperationException("Current executable path unavailable.");
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RedBladePlus", "updates", offer.Version.ToString());
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, name), temp = Path.Combine(root, Guid.NewGuid().ToString("N") + ".tmp");
        try { await File.WriteAllBytesAsync(temp, bytes, cancel); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return new(path, asset.Sha256, current, offer.Version);
    }
    public void Dispose() => http.Dispose();
}

