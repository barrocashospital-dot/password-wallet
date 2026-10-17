using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.Core.Crypto;

namespace Vault.App.ViewModels;

public partial class ShowRecoveryKeyViewModel : ObservableObject
{
    private readonly Action _onConfirmed;

    public string KeyText { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private string _typedBack = "";

    public ShowRecoveryKeyViewModel(string keyText, Action onConfirmed)
    {
        KeyText = keyText;
        _onConfirmed = onConfirmed;
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private void Continue() => _onConfirmed();

    private bool CanContinue() =>
        RecoveryKey.TryParse(TypedBack, out var typed) &&
        RecoveryKey.TryParse(KeyText, out var real) &&
        typed.AsSpan().SequenceEqual(real);
}
