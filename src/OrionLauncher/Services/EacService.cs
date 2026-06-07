using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace OrionLauncher.Services;

public class EacService
{
    private const string BackupExtension = ".orion_backup";
    private int _restoreGuard; // Interlocked flag — 0 = not restored, 1 = restored

    // Vanilla Settings.json for The Isle — used as a fallback if the backup is missing.
    private const string VanillaSettings =
        "{\r\n" +
        "    \"productid\"\t\t\t\t\t\t\t\t\t\t: \"108bae92517548518cbd371722381ded\",\r\n" +
        "    \"sandboxid\"\t\t\t\t\t\t\t\t\t\t: \"9c46d97dbe664f63823d11cf0b1cd8ae\",\r\n" +
        "    \"deploymentid\"\t\t\t\t\t\t\t\t\t: \"6db6bea492f94b1bbdfcdfe3e4f898dc\",\r\n" +
        "    \"title\"\t\t\t\t\t\t\t\t\t\t\t: \"TheIsleClient\",\r\n" +
        "    \"executable\"\t\t\t\t\t\t\t\t\t: \"TheIsle\\\\Binaries\\\\Win64\\\\TheIsleClient-Win64-Shipping.exe\",\r\n" +
        "    \"logo_position\"\t\t\t\t\t\t\t\t\t: \"bottom-left\",\r\n" +
        "\t\"requested_splash\"\t\t\t\t\t\t\t\t: \"EasyAntiCheat/SplashScreen.png\",\r\n" +
        "    \"parameters\"\t\t\t\t\t\t\t\t\t: \"\",\r\n" +
        "    \"use_cmdline_parameters\"\t\t\t\t\t\t: \"1\",\r\n" +
        "    \"working_directory\"\t\t\t\t\t\t\t\t: \"\",\r\n" +
        "    \"wait_for_game_process_exit\"\t\t\t\t\t: \"1\",\r\n" +
        "    \"hide_splash_screen\"\t\t\t\t\t\t\t: \"0\",\r\n" +
        "    \"hide_ui_controls\"\t\t\t\t\t\t\t: \"0\"\r\n" +
        "}\r\n";

    private static string SettingsPath(string gameDir) =>
        Path.Combine(gameDir, "EasyAntiCheat", "Settings.json");

    private static string BackupPath(string gameDir) =>
        SettingsPath(gameDir) + BackupExtension;

    public bool CheckStaleBackup(string gameDir) => File.Exists(BackupPath(gameDir));

    public void DisableEac(string gameDir)
    {
        Interlocked.Exchange(ref _restoreGuard, 0);

        var settingsPath = SettingsPath(gameDir);
        var backupPath = BackupPath(gameDir);

        if (!File.Exists(settingsPath))
            throw new FileNotFoundException($"EAC Settings.json not found at: {settingsPath}");

        var original = File.ReadAllText(settingsPath);
        File.WriteAllText(backupPath, original);

        var corrupted = CorruptGuids(original);
        File.WriteAllText(settingsPath, corrupted);
    }

    public void RestoreEac(string gameDir)
    {
        // Guard against double-restore
        if (Interlocked.CompareExchange(ref _restoreGuard, 1, 0) != 0)
            return;

        var backupPath = BackupPath(gameDir);
        var settingsPath = SettingsPath(gameDir);

        if (File.Exists(backupPath))
        {
            File.Copy(backupPath, settingsPath, overwrite: true);
            File.Delete(backupPath);
        }
        else
        {
            // Backup missing (e.g. deleted externally) — write known-good vanilla content
            File.WriteAllText(settingsPath, VanillaSettings);
        }
    }

    // Corrupt the last hex character of productid, sandboxid, deploymentid values.
    // Digit → different digit; letter (a–f) → different letter. Deterministic.
    private static string CorruptGuids(string json)
    {
        var fields = new[] { "productid", "sandboxid", "deploymentid" };
        var result = json;

        foreach (var field in fields)
        {
            result = Regex.Replace(
                result,
                $@"(""{field}""\s*:\s*"")([^""]+)("")",
                m => m.Groups[1].Value + CorruptLastHexChar(m.Groups[2].Value) + m.Groups[3].Value,
                RegexOptions.IgnoreCase);
        }

        return result;
    }

    private static string CorruptLastHexChar(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        int i = value.Length - 1;
        char c = value[i];

        char replacement = c switch
        {
            >= '0' and <= '9' => c == '9' ? '0' : (char)(c + 1),
            >= 'a' and <= 'f' => c == 'f' ? 'a' : (char)(c + 1),
            >= 'A' and <= 'F' => c == 'F' ? 'A' : (char)(c + 1),
            _ => c
        };

        return value[..i] + replacement;
    }
}
