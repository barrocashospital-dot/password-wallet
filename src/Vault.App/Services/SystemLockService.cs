using System.Threading;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Vault.App.Services;

public sealed class SystemLockService : IDisposable
{
    private static readonly TimeSpan InvokeTimeout = TimeSpan.FromSeconds(2);

    private readonly Dispatcher _dispatcher;

    public event Action? Locked;

    public SystemLockService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
            RaiseLocked();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
            RaiseLocked();
    }

    private void RaiseLocked()
    {
        if (_dispatcher.HasShutdownStarted) return;
        _dispatcher.Invoke(() => Locked?.Invoke(), DispatcherPriority.Send, CancellationToken.None, InvokeTimeout);
    }
}
