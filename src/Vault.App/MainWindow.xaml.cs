using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Vault.App.Services;
using Vault.App.ViewModels;

namespace Vault.App;

public partial class MainWindow : Window
{
    private const int DwmUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public MainWindow(IdleLockService idle)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => UseDarkTitleBar();

        PreviewMouseMove += (_, _) => idle.Reset();
        PreviewMouseDown += (_, _) => idle.Reset();
        PreviewKeyDown += (_, _) => idle.Reset();

        Closing += (_, _) => (DataContext as MainViewModel)?.OnClosing();
    }

    private void UseDarkTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var enabled = 1;
        DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
    }
}
