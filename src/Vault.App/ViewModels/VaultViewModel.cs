using System.Collections.ObjectModel;
using System.IO;
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
            Status = "Salvo.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
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

        _session.Lock();
        Entries.Clear();
        Selected = null;
        _shell.ShowStart();
    }

    public void SaveIfDirty()
    {
        if (IsDirty && !_session.IsLocked)
            Save();
    }

    public void Dispose() => _session.Lock();
}
