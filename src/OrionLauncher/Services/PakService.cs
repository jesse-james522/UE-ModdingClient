using System.IO;
using OrionLauncher.Models;

namespace OrionLauncher.Services;

public class PakService
{
    private static readonly string[] TripletExtensions = [".pak", ".ucas", ".utoc"];

    /// <summary>
    /// Ensures a PAK/UCAS/UTOC filename has the _P suffix UE5 needs for
    /// mod content to override base-game assets.
    /// e.g. "MyMod.pak" → "MyMod_P.pak",  "pakchunk55_0_P.pak" → unchanged
    /// </summary>
    public static string EnsurePSuffix(string filename)
    {
        var stem = Path.GetFileNameWithoutExtension(filename);
        var ext  = Path.GetExtension(filename);
        if (stem.EndsWith("_P", StringComparison.OrdinalIgnoreCase))
            return filename;
        return stem + "_P" + ext;
    }

    // --- Shim deployment ---

    public void DeployShims(string gameDir)
    {
        var binDir    = Path.Combine(gameDir, "TheIsle", "Binaries", "Win64");
        var vendorDir = GetVendorDir();
        Directory.CreateDirectory(binDir);
        CopyIfNewer(Path.Combine(vendorDir, "winhttp.dll"),              Path.Combine(binDir, "winhttp.dll"));
        CopyIfNewer(Path.Combine(vendorDir, "UniversalSigBypasser.asi"), Path.Combine(binDir, "UniversalSigBypasser.asi"));
    }

    public void RemoveShims(string gameDir)
    {
        var binDir = Path.Combine(gameDir, "TheIsle", "Binaries", "Win64");
        foreach (var name in new[] { "winhttp.dll", "UniversalSigBypasser.asi" })
        {
            var path = Path.Combine(binDir, name);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // --- Cleanup ---

    /// <summary>Deletes all files currently deployed to the game's LogicMods folder.</summary>
    public void WipeDeployedMods(string gameDir)
    {
        var dir = GetLogicModsDir(gameDir);
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.GetFiles(dir))
            File.Delete(f);
    }

    // --- Helpers ---

    private static void CopyIfNewer(string src, string dest)
    {
        if (!File.Exists(src)) return;
        if (!File.Exists(dest) || new FileInfo(src).Length != new FileInfo(dest).Length)
            File.Copy(src, dest, overwrite: true);
    }

    private static string GetVendorDir()
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        return Path.Combine(exeDir, "vendor");
    }

    /// <summary>The folder inside the game's Paks directory where Orion deploys mod files.</summary>
    public static string GetLogicModsDir(string gameDir) =>
        Path.Combine(gameDir, "TheIsle", "Content", "Paks", "LogicMods");
}
