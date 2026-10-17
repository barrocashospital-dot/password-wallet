using System.IO;
using Vault.Core.Storage;

namespace Vault.App.Services;

public sealed class KeyfileLocator : IKeyfileLocator
{
    public IReadOnlyList<string> RemovableRoots() =>
        DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
            .Select(d => d.RootDirectory.FullName)
            .ToList();

    public string? FindKeyfile() => Keyfile.Find(RemovableRoots());
}
