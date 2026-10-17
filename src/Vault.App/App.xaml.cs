using System.Windows;
using Vault.App.Services;
using Vault.App.ViewModels;

namespace Vault.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new AppServices(
            new FileDialogService(),
            new KeyfileLocator(),
            new ClipboardService());

        var window = new MainWindow
        {
            DataContext = new MainViewModel(services)
        };
        window.Show();
    }
}
