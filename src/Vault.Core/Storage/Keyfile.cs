using Vault.Core.Crypto;

namespace Vault.Core.Storage;

public static class Keyfile
{
    public const int Size = 32;
    public const string FileName = "vault.keyfile";

    public static byte[] Create(string path)
    {
        var bytes = SecureRandom.Bytes(Size);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
        return bytes;
    }
    public static byte[] Read (string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length != Size)
            throw new WrongCredentialsException();
        return bytes;
    }
    public static string? Find(IEnumerable <string> roots)
    {
        foreach(var root in roots)
        {
            var candidate = Path.Combine(root,FileName);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
}