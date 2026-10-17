using System.IO;
using Vault.Core.Storage;

namespace Vault.App.Services;

public sealed class KeyfileLocator : IKeyfileLocator
{
    public IReadOnlyList<DriveOption> AvailableDrives()
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
        var result = new List<DriveOption>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType is not (DriveType.Removable or DriveType.Fixed)) continue;

            string label;
            try
            {
                if (!drive.IsReady) continue;
                label = drive.VolumeLabel;
            }
            catch (IOException)
            {
                continue;
            }

            var root = drive.RootDirectory.FullName;
            var isRemovable = drive.DriveType == DriveType.Removable;
            var isSystem = string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase);
            result.Add(new DriveOption(root, BuildLabel(root, label, isRemovable, isSystem), isRemovable, isSystem));
        }

        return result
            .OrderBy(d => d.IsSystem)
            .ThenByDescending(d => d.IsRemovable)
            .ThenBy(d => d.Root)
            .ToList();
    }

    public string? FindKeyfile() =>
        Keyfile.Find(AvailableDrives().Select(d => d.Root)) ?? Keyfile.Find([AppPaths.DefaultFolder]);

    private static string BuildLabel(string root, string volumeLabel, bool isRemovable, bool isSystem)
    {
        var name = string.IsNullOrWhiteSpace(volumeLabel) ? "Sem nome" : volumeLabel;
        var kind = isSystem ? "disco do sistema" : isRemovable ? "removível" : "disco";
        return $"{root}  ·  {name}  ({kind})";
    }
}
