using CommunityToolkit.Mvvm.ComponentModel;
using Vault.App.Services;
using Vault.Core;

namespace Vault.App.ViewModels;

public partial class MainViewModel : ObservableObject, IShell
{
    private readonly AppServices _services;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    public MainViewModel(AppServices services, IdleLockService idle)
    {
        _services = services;
        idle.Idle += OnIdle;
        ShowStart();
    }

    public void ShowStart() => Navigate(new StartViewModel(this, _services));

    public void ShowCreate() => Navigate(new CreateVaultViewModel(this, _services));

    public void ShowUnlock(string vaultPath) =>
        Navigate(new UnlockViewModel(this, _services, vaultPath));

    public void ShowRecover() => Navigate(new RecoverViewModel(this, _services));

    public void ShowRecoveryKey(string keyText, Action onConfirmed) =>
        Navigate(new ShowRecoveryKeyViewModel(keyText, onConfirmed, _services.Clipboard));

    public void ShowVault(VaultSession session, string vaultPath) =>
        Navigate(new VaultViewModel(this, _services, session, vaultPath));

    public void OnClosing() => (CurrentViewModel as VaultViewModel)?.SaveIfDirty();

    private void OnIdle()
    {
        switch (CurrentViewModel)
        {
            case VaultViewModel vault:
                vault.LockCommand.Execute(null);
                break;
            case RecoverViewModel { IsStepTwo: true }:
                ShowStart();
                break;
        }
    }

    private void Navigate(ObservableObject next)
    {
        var previous = CurrentViewModel;
        CurrentViewModel = next;
        (previous as IDisposable)?.Dispose();
    }
}
