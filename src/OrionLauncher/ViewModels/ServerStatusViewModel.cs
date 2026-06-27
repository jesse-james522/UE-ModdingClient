using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrionLauncher.Services;

namespace OrionLauncher.ViewModels;

public partial class ServerStatusViewModel : ObservableObject
{
    private readonly EosService      _eos;
    private readonly AutoJoinService _autoJoin;
    private System.Timers.Timer?     _timer;
    private readonly Dispatcher      _dispatcher = Application.Current.Dispatcher;

    [ObservableProperty] private string _serverName     = "—";
    [ObservableProperty] private string _playerCount    = "—";
    [ObservableProperty] private string _statusLine     = "Connecting to EOS...";
    [ObservableProperty] private bool   _isOnline;
    [ObservableProperty] private bool   _isLoading       = true;

    public ServerStatusViewModel(EosService eos, AutoJoinService autoJoin)
    {
        _eos      = eos;
        _autoJoin = autoJoin;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _eos.InitializeAsync();
            await _eos.ConnectAsync();

            _timer          = new System.Timers.Timer(30_000);
            _timer.Elapsed += async (_, _) => await RefreshAsync();
            _timer.AutoReset = true;
            _timer.Start();

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            SetStatus(false, "—", "—", $"EOS unavailable: {ex.Message}", false);
        }
    }

    [RelayCommand]
    private async Task Refresh() => await RefreshAsync();

    private async Task RefreshAsync()
    {
        _dispatcher.Invoke(() => IsLoading = true);
        try
        {
            var ip      = await _autoJoin.FetchServerIpAsync();
            var servers = await _eos.SearchSessionsAsync();

            var targetHost = ip?.Split(':')[0];

            var match = targetHost != null
                ? servers.FirstOrDefault(s =>
                      s.HostAddress.Contains(targetHost, StringComparison.OrdinalIgnoreCase) ||
                      s.Attributes.Values.Any(v => v.Contains(targetHost, StringComparison.OrdinalIgnoreCase)))
                : null;

            match ??= servers.FirstOrDefault();

            if (match != null)
            {
                var name    = string.IsNullOrEmpty(match.Name) ? "Thenyaw Server" : match.Name;
                var players = $"{match.PlayerCount} / {match.MaxPlayers}";
                SetStatus(true, name, players, $"Online  •  {match.PlayerCount} / {match.MaxPlayers} players", false);
            }
            else
            {
                SetStatus(false, "—", "—", "Server offline or not found in EOS", false);
            }
        }
        catch (Exception ex)
        {
            SetStatus(false, "—", "—", $"Query failed: {ex.Message}", false);
        }
    }

    private void SetStatus(bool online, string name, string players, string line, bool loading)
    {
        _dispatcher.Invoke(() =>
        {
            IsOnline    = online;
            ServerName  = name;
            PlayerCount = players;
            StatusLine  = line;
            IsLoading   = loading;
        });
    }
}
