using CommunityToolkit.Mvvm.ComponentModel;
using Vault.Core;
using Vault.Core.Model;

namespace Vault.App.ViewModels;

public sealed class EntryViewModel : ObservableObject
{
    public Entry Model { get; }

    public event Action? Changed;

    public EntryViewModel(Entry model) => Model = model;

    public string Title
    {
        get => Model.Title;
        set { if (SetProperty(Model.Title, value, Model, (m, v) => m.Title = v)) Changed?.Invoke(); }
    }

    public string Url
    {
        get => Model.Url;
        set { if (SetProperty(Model.Url, value, Model, (m, v) => m.Url = v)) Changed?.Invoke(); }
    }

    public string Username
    {
        get => Model.Username;
        set { if (SetProperty(Model.Username, value, Model, (m, v) => m.Username = v)) Changed?.Invoke(); }
    }

    public string Password
    {
        get => Model.Password;
        set { if (SetProperty(Model.Password, value, Model, (m, v) => m.Password = v)) Changed?.Invoke(); }
    }

    public string Notes
    {
        get => Model.Notes;
        set { if (SetProperty(Model.Notes, value, Model, (m, v) => m.Notes = v)) Changed?.Invoke(); }
    }

    private PasswordIssue _issue;

    public PasswordIssue Issue
    {
        get => _issue;
        set
        {
            if (SetProperty(ref _issue, value))
            {
                OnPropertyChanged(nameof(HasIssue));
                OnPropertyChanged(nameof(IssueText));
            }
        }
    }

    public bool HasIssue => Issue != PasswordIssue.None;

    public string IssueText
    {
        get
        {
            var parts = new List<string>();
            if (Issue.HasFlag(PasswordIssue.Weak))
                parts.Add("Senha fraca: use pelo menos 12 caracteres com letras, números e símbolos.");
            if (Issue.HasFlag(PasswordIssue.Reused))
                parts.Add("Esta senha também é usada em outra entrada.");
            return string.Join(" ", parts);
        }
    }
}
