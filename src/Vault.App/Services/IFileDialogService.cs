namespace Vault.App.Services;

public interface IFileDialogService
{
    string? PickVaultToOpen();
    string? PickVaultToCreate();
    string? PickKeyfileToOpen();
    string? PickKeyfileToCreate();
}
