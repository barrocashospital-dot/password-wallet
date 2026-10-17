namespace Vault.App.Services;

public sealed record AppServices(
    IFileDialogService Files,
    IKeyfileLocator Keyfiles,
    IClipboardService Clipboard);
