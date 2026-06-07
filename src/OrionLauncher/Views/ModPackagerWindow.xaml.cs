using System.Windows;
using OrionLauncher.ViewModels;

namespace OrionLauncher.Views;

public partial class ModPackagerWindow : Window
{
    public ModPackagerWindow(ModPackagerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void OnDrop(object sender, DragEventArgs e) { }
    private void OnDragOver(object sender, DragEventArgs e) { e.Handled = true; }
}
