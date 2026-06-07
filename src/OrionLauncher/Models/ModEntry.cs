using CommunityToolkit.Mvvm.ComponentModel;

namespace OrionLauncher.Models;

public partial class ModEntry : ObservableObject
{
    // From mods.json trusted list
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string ZipUrl { get; set; } = "";

    // True for files imported directly from disk (no remote URL)
    public bool IsLocal { get; set; }

    // Fetched lazily from the mod's GitHub release (remote mods only)
    public string? Author { get; set; }
    public string? Version { get; set; }
    public string? FullDescription { get; set; }

    // Runtime state
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _isInstalled;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private int _downloadProgress;

    // Remote mods can be downloaded; local mods are already installed
    public bool CanDownload => !IsLocal && !IsInstalled && !IsDownloading;
    public bool CanDelete => IsInstalled && !IsDownloading;

    // Label shown in the Source column
    public string SourceLabel => IsLocal ? "Local" : "Remote";
}
