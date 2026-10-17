using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Services;
using Vault.Core;
using Vault.Core.Storage;

namespace Vault.App.ViewModels;

public partial class RecoverViewModel : ObservableObject, IDisposable
{
    private readonly IShell _shell;
    private readonly AppServices _services;

    public ObservableCollection<DriveOption> Drives { get; } = new();

    [ObservableProperty]
    private string _vaultPath = "";

    [ObservableProperty]
    private string _recoveryKeyText = "";

    [ObservableProperty]
    private string _newPassphrase = "";

    [ObservableProperty]
    private string _confirmation = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DriveWarning))]
    private DriveOption? _selectedDrive;

    [ObservableProperty]
    private string _keyfilePath = "";

    [ObservableProperty]
    private string _error = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    [NotifyCanExecuteChangedFor(nameof(FinishCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStepOne))]
    [NotifyPropertyChangedFor(nameof(IsStepTwo))]
    private VaultSession? _session;

    public bool IsStepOne => Session is null;
    public bool IsStepTwo => Session is not null;
    public string DriveWarning => SelectedDrive?.Warning ?? "";

    public RecoverViewModel(IShell shell, AppServices services)
    {
        _shell = shell;
        _services = services;
        RefreshDrives();
    }

    partial void OnSelectedDriveChanged(DriveOption? value)
    {
        if (value is not null)
            KeyfilePath = Path.Combine(value.Root, Keyfile.FileName);
    }

    [RelayCommand]
    private void RefreshDrives()
    {
        Drives.Clear();
        foreach (var drive in _services.Keyfiles.AvailableDrives())
            Drives.Add(drive);
        SelectedDrive = Drives.FirstOrDefault();
    }

    [RelayCommand]
    private void BrowseVault()
    {
        var path = _services.Files.PickVaultToOpen();
        if (path is not null) VaultPath = path;
    }

    [RelayCommand]
    private void BrowseKeyfile()
    {
        var path = _services.Files.PickKeyfileToCreate();
        if (path is not null) KeyfilePath = path;
    }

    [RelayCommand]
    private void Back() => _shell.ShowStart();

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task VerifyAsync()
    {
        Error = "";
        if (!File.Exists(VaultPath))
        {
            Error = "Escolha o arquivo do cofre.";
            return;
        }

        IsBusy = true;
        try
        {
            var path = VaultPath;
            var key = RecoveryKeyText;
            Session = await Task.Run(() => VaultSession.OpenWithRecovery(VaultStorage.Load(path), key));
        }
        catch (WrongCredentialsException)
        {
            Error = "Chave de recuperação incorreta. Confira cada caractere.";
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

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task FinishAsync()
    {
        Error = "";
        var session = Session;
        if (session is null) return;

        if (!PassphrasePolicy.IsAcceptable(NewPassphrase))
        {
            Error = $"A frase precisa ter pelo menos {PassphrasePolicy.MinLength} caracteres.";
            return;
        }
        if (NewPassphrase != Confirmation)
        {
            Error = "As frases não conferem.";
            return;
        }
        if (string.IsNullOrWhiteSpace(KeyfilePath))
        {
            Error = "Escolha onde gravar o novo arquivo-chave.";
            return;
        }

        IsBusy = true;
        try
        {
            var pass = NewPassphrase;
            var keyfilePath = KeyfilePath;
            var vaultPath = VaultPath;

            var newRecovery = await Task.Run(() =>
            {
                var keyfile = Keyfile.Create(keyfilePath);
                try
                {
                    var recovery = session.Rekey(pass, keyfile);
                    VaultStorage.SaveAtomic(vaultPath, session.Save());
                    return recovery;
                }
                catch
                {
                    File.Delete(keyfilePath);
                    throw;
                }
            });

            Session = null;
            _shell.ShowRecoveryKey(newRecovery, () => _shell.ShowVault(session, vaultPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = $"Não foi possível gravar (o arquivo-chave novo já existe?): {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRun() => !IsBusy;

    public void Dispose() => Session?.Dispose();
}
