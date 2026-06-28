using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OrionLauncher.Models;
using OrionLauncher.Services;

namespace OrionLauncher.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService       _settingsService;
    private readonly AppSettings           _settings;
    private readonly PakService            _pak;
    private readonly EacService            _eac;
    private readonly ClientModService      _clientMod;
    private readonly LauncherUpdateService _launcherUpdate;

    public string ActiveGameDir { get; set; } = "";

    [ObservableProperty] private string  _gameDirectory = "";
    [ObservableProperty] private string  _detectedPath  = "";
    [ObservableProperty] private string? _devServerIp;

    [ObservableProperty] private string _launcherUpdateStatus   = "Checking for launcher updates...";
    [ObservableProperty] private bool   _launcherUpdateAvailable;

    public SettingsViewModel(
        SettingsService       settingsService,
        AppSettings           settings,
        PakService            pak,
        EacService            eac,
        ClientModService      clientMod,
        LauncherUpdateService launcherUpdate)
    {
        _settingsService = settingsService;
        _settings        = settings;
        _pak             = pak;
        _eac             = eac;
        _clientMod       = clientMod;
        _launcherUpdate  = launcherUpdate;

        GameDirectory = _settings.GameDirectoryOverride ?? "";
        DevServerIp   = _settings.DevServerIp;
    }

    /// <summary>Checks GitHub for a newer launcher release. Safe to fire-and-forget.</summary>
    public async Task RefreshLauncherVersionAsync()
    {
        var result = await _launcherUpdate.CheckAsync();
        Application.Current.Dispatcher.Invoke(() =>
        {
            LauncherUpdateStatus    = result.Message;
            LauncherUpdateAvailable = result.UpdateAvailable;
        });
    }

    [RelayCommand]
    private void OpenLatestRelease()
    {
        try
        {
            Process.Start(new ProcessStartInfo(LauncherUpdateService.DownloadPageUrl)
            {
                UseShellExecute = true
            });
        }
        catch { /* opening a browser is best-effort */ }
    }

    [RelayCommand]
    private void Browse()
    {
        var dlg = new OpenFolderDialog
        {
            Title            = "Select The Isle game directory",
            InitialDirectory = string.IsNullOrWhiteSpace(GameDirectory) ? null : GameDirectory
        };
        if (dlg.ShowDialog() == true)
            GameDirectory = dlg.FolderName;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.GameDirectoryOverride = string.IsNullOrWhiteSpace(GameDirectory) ? null : GameDirectory;
        _settings.DevServerIp           = string.IsNullOrWhiteSpace(DevServerIp)   ? null : DevServerIp.Trim();
        _settingsService.Save(_settings);
        MessageBox.Show("Settings saved.", "Orion", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── Danger zone ───────────────────────────────────────────────────────────

    /// <summary>
    /// Hard reset — removes EAC bypass, shims, and all mod files from the game.
    /// Requires confirmation.
    /// </summary>
    [RelayCommand]
    private void RestoreGame()
    {
        if (!RequireGameDir()) return;

        var r = MessageBox.Show(
            "This will:\n\n" +
            "  • Restore EasyAntiCheat\\Settings.json to vanilla\n" +
            "  • Delete winhttp.dll and UniversalSigBypasser.asi from Binaries\\Win64\n" +
            "  • Delete all mod files from Content\\Paks\\LogicMods\n\n" +
            "Are you sure?",
            "Restore Game to Vanilla",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (r != MessageBoxResult.Yes) return;

        try
        {
            _pak.WipeDeployedMods(ActiveGameDir);
            _pak.RemoveShims(ActiveGameDir);
            _eac.RestoreEac(ActiveGameDir);
            MessageBox.Show("Game restored to vanilla.", "Orion", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Restore failed: {ex.Message}", "Orion", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Deletes all cached mod files — they re-download on next inject.</summary>
    [RelayCommand]
    private void ClearDownloadedMods()
    {
        var r = MessageBox.Show(
            "Delete all cached mod files? They will be re-downloaded on the next inject.",
            "Clear Downloaded Mods", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (r != MessageBoxResult.Yes) return;

        try
        {
            _clientMod.WipeCache();
            MessageBox.Show("Mod cache cleared.", "Orion", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed: {ex.Message}", "Orion", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool RequireGameDir()
    {
        if (!string.IsNullOrEmpty(ActiveGameDir) && Directory.Exists(ActiveGameDir))
            return true;
        MessageBox.Show("Game directory is not set or not found. Configure it above and save.", "Orion",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }
}
