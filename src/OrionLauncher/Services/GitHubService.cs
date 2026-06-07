using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using OrionLauncher.Models;

namespace OrionLauncher.Services;

public class GitHubService
{
    private readonly HttpClient _http;
    private readonly string _cacheDir;

    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public GitHubService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("OrionLauncher", "1.0"));
        _cacheDir = Path.Combine(SettingsService.DataDir, "cache");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<ModManifest?> FetchModsAsync(string url, CancellationToken ct = default)
    {
        return await FetchJsonAsync<ModManifest>(url, "mods.json", ct);
    }

    public async Task<ServerManifest?> FetchServersAsync(string url, CancellationToken ct = default)
    {
        return await FetchJsonAsync<ServerManifest>(url, "servers.json", ct);
    }

    private async Task<T?> FetchJsonAsync<T>(string url, string cacheFile, CancellationToken ct)
    {
        var cachePath = Path.Combine(_cacheDir, cacheFile);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (File.Exists(cachePath + ".etag"))
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(
                    File.ReadAllText(cachePath + ".etag")));

            var response = await _http.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified && File.Exists(cachePath))
                return DeserializeCache<T>(cachePath);

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            await File.WriteAllTextAsync(cachePath, body, ct);

            if (response.Headers.ETag != null)
                await File.WriteAllTextAsync(cachePath + ".etag", response.Headers.ETag.Tag, ct);

            return JsonSerializer.Deserialize<T>(body, _json);
        }
        catch when (File.Exists(cachePath))
        {
            return DeserializeCache<T>(cachePath);
        }
    }

    private static T? DeserializeCache<T>(string path)
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), _json); }
        catch { return default; }
    }

    // Lazy-load mod detail from GitHub API
    public async Task<(string? author, string? version, string? description)> FetchModDetailAsync(
        string zipUrl, CancellationToken ct = default)
    {
        try
        {
            // zipUrl: https://github.com/{owner}/{repo}/releases/download/{tag}/{file}
            var parts = new Uri(zipUrl).AbsolutePath.TrimStart('/').Split('/');
            if (parts.Length < 5) return default;
            var owner = parts[0];
            var repo = parts[1];
            var tag = parts[3];

            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}";
            var json = await _http.GetStringAsync(apiUrl, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var version = root.TryGetProperty("tag_name", out var tv) ? tv.GetString() : null;
            var description = root.TryGetProperty("body", out var bv) ? bv.GetString() : null;

            // Author from repo info
            var repoJson = await _http.GetStringAsync($"https://api.github.com/repos/{owner}/{repo}", ct);
            using var repoDoc = JsonDocument.Parse(repoJson);
            var author = repoDoc.RootElement.TryGetProperty("owner", out var ov)
                && ov.TryGetProperty("login", out var lv) ? lv.GetString() : owner;

            return (author, version, description);
        }
        catch
        {
            return default;
        }
    }
}
