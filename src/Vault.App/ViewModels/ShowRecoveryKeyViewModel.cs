using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Services;
using Vault.Core.Crypto;

namespace Vault.App.ViewModels;

public partial class ShowRecoveryKeyViewModel : ObservableObject
{
    private readonly Action _onConfirmed;
    private readonly IClipboardService _clipboard;

    public string KeyText { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private string _typedBack = "";

    [ObservableProperty]
    private string _copyLabel = "Copiar";

    public ShowRecoveryKeyViewModel(string keyText, Action onConfirmed, IClipboardService clipboard)
    {
        _clipboard = clipboard;
        KeyText = keyText;
        _onConfirmed = onConfirmed;
    }

    [RelayCommand]
    private void Copy()
    {
        _clipboard.CopySensitive(KeyText);
        CopyLabel = "Copiada (some em 15 s)";
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private void Continue() => _onConfirmed();

    private bool CanContinue() =>
        RecoveryKey.TryParse(TypedBack, out var typed) &&
        RecoveryKey.TryParse(KeyText, out var real) &&
        typed.AsSpan().SequenceEqual(real);
}
