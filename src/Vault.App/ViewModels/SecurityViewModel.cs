using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Services;
using Vault.Core;
using Vault.Core.Storage;

namespace Vault.App.ViewModels;

public partial class SecurityViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly VaultSession _session;
    private readonly string _vaultPath;
    private readonly Action _close;

    public ObservableCollection<DriveOption> Drives { get; } = new();

    [ObservableProperty]
    private string _currentPassphrase = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackupWarning))]
    private string _currentKeyfilePath = "";

    [ObservableProperty]
    private string _newPassphrase = "";

    [ObservableProperty]
    private string _confirmation = "";

    [ObservableProperty]
    private bool _changeKeyfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewKeyfileWarning))]
    private DriveOption? _selectedNewDrive;

    [ObservableProperty]
    private string _newKeyfilePath = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackupWarning))]
    private DriveOption? _selectedBackupDrive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackupWarning))]
    private string _backupKeyfilePath = "";

    [ObservableProperty]
    private string _error = "";

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyfileCountText))]
    private int _keyfileCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangeCredentialsCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddBackupCommand))]
    private bool _isBusy;

    public string NewKeyfileWarning => SelectedNewDrive?.Warning ?? "";

    public string KeyfileCountText =>
        KeyfileCount == 1
            ? "Só um arquivo-chave abre este cofre."
            : $"{KeyfileCount} arquivos-chave abrem este cofre (o principal e {KeyfileCount - 1} reserva).";

    public string BackupWarning
    {
        get
        {
            if (BackupKeyfilePath.Length == 0) return "";
            if (File.Exists(BackupKeyfilePath))
                return "Já existe um arquivo-chave neste local. Escolha outro pendrive ou pasta.";
            if (SameDrive(BackupKeyfilePath, CurrentKeyfilePath))
                return "Este é o mesmo disco do arquivo-chave atual: o reserva deve ficar em outro pendrive.";
            return SelectedBackupDrive?.Warning ?? "";
        }
    }

    public SecurityViewModel(AppServices services, VaultSession session, string vaultPath, Action close)
    {
        _services = services;
        _session = session;
        _vaultPath = vaultPath;
        _close = close;
        KeyfileCount = session.KeyfileCount;
        CurrentKeyfilePath = services.Keyfiles.FindKeyfile() ?? "";
        RefreshDrives();
    }

    partial void OnSelectedNewDriveChanged(DriveOption? value)
    {
        if (value is not null) NewKeyfilePath = AppPaths.KeyfilePathFor(value);
    }

    partial void OnSelectedBackupDriveChanged(DriveOption? value)
    {
        if (value is not null) BackupKeyfilePath = AppPaths.KeyfilePathFor(value);
    }

    [RelayCommand]
    private void RefreshDrives()
    {
        Drives.Clear();
        foreach (var drive in _services.Keyfiles.AvailableDrives())
            Drives.Add(drive);
        SelectedNewDrive = Drives.FirstOrDefault();
        SelectedBackupDrive = Drives.FirstOrDefault(d => !SameDrive(d.Root, CurrentKeyfilePath)) ?? Drives.FirstOrDefault();
    }

    [RelayCommand]
    private void BrowseCurrentKeyfile()
    {
        var path = _services.Files.PickKeyfileToOpen();
        if (path is not null) CurrentKeyfilePath = path;
    }

    [RelayCommand]
    private void BrowseNewKeyfile()
    {
        var path = _services.Files.PickKeyfileToCreate();
        if (path is not null) NewKeyfilePath = path;
    }

    [RelayCommand]
    private void BrowseBackupKeyfile()
    {
        var path = _services.Files.PickKeyfileToCreate();
        if (path is not null) BackupKeyfilePath = path;
    }

    [RelayCommand]
    private void Close()
    {
        Clear();
        _close();
    }

    public void Clear()
    {
        CurrentPassphrase = "";
        NewPassphrase = "";
        Confirmation = "";
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task ChangeCredentialsAsync()
    {
        Error = "";
        Message = "";
        if (!PassphrasePolicy.IsAcceptable(NewPassphrase))
        {
            Error = $"A nova frase precisa ter pelo menos {PassphrasePolicy.MinLength} caracteres.";
            return;
        }
        if (NewPassphrase != Confirmation)
        {
            Error = "As novas frases não conferem.";
            return;
        }
        if (ChangeKeyfile && string.IsNullOrWhiteSpace(NewKeyfilePath))
        {
            Error = "Escolha onde gravar o novo arquivo-chave.";
            return;
        }

        var hadBackups = _session.KeyfileCount > 1;
        var changeKeyfile = ChangeKeyfile;
        var newPass = NewPassphrase;
        var newKeyfilePath = NewKeyfilePath;
        var notice = await Run((current, pass) =>
        {
            void Persist(byte[] bytes) => VaultStorage.SaveAtomic(_vaultPath, bytes);
            if (!changeKeyfile)
            {
                _session.ChangeCredentials(pass, current, newPass, current, Persist);
                return null;
            }
            return KeyfileWriter.CreateThenSwap(newKeyfilePath, keyfile =>
                _session.ChangeCredentials(pass, current, newPass, keyfile, Persist));
        });
        if (notice is null && Error.Length > 0) return;

        Clear();
        KeyfileCount = _session.KeyfileCount;
        Message = (changeKeyfile
                      ? "Pronto. Da próxima vez, use a nova frase e o novo arquivo-chave."
                      : "Pronto. Da próxima vez, use a nova frase.")
                  + " A chave de recuperação continua a mesma."
                  + (hadBackups ? " Os pendrives reserva deixaram de valer: cadastre-os de novo." : "");
        if (notice is not null) Error = notice;
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task AddBackupAsync()
    {
        Error = "";
        Message = "";
        if (string.IsNullOrWhiteSpace(BackupKeyfilePath))
        {
            Error = "Escolha onde gravar o arquivo-chave reserva.";
            return;
        }
        if (File.Exists(BackupKeyfilePath))
        {
            Error = "Já existe um arquivo-chave neste local. Escolha outro pendrive ou pasta.";
            return;
        }

        var backupPath = BackupKeyfilePath;
        await Run((current, pass) =>
        {
            var folder = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            var backup = Keyfile.Create(backupPath);
            try
            {
                _session.AddBackupKeyfile(pass, current, backup, bytes => VaultStorage.SaveAtomic(_vaultPath, bytes));
            }
            catch
            {
                File.Delete(backupPath);
                throw;
            }
            return null;
        });
        if (Error.Length > 0) return;

        CurrentPassphrase = "";
        KeyfileCount = _session.KeyfileCount;
        OnPropertyChanged(nameof(BackupWarning));
        Message = $"Pendrive reserva cadastrado em {backupPath}. Ele abre o cofre com a mesma frase.";
    }

    private async Task<string?> Run(Func<byte[], string, string?> work)
    {
        if (!File.Exists(CurrentKeyfilePath))
        {
            Error = "Insira o pendrive atual ou escolha o arquivo-chave atual.";
            return null;
        }

        IsBusy = true;
        try
        {
            var currentPath = CurrentKeyfilePath;
            var pass = CurrentPassphrase;
            return await Task.Run(() =>
            {
                var current = Keyfile.Read(currentPath);
                if (!_session.VerifyCredentials(pass, current))
                    throw new WrongCredentialsException();
                return work(current, pass);
            });
        }
        catch (WrongCredentialsException)
        {
            Error = "A frase atual ou o arquivo-chave atual estão incorretos.";
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = $"Não foi possível gravar: {ex.Message} O cofre não foi alterado.";
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            Error = $"Caminho inválido: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
        return null;
    }

    private bool CanRun() => !IsBusy;

    private static bool SameDrive(string a, string b) =>
        a.Length > 0 && b.Length > 0 &&
        string.Equals(Path.GetPathRoot(a), Path.GetPathRoot(b), StringComparison.OrdinalIgnoreCase);
}
