using System.Windows.Threading;

namespace Vault.App.Services;

public sealed class IdleLockService
{
    private readonly DispatcherTimer _timer;

    public event Action? Idle;

    public IdleLockService(TimeSpan timeout)
    {
        _timer = new DispatcherTimer { Interval = timeout };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Idle?.Invoke();
        };
    }

    public void Reset()
    {
        _timer.Stop();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();
}
