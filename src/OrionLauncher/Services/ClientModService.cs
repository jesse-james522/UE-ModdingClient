using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using OrionLauncher.Models;
using System.IO.Compression;

namespace OrionLauncher.Services;

/// <summary>
/// Downloads the latest paks.rar from the ClientMod GitHub release,
/// caches extracted PAKs next to the exe in mod_cache/, and deploys them
/// to the game's LogicMods folder.
/// </summary>
public class ClientModService
{
    private const string RepoOwner      = "jesse-james522";
    private const string RepoName       = "ClientMod";
    private const string ReleaseAsset   = "paks.zip";
    private const string ReleaseApiUrl  =
        $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    private static readonly HashSet<string> TripletExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pak", ".ucas", ".utoc" };

    private static readonly string ModCacheDir =
        Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath!) ?? AppContext.BaseDirectory,
            "mod_cache");

    private readonly HttpClient      _http;
    private readonly SettingsService _settingsService;
    private readonly AppSettings     _settings;

    public string? InstalledTag  { get; private set; }
    public string? LatestTag     { get; private set; }
    public string? LatestRarUrl  { get; private set; }

    public bool UpdateAvailable => LatestTag != null && LatestTag != InstalledTag;
    public bool HasCache        => Directory.Exists(ModCacheDir) &&
                                   Directory.GetFiles(ModCacheDir, "*.pak").Length > 0;

    public ClientModService(SettingsService settingsService, AppSettings settings)
    {
        _settingsService = settingsService;
        _settings        = settings;

        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("OrionLauncher", "1.0"));

        InstalledTag = settings.LastModCommitSha; // reusing field — stores release tag
        Directory.CreateDirectory(ModCacheDir);
    }

    /// <summary>
    /// Fetches the latest release tag from GitHub.
    /// Returns true if an update is available or no cached files exist.
    /// </summary>
    public async Task<bool> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await _http.GetStringAsync(ReleaseApiUrl, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            LatestTag = root.GetProperty("tag_name").GetString();

            // Find the paks.rar asset URL
            LatestRarUrl = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    if (string.Equals(name, ReleaseAsset, StringComparison.OrdinalIgnoreCase))
                    {
                        LatestRarUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            return UpdateAvailable || !HasCache;
        }
        catch
        {
            LatestTag    = InstalledTag;
            LatestRarUrl = null;
            return !HasCache;
        }
    }

    /// <summary>
    /// Downloads paks.rar from the latest release, extracts PAK triplets into mod_cache/.
    /// Reports progress as "Downloading... X.X MB / Y.Y MB (Z%)" then "Extracting...".
    /// </summary>
    public async Task DownloadAndCacheAsync(
        IProgress<string>? status  = null,
        IProgress<int>?    percent = null,
        CancellationToken  ct      = default)
    {
        var url = LatestRarUrl;
        if (string.IsNullOrEmpty(url))
        {
            // Re-fetch if we don't have the URL yet (e.g. force re-download path)
            await CheckForUpdateAsync(ct);
            url = LatestRarUrl;
        }

        if (string.IsNullOrEmpty(url))
            throw new InvalidOperationException(
                "No paks.rar asset found in the latest release. " +
                "Make sure the release has a paks.rar file attached.");

        status?.Report("Connecting to GitHub...");
        percent?.Report(0);

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var totalMb    = totalBytes > 0 ? totalBytes / 1_048_576.0 : 0;

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var ms     = new MemoryStream(totalBytes > 0 ? (int)Math.Min(totalBytes, int.MaxValue) : 0);

        var buffer     = new byte[81920];
        long downloaded = 0;
        int  read;

        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            ms.Write(buffer, 0, read);
            downloaded += read;

            var dlMb = downloaded / 1_048_576.0;
            if (totalBytes > 0)
            {
                var pct = (int)(downloaded * 100 / totalBytes);
                status?.Report($"Downloading... {dlMb:F1} MB / {totalMb:F1} MB ({pct}%)");
                percent?.Report(pct);
            }
            else
            {
                status?.Report($"Downloading... {dlMb:F1} MB");
            }
        }

        percent?.Report(100);
        status?.Report("Extracting mod files...");

        // Clear old cache
        foreach (var f in Directory.GetFiles(ModCacheDir))
            File.Delete(f);

        ms.Seek(0, SeekOrigin.Begin);

        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            var name = Path.GetFileName(entry.FullName);
            var ext  = Path.GetExtension(name).ToLowerInvariant();
            if (!TripletExtensions.Contains(ext)) continue;

            var destPath = Path.Combine(ModCacheDir, PakService.EnsurePSuffix(name));
            entry.ExtractToFile(destPath, overwrite: true);
        }

        // Persist installed tag
        InstalledTag                 = LatestTag;
        _settings.LastModCommitSha   = LatestTag;
        _settingsService.Save(_settings);

        status?.Report($"Done — {Directory.GetFiles(ModCacheDir).Length} file(s) cached.");
    }

    /// <summary>
    /// Copies cached PAK triplets into the game's LogicMods folder, wiping it first.
    /// </summary>
    public void DeployToGame(string gameDir)
    {
        var logicModsDir = PakService.GetLogicModsDir(gameDir);
        Directory.CreateDirectory(logicModsDir);

        foreach (var f in Directory.GetFiles(logicModsDir))
            File.Delete(f);

        foreach (var file in Directory.GetFiles(ModCacheDir))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (!TripletExtensions.Contains(ext)) continue;
            File.Copy(file, Path.Combine(logicModsDir, Path.GetFileName(file)), overwrite: true);
        }
    }

    /// <summary>Deletes all cached mod files and resets the stored release tag.</summary>
    public void WipeCache()
    {
        if (Directory.Exists(ModCacheDir))
            foreach (var f in Directory.GetFiles(ModCacheDir))
                File.Delete(f);

        InstalledTag                 = null;
        _settings.LastModCommitSha   = null;
        _settingsService.Save(_settings);
    }

    public string? ShortTag => LatestTag ?? InstalledTag;
}
