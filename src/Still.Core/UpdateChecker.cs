using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Still.Core;

/// <summary>Project links and the on-demand GitHub release check. Nothing here runs unless the user asks.</summary>
public static class Links
{
    public const string Author = "Danny Rudd";
    public const string Repository = "https://github.com/danny19862025/still-clock";
    public const string Releases = Repository + "/releases/latest";
    public const string Issues = Repository + "/issues";
    public const string Coffee = "https://buymeacoffee.com/danny1986";
}

public sealed record ReleaseInfo(Version Version, string Tag, string PageUrl, string? InstallerUrl, string? ChecksumsUrl);

public static class UpdateChecker
{
    public const string LatestApi = "https://api.github.com/repos/danny19862025/still-clock/releases/latest";
    public const string InstallerName = "Still-Setup-win-x64.exe";
    public const string LinuxArchiveName = "Still-linux-x64.tar.gz";
    private const string DownloadPrefix = "https://github.com/danny19862025/still-clock/releases/download/";
    private const long MaxInstallerBytes = 300L * 1024 * 1024;

    /// <summary>Parses "v2.4.0" or "2.4" into a three-part version; null if it is not a plain release number.</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var text = tag.Trim().TrimStart('v', 'V');
        var parts = text.Split('.');
        if (parts.Length is < 2 or > 3) return null;
        var numbers = new int[3];
        for (int i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out numbers[i])) return null;
        return new Version(numbers[0], numbers[1], numbers[2]);
    }

    public static bool IsNewer(Version latest, Version current) =>
        new Version(latest.Major, latest.Minor, Math.Max(0, latest.Build)) > new Version(current.Major, current.Minor, Math.Max(0, current.Build));

    /// <summary>Reads the GitHub "latest release" response. Drafts, pre-releases and odd tags are rejected.</summary>
    public static ReleaseInfo ParseLatest(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new FormatException("GitHub returned an unexpected response.");
        if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True ||
            root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True)
            throw new FormatException("No stable release is available.");
        string? tag = root.TryGetProperty("tag_name", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        var version = ParseVersion(tag) ?? throw new FormatException("The latest release has no version number.");
        string page = root.TryGetProperty("html_url", out var h) && h.ValueKind == JsonValueKind.String && (h.GetString() ?? "").StartsWith(Links.Repository + "/", StringComparison.Ordinal)
            ? h.GetString()! : Links.Releases;
        string? installer = null, sums = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            foreach (var asset in assets.EnumerateArray())
            {
                string? name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                string? url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                // Only accept downloads from this repository's own releases.
                if (url == null || !url.StartsWith(DownloadPrefix, StringComparison.Ordinal)) continue;
                if (name == InstallerName) installer = url;
                if (name == "SHA256SUMS.txt") sums = url;
            }
        return new ReleaseInfo(version, tag!, page, installer, sums);
    }

    /// <summary>Finds a file's hash in a "hash  name" SHA256SUMS listing.</summary>
    public static string? ExpectedHash(string sums, string fileName)
    {
        foreach (var raw in sums.Split('\n'))
        {
            var line = raw.Trim();
            int space = line.IndexOf(' ');
            if (space != 64) continue;
            if (line[space..].Trim().TrimStart('*') == fileName && line[..64].All(Uri.IsHexDigit)) return line[..64].ToLowerInvariant();
        }
        return null;
    }

    public static string Summary(ReleaseInfo latest, Version current) => IsNewer(latest.Version, current)
        ? $"Still {latest.Version.ToString(3)} is available. You have {current.ToString(3)}."
        : $"You're up to date. Installed: {current.ToString(3)}; latest: {latest.Version.ToString(3)}.";

    public static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // A generic product name; no account, machine or personal details are sent.
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Still-Clock", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static async Task<ReleaseInfo> CheckAsync(HttpClient client, CancellationToken cancel = default)
    {
        using var response = await client.GetAsync(LatestApi, HttpCompletionOption.ResponseHeadersRead, cancel);
        if ((int)response.StatusCode == 403 || (int)response.StatusCode == 429) throw new HttpRequestException("GitHub is limiting requests right now. Try again later.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 1024 * 1024) throw new FormatException("GitHub returned an unexpectedly large response.");
        return ParseLatest(await response.Content.ReadAsStringAsync(cancel));
    }

    /// <summary>Downloads the Windows installer and its checksum list, and returns the installer path only if the SHA-256 matches.</summary>
    public static async Task<string> DownloadVerifiedInstallerAsync(HttpClient client, ReleaseInfo release, string directory, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        if (release.InstallerUrl == null || release.ChecksumsUrl == null) throw new InvalidOperationException("This release has no Windows installer.");
        string sums = await client.GetStringAsync(release.ChecksumsUrl, cancel);
        string expected = ExpectedHash(sums, InstallerName) ?? throw new InvalidOperationException("The release checksums do not list the installer.");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"Still-Setup-{release.Version.ToString(3)}.exe");
        using (var response = await client.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancel))
        {
            response.EnsureSuccessStatusCode();
            long? total = response.Content.Headers.ContentLength;
            if (total > MaxInstallerBytes) throw new InvalidOperationException("The installer is unexpectedly large.");
            await using var source = await response.Content.ReadAsStreamAsync(cancel);
            await using var file = File.Create(path);
            var buffer = new byte[81920];
            long read = 0; int n;
            while ((n = await source.ReadAsync(buffer, cancel)) > 0)
            {
                read += n;
                if (read > MaxInstallerBytes) throw new InvalidOperationException("The installer is unexpectedly large.");
                await file.WriteAsync(buffer.AsMemory(0, n), cancel);
                if (total > 0) progress?.Report((double)read / total.Value);
            }
        }
        string actual;
        await using (var check = File.OpenRead(path)) actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(check, cancel));
        if (actual != expected)
        {
            try { File.Delete(path); } catch (IOException) { }
            throw new InvalidOperationException("The downloaded installer did not match its published checksum, so it was deleted.");
        }
        return path;
    }
}
