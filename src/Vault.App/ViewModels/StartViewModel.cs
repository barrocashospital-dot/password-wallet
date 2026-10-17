using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Services;

namespace Vault.App.ViewModels;

public partial class StartViewModel : ObservableObject
{
    private readonly IShell _shell;
    private readonly AppServices _services;

    public StartViewModel(IShell shell, AppServices services)
    {
        _shell = shell;
        _services = services;
    }

    [RelayCommand]
    private void CreateVault() => _shell.ShowCreate();
}
