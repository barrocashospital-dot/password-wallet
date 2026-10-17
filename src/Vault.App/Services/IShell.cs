using Vault.Core;

namespace Vault.App.Services;

public interface IShell
{
    void ShowStart();
    void ShowCreate();
    void ShowUnlock(string vaultPath);
    void ShowRecover();
    void ShowRecoveryKey(string keyText, Action onConfirmed);
    void ShowVault(VaultSession session, string vaultPath);
}
