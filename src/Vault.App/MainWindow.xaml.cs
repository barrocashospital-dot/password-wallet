using System.Windows;
using Vault.App.ViewModels;

namespace Vault.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += (_, _) => (DataContext as MainViewModel)?.OnClosing();
    }
}
