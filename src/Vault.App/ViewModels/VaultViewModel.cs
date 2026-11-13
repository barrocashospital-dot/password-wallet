using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Services;
using Vault.Core;
using Vault.Core.Crypto;
using Vault.Core.Model;
using Vault.Core.Storage;

namespace Vault.App.ViewModels;

public partial class VaultViewModel : ObservableObject, IDisposable
{
    private readonly IShell _shell;
    private readonly AppServices _services;
    private readonly VaultSession _session;
    private readonly string _vaultPath;

    public ObservableCollection<EntryViewModel> Entries { get; } = new();

    public ICollectionView EntriesView { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private EntryViewModel? _selected;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HidePassword))]
    private bool _showPassword;

    [ObservableProperty]
    private string _searchText = "";

    public string SaveError { get; private set; } = "";

    public bool HasSelection => Selected is not null;
    public bool HidePassword => !ShowPassword;

    public VaultViewModel(IShell shell, AppServices services, VaultSession session, string vaultPath)
    {
        _shell = shell;
        _services = services;
        _session = session;
        _vaultPath = vaultPath;

        foreach (var entry in session.Entries)
            Entries.Add(Wrap(entry));

        EntriesView = CollectionViewSource.GetDefaultView(Entries);
        EntriesView.Filter = item => item is EntryViewModel entry && Matches(entry);
    }

    partial void OnSelectedChanged(EntryViewModel? value) => ShowPassword = false;

    partial void OnSearchTextChanged(string value)
    {
        EntriesView.Refresh();
        if (Selected is not null && !Matches(Selected))
            Selected = null;
    }

    private bool Matches(EntryViewModel entry)
    {
        var term = SearchText.Trim();
        return term.Length == 0
               || entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
               || entry.Username.Contains(term, StringComparison.OrdinalIgnoreCase)
               || entry.Url.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private EntryViewModel Wrap(Entry entry)
    {
        var vm = new EntryViewModel(entry);
        vm.Changed += () => IsDirty = true;
        return vm;
    }

    [RelayCommand]
    private void Add()
    {
        SearchText = "";
        var entry = new Entry { Title = "Nova entrada" };
        _session.Entries.Add(entry);
        var vm = Wrap(entry);
        Entries.Add(vm);
        Selected = vm;
        IsDirty = true;
    }

    [RelayCommand]
    private void Delete()
    {
        var target = Selected;
        if (target is null) return;

        if (!_services.Dialogs.Confirm($"Excluir a entrada \"{target.Title}\"? Isso não pode ser desfeito depois de salvar."))
            return;

        _session.Entries.Remove(target.Model);
        Entries.Remove(target);
        Selected = null;
        IsDirty = true;
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            VaultStorage.SaveAtomic(_vaultPath, _session.Save());
            IsDirty = false;
            SaveError = "";
            Status = "Salvo.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveError = ex.Message;
            Status = $"Erro ao salvar (o pendrive/disco está disponível?): {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyUsername()
    {
        if (Selected is null) return;
        _services.Clipboard.CopySensitive(Selected.Username);
        Status = "Usuário copiado · apagado em 15 s";
    }

    [RelayCommand]
    private void CopyPassword()
    {
        if (Selected is null) return;
        _services.Clipboard.CopySensitive(Selected.Password);
        Status = "Senha copiada · apagada em 15 s";
    }

    [RelayCommand]
    private void GeneratePassword()
    {
        if (Selected is not null)
            Selected.Password = PasswordGenerator.Generate();
    }

    [RelayCommand]
    private void Lock()
    {
        SaveIfDirty();
        if (IsDirty) return;

        LockAndLeave(null);
    }

    public void ForceLock()
    {
        try
        {
            SaveIfDirty();
        }
        catch (Exception)
        {
        }

        LockAndLeave(IsDirty ? SaveRescueCopy() : null);
    }

    private string SaveRescueCopy()
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension(_vaultPath);
            var path = Path.Combine(AppPaths.DefaultFolder, $"{name}.nao-salvo-{DateTime.Now:yyyyMMdd-HHmmss}.vault");
            Directory.CreateDirectory(AppPaths.DefaultFolder);
            VaultStorage.SaveAtomic(path, _session.Save());
            return $"Não foi possível salvar no local original. Uma cópia com as últimas alterações foi gravada em: {path}";
        }
        catch (Exception)
        {
            return "O cofre foi bloqueado por inatividade e as últimas alterações não puderam ser salvas.";
        }
    }

    private void LockAndLeave(string? notice)
    {
        _session.Lock();
        Entries.Clear();
        Selected = null;
        _services.Clipboard.ClearNow();
        _shell.ShowUnlock(_vaultPath, notice);
    }

    public void SaveIfDirty()
    {
        if (IsDirty && !_session.IsLocked)
            Save();
    }

    public void Dispose() => _session.Lock();
}
