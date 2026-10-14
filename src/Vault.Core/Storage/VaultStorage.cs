namespace Vault.Core.Storage;

public static class VaultStorage
{
    public static void SaveAtomic(string path, byte[] bytes)
    {
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        if (File.Exists(path))
            File.Replace(temp, path, path + ".bak");
        else
            File.Move(temp, path);
    }

    public static byte[] Load(string path) => File.ReadAllBytes(path);
}
