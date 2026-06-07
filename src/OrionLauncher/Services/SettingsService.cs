using System.IO;
using System.Text.Json;
using OrionLauncher.Models;

namespace OrionLauncher.Services;

public class SettingsService
{
    public static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OrionLauncher");

    private static readonly string SettingsPath = Path.Combine(DataDir, "settings.json");

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public AppSettings Load()
    {
        Directory.CreateDirectory(DataDir);
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        try
        {
            var text = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(text) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DataDir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, _json));
    }
}
