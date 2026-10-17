using Microsoft.Win32;

namespace Vault.App.Services;

public sealed class FileDialogService : IFileDialogService
{
    private const string VaultFilter = "Cofre (*.vault)|*.vault|Todos os arquivos (*.*)|*.*";
    private const string KeyfileFilter = "Arquivo-chave (*.keyfile)|*.keyfile|Todos os arquivos (*.*)|*.*";

    public string? PickVaultToOpen() => Open(VaultFilter);

    public string? PickKeyfileToOpen() => Open(KeyfileFilter);

    public string? PickVaultToCreate() =>
        Save(VaultFilter, "meu-cofre.vault", ".vault");

    public string? PickKeyfileToCreate() =>
        Save(KeyfileFilter, "vault.keyfile", ".keyfile");

    private static string? Open(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? Save(string filter, string defaultName, string extension)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultName,
            DefaultExt = extension,
            AddExtension = true,
            OverwritePrompt = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
