using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace OrionLauncher.Services;

public class GamePathService
{
    private const int AppId = 376210;

    public string? Detect(string? overridePath = null)
    {
        if (!string.IsNullOrWhiteSpace(overridePath) && Directory.Exists(overridePath))
            return overridePath;

        var steamPath = GetSteamPath();
        if (steamPath == null) return null;

        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) return null;

        foreach (var libraryPath in ParseLibraryPaths(vdf))
        {
            var manifest = Path.Combine(libraryPath, "steamapps", $"appmanifest_{AppId}.acf");
            if (!File.Exists(manifest)) continue;

            var installDir = ParseAcfValue(manifest, "installdir");
            if (installDir == null) continue;

            var gamePath = Path.Combine(libraryPath, "steamapps", "common", installDir);
            if (Directory.Exists(gamePath))
                return gamePath;
        }

        return null;
    }

    private static string? GetSteamPath()
    {
        // 64-bit OS: WOW6432Node key
        var path = Registry.LocalMachine
            .OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
            ?.GetValue("InstallPath") as string;

        if (string.IsNullOrEmpty(path))
            path = Registry.CurrentUser
                .OpenSubKey(@"SOFTWARE\Valve\Steam")
                ?.GetValue("SteamPath") as string;

        return string.IsNullOrEmpty(path) ? null : path;
    }

    private static IEnumerable<string> ParseLibraryPaths(string vdfPath)
    {
        var text = File.ReadAllText(vdfPath);
        // Match "path"  "D:\\Steam" style entries
        var regex = new Regex(@"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
        foreach (Match m in regex.Matches(text))
        {
            var p = m.Groups[1].Value.Replace(@"\\", @"\");
            if (Directory.Exists(p))
                yield return p;
        }
    }

    private static string? ParseAcfValue(string acfPath, string key)
    {
        var text = File.ReadAllText(acfPath);
        var m = Regex.Match(text, $@"""{Regex.Escape(key)}""\s+""([^""]+)""", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }
}
