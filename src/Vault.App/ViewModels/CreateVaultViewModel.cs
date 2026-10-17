using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Services;
using Vault.Core;
using Vault.Core.Storage;

namespace Vault.App.ViewModels;

public partial class CreateVaultViewModel : ObservableObject
{
    private readonly IShell _shell;
    private readonly AppServices _services;

    public ObservableCollection<string> Drives { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PassphraseHint))]
    private string _passphrase = "";

    [ObservableProperty]
    private string _confirmation = "";

    [ObservableProperty]
    private string? _selectedDrive;

    [ObservableProperty]
    private string _keyfilePath = "";

    [ObservableProperty]
    private string _vaultPath = "";

    [ObservableProperty]
    private string _error = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    private bool _isBusy;

    public string PassphraseHint =>
        $"{Passphrase.Length} de {PassphrasePolicy.MinLength} caracteres (mínimo)";

    public CreateVaultViewModel(IShell shell, AppServices services)
    {
        _shell = shell;
        _services = services;
        RefreshDrives();
    }

    partial void OnSelectedDriveChanged(string? value)
    {
        if (value is not null)
            KeyfilePath = Path.Combine(value, Keyfile.FileName);
    }

    [RelayCommand]
    private void RefreshDrives()
    {
        Drives.Clear();
        foreach (var root in _services.Keyfiles.RemovableRoots())
            Drives.Add(root);
        SelectedDrive = Drives.FirstOrDefault();
    }

    [RelayCommand]
    private void BrowseKeyfile()
    {
        var path = _services.Files.PickKeyfileToCreate();
        if (path is not null) KeyfilePath = path;
    }

    [RelayCommand]
    private void BrowseVault()
    {
        var path = _services.Files.PickVaultToCreate();
        if (path is not null) VaultPath = path;
    }

    [RelayCommand]
    private void Back() => _shell.ShowStart();

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        Error = "";
        if (!PassphrasePolicy.IsAcceptable(Passphrase))
        {
            Error = $"A frase precisa ter pelo menos {PassphrasePolicy.MinLength} caracteres.";
            return;
        }
        if (Passphrase != Confirmation)
        {
            Error = "As frases não conferem.";
            return;
        }
        if (string.IsNullOrWhiteSpace(KeyfilePath))
        {
            Error = "Escolha onde gravar o arquivo-chave (de preferência, no pendrive).";
            return;
        }
        if (string.IsNullOrWhiteSpace(VaultPath))
        {
            Error = "Escolha onde gravar o arquivo do cofre.";
            return;
        }

        IsBusy = true;
        try
        {
            var pass = Passphrase;
            var keyfilePath = KeyfilePath;
            var vaultPath = VaultPath;

            var (session, recovery) = await Task.Run(() =>
            {
                var keyfile = Keyfile.Create(keyfilePath);
                try
                {
                    var created = VaultSession.Create(pass, keyfile);
                    VaultStorage.SaveAtomic(vaultPath, created.Session.Save());
                    return created;
                }
                catch
                {
                    File.Delete(keyfilePath);
                    throw;
                }
            });

            Passphrase = "";
            Confirmation = "";
            _shell.ShowRecoveryKey(recovery, () => _shell.ShowVault(session, vaultPath));
        }
        catch (IOException ex)
        {
            Error = $"Não foi possível gravar os arquivos (o arquivo-chave já existe?): {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            Error = $"Sem permissão para gravar: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCreate() => !IsBusy;
}
