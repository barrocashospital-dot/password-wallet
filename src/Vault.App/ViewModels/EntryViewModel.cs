using CommunityToolkit.Mvvm.ComponentModel;
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
}
