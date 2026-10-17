using Vault.Core;

namespace Vault.App.Services;

public interface IShell
{
    void ShowStart();
    void ShowCreate();
    void ShowRecoveryKey(string keyText, Action onConfirmed);
    void ShowVault(VaultSession session, string vaultPath);
}
