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
            KeyfilePath = AppPaths.KeyfilePathFor(value);
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

            var newRecovery = await Task.Run(() => Rekey(session, pass, keyfilePath, vaultPath));

            Session = null;
            _shell.ShowRecoveryKey(newRecovery, () => _shell.ShowVault(session, vaultPath));
        }
        catch (KeyfileSwapException ex)
        {
            Error = $"O cofre foi salvo, mas não foi possível trocar o arquivo-chave antigo ({ex.Reason}). " +
                    $"O novo arquivo-chave está em {ex.NewPath}: renomeie-o para {Keyfile.FileName}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = $"Não foi possível gravar: {ex.Message}";
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            Error = $"Caminho inválido: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string Rekey(VaultSession session, string pass, string keyfilePath, string vaultPath)
    {
        var folder = Path.GetDirectoryName(keyfilePath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        var replacing = File.Exists(keyfilePath);
        var writtenPath = replacing ? keyfilePath + ".new" : keyfilePath;
        if (replacing) File.Delete(writtenPath);

        var keyfile = Keyfile.Create(writtenPath);
        string recovery;
        try
        {
            recovery = session.Rekey(pass, keyfile);
            VaultStorage.SaveAtomic(vaultPath, session.Save());
        }
        catch
        {
            File.Delete(writtenPath);
            throw;
        }

        if (replacing)
        {
            try
            {
                File.Replace(writtenPath, keyfilePath, keyfilePath + ".old");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new KeyfileSwapException(writtenPath, ex.Message);
            }
        }
        return recovery;
    }

    private sealed class KeyfileSwapException : Exception
    {
        public string NewPath { get; }
        public string Reason { get; }

        public KeyfileSwapException(string newPath, string reason)
        {
            NewPath = newPath;
            Reason = reason;
        }
    }

    private bool CanRun() => !IsBusy;

    public void Dispose() => Session?.Dispose();
}
