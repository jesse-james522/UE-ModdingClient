using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrionLauncher.Services;

namespace OrionLauncher.ViewModels;

public partial class ServerStatusViewModel : ObservableObject
{
    private readonly ReachabilityService _reach;
    private readonly AutoJoinService     _autoJoin;
    private System.Timers.Timer?         _timer;
    private readonly Dispatcher          _dispatcher = Application.Current.Dispatcher;
    private int                          _busy;

    [ObservableProperty] private string _serverName = "—";
    [ObservableProperty] private string _statusLine = "Checking server...";
    [ObservableProperty] private bool   _isOnline;
    [ObservableProperty] private bool   _isLoading = true;

    public ServerStatusViewModel(ReachabilityService reach, AutoJoinService autoJoin)
    {
        _reach    = reach;
        _autoJoin = autoJoin;
    }

    public async Task InitializeAsync()
    {
        _timer          = new System.Timers.Timer(30_000) { AutoReset = true };
        _timer.Elapsed += async (_, _) => await RefreshAsync();
        _timer.Start();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task Refresh() => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        _dispatcher.Invoke(() => IsLoading = true);
        try
        {
            var ip = await _autoJoin.FetchServerIpAsync();
            if (string.IsNullOrWhiteSpace(ip))
            {
                Set(false, "—", "No server IP configured");
                return;
            }

            var result = await _reach.CheckAsync(ip);
            var host   = ip.Split(':')[0];
            switch (result)
            {
                case Reachability.PortOpen:
                    Set(true, host, "Online");
                    break;
                case Reachability.HostUp:
                    Set(true, host, "Online — host responding");
                    break;
                default:
                    Set(false, host, "Offline / unreachable");
                    break;
            }
        }
        catch (Exception ex)
        {
            Set(false, "—", $"Check failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private void Set(bool online, string name, string line)
    {
        _dispatcher.Invoke(() =>
        {
            IsOnline   = online;
            ServerName = name;
            StatusLine = line;
            IsLoading  = false;
        });
    }
}
