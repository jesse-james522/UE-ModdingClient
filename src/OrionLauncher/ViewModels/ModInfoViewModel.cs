using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrionLauncher.Services;

namespace OrionLauncher.ViewModels;

public partial class ModInfoViewModel : ObservableObject
{
    private readonly ClientModService _clientMod;

    public string ModName        => "Evrima Mod for Thenyaw";
    public string ModAuthor      => "Made by Pretzel";
    public string ModDescription => "This is a map overhaul bringing thenyaw into evrima";

    [ObservableProperty] private bool   _isUpToDate;
    [ObservableProperty] private bool   _updateAvailable;
    [ObservableProperty] private string _commitInfo          = "Checking for updates...";
    [ObservableProperty] private bool   _isDownloading;
    [ObservableProperty] private int    _downloadProgressPct;

    public ModInfoViewModel(ClientModService clientMod)
    {
        _clientMod = clientMod;
    }

    public void SetStatus(bool upToDate, bool updateAvailable, string? tag)
    {
        IsUpToDate      = upToDate;
        UpdateAvailable = updateAvailable;
        CommitInfo      = tag != null ? $"Installed: {tag}" : "Not yet downloaded";
    }

    [RelayCommand]
    private async Task ForceRedownload()
    {
        IsDownloading        = true;
        DownloadProgressPct  = 0;
        try
        {
            var statusProg  = new Progress<string>(msg => CommitInfo = msg);
            var percentProg = new Progress<int>(p  => DownloadProgressPct = p);
            await _clientMod.DownloadAndCacheAsync(statusProg, percentProg);
            SetStatus(upToDate: true, updateAvailable: false, tag: _clientMod.ShortTag);
        }
        catch (Exception ex)
        {
            CommitInfo = $"Download failed: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }
}
