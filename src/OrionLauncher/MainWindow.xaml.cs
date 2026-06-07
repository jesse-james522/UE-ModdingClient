using System.Windows;
using OrionLauncher.ViewModels;

namespace OrionLauncher;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.InitializeAsync();
    }
}
