namespace Vault.App.Services;

public interface IKeyfileLocator
{
    IReadOnlyList<string> RemovableRoots();

    string? FindKeyfile();
}
