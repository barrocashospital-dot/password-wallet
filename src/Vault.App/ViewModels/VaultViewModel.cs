using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Services;
using Vault.Core;
using Vault.Core.Crypto;
using Vault.Core.Import;
using Vault.Core.Model;
using Vault.Core.Storage;

namespace Vault.App.ViewModels;

public partial class VaultViewModel : ObservableObject, IDisposable
{
    private readonly IShell _shell;
    private readonly AppServices _services;
    private readonly VaultSession _session;
    private readonly string _vaultPath;
    private const long MaxImportBytes = 10 * 1024 * 1024;

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

    [ObservableProperty]
    private bool _isGeneratorOpen;

    [ObservableProperty]
    private string _strongSuggestion = "";

    [ObservableProperty]
    private string _veryStrongSuggestion = "";

    [ObservableProperty]
    private bool _onlyIssues;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHealthIssues))]
    private string _healthSummary = "";

    public bool HasHealthIssues => HealthSummary.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSecurityOpen))]
    private SecurityViewModel? _security;

    public bool IsSecurityOpen => Security is not null;

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
        RefreshHealth();
    }

    partial void OnOnlyIssuesChanged(bool value) => OnSearchTextChanged(SearchText);

    private void RefreshHealth()
    {
        var report = PasswordHealth.Analyze(Entries.Select(e => e.Model));
        foreach (var entry in Entries)
            entry.Issue = report.TryGetValue(entry.Model.Id, out var issue) ? issue : PasswordIssue.None;

        var count = Entries.Count(e => e.HasIssue);
        HealthSummary = count switch
        {
            0 => "",
            1 => "1 senha fraca ou repetida",
            _ => $"{count} senhas fracas ou repetidas"
        };
    }

    partial void OnSelectedChanged(EntryViewModel? value)
    {
        ShowPassword = false;
        CloseGenerator();
    }

    partial void OnSearchTextChanged(string value)
    {
        EntriesView.Refresh();
        if (Selected is not null && !Matches(Selected))
            Selected = null;
    }

    private bool Matches(EntryViewModel entry)
    {
        if (OnlyIssues && !entry.HasIssue) return false;
        var term = SearchText.Trim();
        return term.Length == 0
               || entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
               || entry.Username.Contains(term, StringComparison.OrdinalIgnoreCase)
               || entry.Url.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private EntryViewModel Wrap(Entry entry)
    {
        var vm = new EntryViewModel(entry);
        vm.Changed += () =>
        {
            IsDirty = true;
            RefreshHealth();
        };
        return vm;
    }

    [RelayCommand]
    private void Add()
    {
        SearchText = "";
        OnlyIssues = false;
        var entry = new Entry { Title = "Nova entrada" };
        _session.Entries.Add(entry);
        var vm = Wrap(entry);
        Entries.Add(vm);
        Selected = vm;
        IsDirty = true;
        RefreshHealth();
    }

    [RelayCommand]
    private void Import()
    {
        var path = _services.Files.PickCsvToOpen();
        if (path is null) return;

        CsvImportResult result;
        try
        {
            if (new FileInfo(path).Length > MaxImportBytes)
            {
                Status = "O arquivo é grande demais para importar (máximo de 10 MB).";
                return;
            }
            result = CsvImporter.Parse(File.ReadAllText(path));
        }
        catch (CsvImportException ex)
        {
            Status = ex.Message;
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = $"Não foi possível ler o arquivo: {ex.Message}";
            return;
        }

        var fresh = result.Entries.Where(e => !Entries.Any(existing => SameLogin(existing.Model, e))).ToList();
        var repeated = result.Entries.Count - fresh.Count;
        if (fresh.Count == 0)
        {
            Status = "Nada novo para importar: todas as entradas já estão no cofre.";
            return;
        }

        var ignored = repeated + result.Skipped;
        var question = $"Importar {fresh.Count} entradas ({result.Format}) de \"{Path.GetFileName(path)}\"?" +
                       ignored switch
                       {
                           0 => "",
                           1 => " 1 será ignorada (repetida ou sem dados).",
                           _ => $" {ignored} serão ignoradas (repetidas ou sem dados)."
                       };
        if (!_services.Dialogs.Confirm(question)) return;

        SearchText = "";
        OnlyIssues = false;
        foreach (var entry in fresh)
        {
            _session.Entries.Add(entry);
            Entries.Add(Wrap(entry));
        }
        IsDirty = true;
        RefreshHealth();
        Status = $"{fresh.Count} entradas importadas. Salve o cofre e apague o arquivo CSV: ele guarda as senhas sem proteção.";
    }

    private static bool SameLogin(Entry a, Entry b) =>
        a.Username == b.Username && a.Password == b.Password &&
        (a.Url == b.Url || a.Title == b.Title);

    [RelayCommand]
    private void OpenSecurity()
    {
        SaveIfDirty();
        if (IsDirty) return;
        Security = new SecurityViewModel(_services, _session, _vaultPath, CloseSecurity);
    }

    private void CloseSecurity()
    {
        Security?.Clear();
        Security = null;
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
        RefreshHealth();
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
        if (Selected is null) return;
        if (IsGeneratorOpen)
        {
            CloseGenerator();
            return;
        }
        RegenerateStrong();
        RegenerateVeryStrong();
        IsGeneratorOpen = true;
    }

    [RelayCommand]
    private void RegenerateStrong() =>
        StrongSuggestion = PasswordGenerator.Generate(PasswordGenerator.StrongLength);

    [RelayCommand]
    private void RegenerateVeryStrong() =>
        VeryStrongSuggestion = PasswordGenerator.Generate(PasswordGenerator.VeryStrongLength);

    [RelayCommand]
    private void CopyStrongSuggestion() => CopySuggestion(StrongSuggestion);

    [RelayCommand]
    private void CopyVeryStrongSuggestion() => CopySuggestion(VeryStrongSuggestion);

    [RelayCommand]
    private void UseStrongSuggestion() => ApplySuggestion(StrongSuggestion);

    [RelayCommand]
    private void UseVeryStrongSuggestion() => ApplySuggestion(VeryStrongSuggestion);

    private void CopySuggestion(string suggestion)
    {
        if (suggestion.Length == 0) return;
        _services.Clipboard.CopySensitive(suggestion);
        Status = $"Senha de {suggestion.Length} caracteres copiada · apagada em 15 s";
    }

    [RelayCommand]
    private void CloseGenerator()
    {
        IsGeneratorOpen = false;
        StrongSuggestion = "";
        VeryStrongSuggestion = "";
    }

    private void ApplySuggestion(string suggestion)
    {
        if (Selected is null || suggestion.Length == 0) return;
        Selected.Password = suggestion;
        Status = $"Senha de {suggestion.Length} caracteres aplicada · salve para guardar";
        CloseGenerator();
    }

    [RelayCommand]
    private void Lock()
    {
        SaveIfDirty();
        if (IsDirty) return;

        LockAndLeave(null);
    }

    public void ForceLock(string reason)
    {
        try
        {
            SaveIfDirty();
        }
        catch (Exception)
        {
        }

        LockAndLeave(IsDirty ? SaveRescueCopy(reason) : null);
    }

    private string SaveRescueCopy(string reason)
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
            return $"O cofre foi bloqueado {reason} e as últimas alterações não puderam ser salvas.";
        }
    }

    private void LockAndLeave(string? notice)
    {
        CloseGenerator();
        CloseSecurity();
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
