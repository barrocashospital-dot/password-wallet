using System.Security.Cryptography;

namespace Vault.Core.Crypto;

public static class RecoveryKey
{
    public const int KeySize = 32;
    private const int ChecksumSize = 2;
    private const int GroupSize = 5;

    public static byte[] Generate() => SecureRandom.Bytes(KeySize);

    public static string Format(byte[] key)
    {
        var payload = key.Concat(Checksum(key)).ToArray();
        var text = Base32.Encode(payload);

        var groups = new List<string>();
        for (var i = 0; i < text.Length; i += GroupSize){
            groups.Add(text.Substring(i, Math.Min(GroupSize, text.Length - i)));
        }
        return string.Join("-", groups);
    }
    public static bool TryParse(string text, out byte[] key)
    {
        key = Array.Empty<byte>();

        var clean = new string(text
            .Where(c => !char.IsWhiteSpace(c) && c != '-')
            .ToArray())
            .ToUpperInvariant();
        if (!Base32.TryDecode(clean, out var payload)) return false;
        if (payload.Length != KeySize + ChecksumSize) return false;

        var candidate = payload[..KeySize];
        var checksum = payload[KeySize..];

        if (!CryptographicOperations.FixedTimeEquals(checksum,Checksum(candidate))) return false;

        key = candidate;
        return true;
    }
    private static byte[] Checksum(byte[] key) => SHA256.HashData(key)[..ChecksumSize];
}
