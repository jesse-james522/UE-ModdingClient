namespace OrionLauncher.Models;

public class ServerInfo
{
    public string  Name        { get; set; } = "";
    public string  HostAddress { get; set; } = "";
    public string  MapName     { get; set; } = "";
    public int     PlayerCount { get; set; }
    public int     MaxPlayers  { get; set; }
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);
}
