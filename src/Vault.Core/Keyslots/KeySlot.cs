namespace Vault.Core.Keyslots;

public sealed class KdfInfo
{
    public string Alg { get; set; } = "argon2id";
    public int MemKiB { get; set; }
    public int Iters { get; set; }
    public int Lanes { get; set; }
}

public sealed class KeySlot
{
    public string Type { get; set; } = "";
    public KdfInfo? Kdf { get; set; }
    public byte[]? Salt { get; set; }
    public byte[] Nonce { get; set; } = Array.Empty<byte>();
    public byte[] WrappedKey { get; set; } = Array.Empty<byte>();
}