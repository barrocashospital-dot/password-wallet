using CommunityToolkit.Mvvm.ComponentModel;
using Vault.App.Services;
using Vault.Core;

namespace Vault.App.ViewModels;

public partial class MainViewModel : ObservableObject, IShell
{
    private readonly AppServices _services;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    public MainViewModel(AppServices services, IdleLockService idle, SystemLockService systemLock)
    {
        _services = services;
        idle.Idle += LockNow;
        systemLock.Locked += LockNow;
        ShowStart();
    }

    public void ShowStart() => Navigate(new StartViewModel(this, _services));

    public void ShowCreate() => Navigate(new CreateVaultViewModel(this, _services));

    public void ShowUnlock(string vaultPath, string? notice = null) =>
        Navigate(new UnlockViewModel(this, _services, vaultPath, notice));

    public void ShowRecover() => Navigate(new RecoverViewModel(this, _services));

    public void ShowRecoveryKey(string keyText, Action onConfirmed) =>
        Navigate(new ShowRecoveryKeyViewModel(keyText, onConfirmed, _services.Clipboard));

    public void ShowVault(VaultSession session, string vaultPath) =>
        Navigate(new VaultViewModel(this, _services, session, vaultPath));

    public bool OnClosing()
    {
        if (CurrentViewModel is not VaultViewModel vault) return true;

        vault.SaveIfDirty();
        if (!vault.IsDirty) return true;

        return _services.Dialogs.Confirm(
            $"Não foi possível salvar as alterações ({vault.SaveError}). Fechar mesmo assim e perder as alterações?");
    }

    private void LockNow()
    {
        switch (CurrentViewModel)
        {
            case VaultViewModel vault:
                vault.ForceLock();
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
