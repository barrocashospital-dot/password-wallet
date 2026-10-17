namespace Vault.App.Services;

public interface IKeyfileLocator
{
    IReadOnlyList<DriveOption> AvailableDrives();

    string? FindKeyfile();
}
