using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Services;
using Vault.Core;
using Vault.Core.Storage;

namespace Vault.App.ViewModels;

public partial class UnlockViewModel : ObservableObject
{
    private readonly IShell _shell;
    private readonly AppServices _services;

    public string VaultPath { get; }

    [ObservableProperty]
    private string _passphrase = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyfileStatus))]
    private string _keyfilePath = "";

    [ObservableProperty]
    private string _error = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    private bool _isBusy;

    public string KeyfileStatus =>
        string.IsNullOrEmpty(KeyfilePath)
            ? "Arquivo-chave não encontrado. Insira o pendrive ou escolha o arquivo."
            : $"Arquivo-chave: {KeyfilePath}";

    public UnlockViewModel(IShell shell, AppServices services, string vaultPath)
    {
        _shell = shell;
        _services = services;
        VaultPath = vaultPath;
        KeyfilePath = services.Keyfiles.FindKeyfile() ?? "";
    }

    [RelayCommand]
    private void RefreshKeyfile() => KeyfilePath = _services.Keyfiles.FindKeyfile() ?? "";

    [RelayCommand]
    private void BrowseKeyfile()
    {
        var path = _services.Files.PickKeyfileToOpen();
        if (path is not null) KeyfilePath = path;
    }

    [RelayCommand]
    private void Back() => _shell.ShowStart();

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockAsync()
    {
        Error = "";
        if (!File.Exists(KeyfilePath))
        {
            Error = "Insira o pendrive ou escolha o arquivo-chave.";
            return;
        }

        IsBusy = true;
        try
        {
            var vaultPath = VaultPath;
            var keyfilePath = KeyfilePath;
            var pass = Passphrase;

            var session = await Task.Run(() =>
                VaultSession.Open(VaultStorage.Load(vaultPath), pass, Keyfile.Read(keyfilePath)));

            Passphrase = "";
            _shell.ShowVault(session, vaultPath);
        }
        catch (WrongCredentialsException)
        {
            Error = "Frase ou arquivo-chave incorretos.";
        }
        catch (CorruptVaultException)
        {
            Error = "O arquivo do cofre está corrompido ou foi alterado.";
        }
        catch (UnsupportedVersionException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            Error = "O arquivo do cofre não foi encontrado.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = $"Não foi possível ler o arquivo: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanUnlock() => !IsBusy;
}
