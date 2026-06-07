namespace OrionLauncher.Models;

public class AppSettings
{
    public string? GameDirectoryOverride { get; set; }
    public string? LastModCommitSha      { get; set; }
    public bool    AutoLaunch            { get; set; } = false;
}
