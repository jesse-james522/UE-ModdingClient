using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using OrionLauncher.Models;

namespace OrionLauncher.Services;

public class AutoJoinService
{
    private const string IpFileUrl =
        "https://raw.githubusercontent.com/jesse-james522/UE-ModdingClient/main/IP.txt";

    private const string IniSection =
        "/Game/TheIsle/Core/Session/BP_TIGameSession.BP_TIGameSession_C";

    private static readonly string EngineIniPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheIsle", "Saved", "Config", "WindowsClient", "Engine.ini");

    private readonly HttpClient  _http;
    private readonly AppSettings _settings;

    public AutoJoinService(AppSettings settings)
    {
        _settings = settings;
        _http     = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("OrionLauncher", "1.0"));
    }

    public async Task<string?> FetchServerIpAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_settings.DevServerIp))
            return _settings.DevServerIp.Trim();

        try
        {
            var text = await _http.GetStringAsync(IpFileUrl, ct);
            return text.Trim();
        }
        catch
        {
            return null;
        }
    }

    public async Task WriteConfigAsync(bool shouldJoin, CancellationToken ct = default)
    {
        var ip = await FetchServerIpAsync(ct);

        var dir = Path.GetDirectoryName(EngineIniPath)!;
        Directory.CreateDirectory(dir);

        var lines = File.Exists(EngineIniPath)
            ? new List<string>(await File.ReadAllLinesAsync(EngineIniPath, ct))
            : new List<string>();

        SetIniValue(lines, IniSection, "bShouldJoinOnLogin", shouldJoin ? "True" : "False");
        SetIniValue(lines, IniSection, "IP:PortToJoin", ip ?? "");

        if (File.Exists(EngineIniPath))
            File.SetAttributes(EngineIniPath, File.GetAttributes(EngineIniPath) & ~FileAttributes.ReadOnly);

        await File.WriteAllLinesAsync(EngineIniPath, lines, ct);

        File.SetAttributes(EngineIniPath, File.GetAttributes(EngineIniPath) | FileAttributes.ReadOnly);
    }

    private static void SetIniValue(List<string> lines, string section, string key, string value)
    {
        var sectionHeader = $"[{section}]";
        var keyPrefix     = $"{key}=";

        int sectionIdx = lines.FindIndex(
            l => l.TrimEnd().Equals(sectionHeader, StringComparison.OrdinalIgnoreCase));

        if (sectionIdx < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                lines.Add("");
            lines.Add(sectionHeader);
            lines.Add($"{key}={value}");
            return;
        }

        int nextSection = lines.FindIndex(sectionIdx + 1, l => l.TrimStart().StartsWith("["));
        int end         = nextSection < 0 ? lines.Count : nextSection;

        int keyIdx = -1;
        for (int i = sectionIdx + 1; i < end; i++)
        {
            if (lines[i].StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                keyIdx = i;
                break;
            }
        }

        if (keyIdx >= 0)
            lines[keyIdx] = $"{key}={value}";
        else
            lines.Insert(end, $"{key}={value}");
    }
}
