using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrionLauncher.Models;
using OrionLauncher.Services;

namespace OrionLauncher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly LaunchService    _launch;
    private readonly GamePathService  _gamePath;
    private readonly EacService       _eac;
    private readonly ClientModService _clientMod;
    private readonly AutoJoinService  _autoJoinSvc;
    private readonly AppSettings      _settings;
    private readonly SettingsService  _settingsService;

    public ModInfoViewModel  ModInfoVm  { get; }
    public SettingsViewModel SettingsVm { get; }

    [ObservableProperty] private string _statusText   = "Initializing...";
    [ObservableProperty] private string _gameDir      = "";
    [ObservableProperty] private bool   _gameDirValid;
    [ObservableProperty] private bool   _isInjected;
    [ObservableProperty] private bool   _autoLaunch;
    [ObservableProperty] private bool   _autoJoin;

    public MainViewModel(
        LaunchService    launch,
        GamePathService  gamePath,
        EacService       eac,
        ClientModService clientMod,
        AutoJoinService  autoJoin,
        AppSettings      settings,
        SettingsService  settingsService,
        ModInfoViewModel modInfoVm,
        SettingsViewModel settingsVm)
    {
        _launch          = launch;
        _gamePath        = gamePath;
        _eac             = eac;
        _clientMod       = clientMod;
        _autoJoinSvc     = autoJoin;
        _settings        = settings;
        _settingsService = settingsService;
        ModInfoVm        = modInfoVm;
        SettingsVm       = settingsVm;

        _autoLaunch = settings.AutoLaunch;
        AutoJoin    = settings.AutoJoin;

        _launch.StatusMessage += msg => StatusText = msg;
    }

    public async Task InitializeAsync()
    {
        var detected = _gamePath.Detect(_settings.GameDirectoryOverride);
        GameDir      = detected ?? "";
        GameDirValid = !string.IsNullOrEmpty(detected) && System.IO.Directory.Exists(detected);

        if (!GameDirValid)
            StatusText = "Game directory not found — set it in Settings.";
        else if (_eac.CheckStaleBackup(GameDir))
        {
            _eac.RestoreEac(GameDir);
            StatusText = "Recovered from previous session — EAC restored.";
        }
        else
            StatusText = "Ready.";

        SettingsVm.ActiveGameDir = GameDir;

        // Background update check — populates the Mod tab status badge
        try
        {
            var hasUpdate = await _clientMod.CheckForUpdateAsync();
            ModInfoVm.SetStatus(
                upToDate:        !hasUpdate && _clientMod.HasCache,
                updateAvailable: hasUpdate,
                tag:             _clientMod.ShortTag);
        }
        catch
        {
            ModInfoVm.CommitInfo = "Could not check for updates";
        }
    }

    // ── Inject ────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanInject))]
    private async Task Inject()
    {
        if (!GameDirValid) return;

        // Check / download update
        StatusText = "Checking for updates...";
        bool needsDownload;
        try   { needsDownload = await _clientMod.CheckForUpdateAsync(); }
        catch { needsDownload = !_clientMod.HasCache; }

        if (needsDownload)
        {
            var statusProg  = new Progress<string>(msg => StatusText = msg);
            var percentProg = new Progress<int>(p  => ModInfoVm.DownloadProgressPct = p);
            ModInfoVm.IsDownloading = true;
            try
            {
                await _clientMod.DownloadAndCacheAsync(statusProg, percentProg);
            }
            catch (Exception ex)
            {
                ModInfoVm.IsDownloading = false;
                if (!_clientMod.HasCache)
                {
                    StatusText = $"Download failed and no cached files exist: {ex.Message}";
                    return;
                }
                StatusText = "Download failed — using cached files.";
            }
            ModInfoVm.IsDownloading = false;
        }

        // Deploy PAKs to LogicMods
        StatusText = "Deploying mod files...";
        _clientMod.DeployToGame(GameDir);
        ModInfoVm.SetStatus(upToDate: true, updateAvailable: false, tag: _clientMod.ShortTag);

        // Write auto-join config to Engine.ini
        try
        {
            StatusText = "Writing server config...";
            await _autoJoinSvc.WriteConfigAsync(AutoJoin);
        }
        catch (Exception ex)
        {
            StatusText = $"Warning: could not write Engine.ini — {ex.Message}";
        }

        // EAC + shims
        try
        {
            _launch.Inject(GameDir);
        }
        catch (UnauthorizedAccessException)
        {
            RelaunchAsAdmin();
            return;
        }
        catch (Exception ex)
        {
            StatusText = $"Inject failed: {ex.Message}";
            return;
        }

        IsInjected = true;
        InjectCommand.NotifyCanExecuteChanged();

        if (AutoLaunch)
            _launch.Launch(GameDir);
    }

    // ── Launch ────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task LaunchGame()
    {
        if (!GameDirValid) return;
        try
        {
            await _autoJoinSvc.WriteConfigAsync(AutoJoin);
            _launch.Launch(GameDir);
        }
        catch (Exception ex) { StatusText = $"Launch failed: {ex.Message}"; }
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        if (!GameDirValid) return;
        try
        {
            _launch.Remove(GameDir);
            IsInjected = false;
            InjectCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) { StatusText = $"Remove failed: {ex.Message}"; }
    }

    // ── CanExecute ────────────────────────────────────────────────────────────

    private bool CanInject()  => GameDirValid && !IsInjected;
    private bool CanLaunch()  => GameDirValid;
    private bool CanRemove()  => GameDirValid;

    partial void OnGameDirValidChanged(bool value)
    {
        InjectCommand.NotifyCanExecuteChanged();
        LaunchGameCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    partial void OnAutoLaunchChanged(bool value)
    {
        _settings.AutoLaunch = value;
        _settingsService.Save(_settings);
    }

    partial void OnAutoJoinChanged(bool value)
    {
        _settings.AutoJoin = value;
        _settingsService.Save(_settings);
    }

    private static void RelaunchAsAdmin()
    {
        var psi = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
        {
            Verb            = "runas",
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(psi);
        System.Windows.Application.Current.Shutdown();
    }
}
