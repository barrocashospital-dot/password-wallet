using System.Windows;
using Vault.App.Services;
using Vault.App.ViewModels;

namespace Vault.App;

public partial class App : Application
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(3);

    private AppServices? _services;
    private SystemLockService? _systemLock;

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

        _services = new AppServices(
            new FileDialogService(),
            new KeyfileLocator(),
            new ClipboardService(),
            new DialogService());
        var idle = new IdleLockService(IdleTimeout);
        _systemLock = new SystemLockService();

        var window = new MainWindow(idle)
        {
            DataContext = new MainViewModel(_services, idle, _systemLock)
        };
        window.Show();
        idle.Reset();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _systemLock?.Dispose();
        _services?.Clipboard.ClearNow();
        base.OnExit(e);
    }
}
