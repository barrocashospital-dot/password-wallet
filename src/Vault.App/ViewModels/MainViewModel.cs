using CommunityToolkit.Mvvm.ComponentModel;
using Vault.App.Services;

namespace Vault.App.ViewModels;

public partial class MainViewModel : ObservableObject, IShell
{
    private readonly AppServices _services;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    public MainViewModel(AppServices services)
    {
        _services = services;
        ShowStart();
    }

    public void ShowStart() => Navigate(new StartViewModel(this, _services));

    private void Navigate(ObservableObject next)
    {
        var previous = CurrentViewModel;
        CurrentViewModel = next;
        (previous as IDisposable)?.Dispose();
    }
}
