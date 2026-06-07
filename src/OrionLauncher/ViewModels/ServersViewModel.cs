// Servers feature removed — stub kept so ServersView.xaml code-behind compiles.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OrionLauncher.Models;

namespace OrionLauncher.ViewModels;

public partial class ServersViewModel : ObservableObject
{
    public ObservableCollection<ServerEntry> Servers { get; } = [];
    [ObservableProperty] private ServerEntry? _selectedServer;
    [ObservableProperty] private string _statusText = "";
    public IEnumerable<string> SelectedRequiredMods => Enumerable.Empty<string>();
}
