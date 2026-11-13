using System.Windows;

namespace Vault.App.Services;

public sealed class DialogService : IDialogService
{
    private const string Title = "Password Wallet";

    public bool Confirm(string message)
    {
        var owner = Application.Current?.MainWindow;
        var result = owner is { IsLoaded: true }
            ? MessageBox.Show(owner, message, Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            : MessageBox.Show(message, Title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        return result == MessageBoxResult.Yes;
    }
}
