using System.Windows;
using OrionLauncher.Models;
using OrionLauncher.Services;
using OrionLauncher.ViewModels;

namespace OrionLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsService = new SettingsService();
        var settings        = settingsService.Load();

        var pak       = new PakService();
        var eac       = new EacService();
        var gamePath  = new GamePathService();
        var clientMod = new ClientModService(settingsService, settings);
        var autoJoin  = new AutoJoinService(settings);
        var launch    = new LaunchService(pak, eac);
        var eos       = new EosService();

        var serverVm   = new ServerStatusViewModel(eos, autoJoin);
        var modInfoVm  = new ModInfoViewModel(clientMod, serverVm);
        var settingsVm = new SettingsViewModel(settingsService, settings, pak, eac, clientMod);
        var mainVm     = new MainViewModel(launch, gamePath, eac, clientMod, autoJoin, settings, settingsService, modInfoVm, settingsVm);

        var window = new MainWindow(mainVm);
        window.Show();

        // Initialize EOS in the background — UI is already visible
        _ = serverVm.InitializeAsync();
    }
}
