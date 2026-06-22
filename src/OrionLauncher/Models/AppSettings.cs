namespace OrionLauncher.Models;

public class AppSettings
{
    public string? GameDirectoryOverride { get; set; }
    public string? LastModCommitSha      { get; set; }
    public bool    AutoLaunch            { get; set; } = false;
    public bool    AutoJoin              { get; set; } = false;
    public string? DevServerIp           { get; set; }
}
