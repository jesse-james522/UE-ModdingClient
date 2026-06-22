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

        var modInfoVm  = new ModInfoViewModel(clientMod);
        var settingsVm = new SettingsViewModel(settingsService, settings, pak, eac, clientMod);
        var mainVm     = new MainViewModel(launch, gamePath, eac, clientMod, autoJoin, settings, settingsService, modInfoVm, settingsVm);

        var window = new MainWindow(mainVm);
        window.Show();
    }
}
