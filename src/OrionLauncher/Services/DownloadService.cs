using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;

namespace OrionLauncher.Services;

public class DownloadService
{
    private readonly HttpClient _http;

    public DownloadService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OrionLauncher", "1.0"));
    }

    /// <summary>
    /// Downloads a mod ZIP from <paramref name="zipUrl"/> and extracts all
    /// .pak / .ucas / .utoc files into <paramref name="destDir"/>.
    /// The _P suffix convention is for UE5 mount priority; we don't enforce it
    /// here — PakService.SyncMods deploys everything present in the storage dir.
    /// Progress is reported as 0–100.
    /// </summary>
    public async Task DownloadAndExtractModAsync(
        string zipUrl,
        string destDir,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(destDir);

        using var response = await _http.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[81920]; // 80 KB chunks
        long downloaded = 0;

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();

        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            ms.Write(buffer, 0, read);
            downloaded += read;
            if (total > 0)
                progress?.Report((int)(downloaded * 100 / total));
        }

        progress?.Report(100);
        ms.Seek(0, SeekOrigin.Begin);

        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            var name = Path.GetFileName(entry.FullName);
            if (!IsTripletFile(name)) continue;

            var dest = Path.Combine(destDir, name);
            entry.ExtractToFile(dest, overwrite: true);
        }
    }

    private static readonly string[] TripletExtensions = [".pak", ".ucas", ".utoc"];

    private static bool IsTripletFile(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return TripletExtensions.Contains(ext);
    }
}
