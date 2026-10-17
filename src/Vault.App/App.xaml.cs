using System.Windows;
using Vault.App.Services;
using Vault.App.ViewModels;

namespace Vault.App;

public partial class App : Application
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(3);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"Ocorreu um erro inesperado:\n\n{args.Exception.Message}",
                "Password Wallet", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        var services = new AppServices(
            new FileDialogService(),
            new KeyfileLocator(),
            new ClipboardService());
        var idle = new IdleLockService(IdleTimeout);

        var window = new MainWindow(idle)
        {
            DataContext = new MainViewModel(services, idle)
        };
        window.Show();
        idle.Reset();
    }
}
