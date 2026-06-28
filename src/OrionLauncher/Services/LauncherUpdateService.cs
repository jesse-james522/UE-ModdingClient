using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace OrionLauncher.Services;

/// <summary>
/// Checks whether this launcher build is current by comparing the SHA-256 of
/// the running exe against the <c>launcher.sha256</c> asset attached to the
/// latest GitHub release. No version numbers — match means up to date, mismatch
/// means a newer build has been published. Never throws; a failed check reports
/// "unknown".
///
/// Workflow: publish-release.bat writes launcher.sha256 next to the build;
/// attach that file as a release asset alongside the distributed zip. The
/// "/releases/latest/download/" URL always points at the newest release's copy.
/// </summary>
public class LauncherUpdateService
{
    private const string RepoOwner = "jesse-james522";
    private const string RepoName  = "UE-ModdingClient";

    // Permanent redirect to the launcher.sha256 asset on the latest release.
    private const string HashFileUrl =
        $"https://github.com/{RepoOwner}/{RepoName}/releases/latest/download/launcher.sha256";

    public const string DownloadPageUrl =
        $"https://github.com/{RepoOwner}/{RepoName}/releases/latest";

    private readonly HttpClient _http;

    public LauncherUpdateService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("OrionLauncher", "1.0"));
    }

    public record Result(bool Checked, bool UpdateAvailable, string Message);

    public async Task<Result> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var raw = await _http.GetStringAsync(HashFileUrl, ct);
            var expected = raw
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(expected))
                return new Result(false, false, "Couldn't check for launcher updates");

            var self = await ComputeSelfHashAsync(ct);
            if (self is null)
                return new Result(false, false, "Couldn't verify this build");

            return string.Equals(self, expected, StringComparison.OrdinalIgnoreCase)
                ? new Result(true, false, "Up to date")
                : new Result(true, true,  "A newer launcher build is available");
        }
        catch
        {
            return new Result(false, false, "Couldn't check for launcher updates");
        }
    }

    private static async Task<string?> ComputeSelfHashAsync(CancellationToken ct)
    {
        try
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            await using var fs = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(fs, ct);
            return Convert.ToHexString(hash); // uppercase hex; compared case-insensitively
        }
        catch
        {
            return null;
        }
    }
}
