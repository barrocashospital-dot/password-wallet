using System.IO;

namespace Vault.App.Services;

public static class AppPaths
{
    public static string DefaultFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PasswordWallet");

    public static string NextFreeVaultPath()
    {
        var path = Path.Combine(DefaultFolder, "meu-cofre.vault");
        for (var i = 2; File.Exists(path); i++)
            path = Path.Combine(DefaultFolder, $"meu-cofre-{i}.vault");
        return path;
    }
}
