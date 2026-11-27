using System.Security.Cryptography;

namespace Vault.Core.Crypto;

public static class RecoveryKey
{
    public const int KeySize = 32;
    public const int WordCount = 24;
    private const int ChecksumSize = 2;
    private const int GroupSize = 5;

    public static byte[] Generate() => SecureRandom.Bytes(KeySize);

    public static string Format(byte[] key) => Mnemonic.Portuguese.Encode(key);

    public static string FormatLegacy(byte[] key)
    {
        var payload = key.Concat(Checksum(key)).ToArray();
        var text = Base32.Encode(payload);

        var groups = new List<string>();
        for (var i = 0; i < text.Length; i += GroupSize)
            groups.Add(text.Substring(i, Math.Min(GroupSize, text.Length - i)));
        return string.Join("-", groups);
    }

    public static bool TryParse(string text, out byte[] key)
    {
        if (Mnemonic.Tokenize(text).Count == WordCount)
            return Mnemonic.Portuguese.TryDecode(text, out key, out _, WordCount) && key.Length == KeySize;
        return TryParseLegacy(text, out key);
    }

    public static string Problem(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || TryParse(text, out _)) return "";
        if (LooksLegacy(text))
            return "A chave antiga (com letras e números) está incompleta ou tem algum caractere errado.";
        Mnemonic.Portuguese.TryDecode(text, out _, out var problem, WordCount);
        return problem;
    }

    private static bool LooksLegacy(string text)
    {
        var tokens = text.Split(new[] { ' ', '-', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (!tokens.Any(t => t.Any(char.IsLetter) && t.Any(char.IsDigit))) return false;
        if (tokens.Length == 1) return true;
        return tokens.Take(tokens.Length - 1).All(t => t.Length == GroupSize) && tokens[^1].Length <= GroupSize;
    }

    private static bool TryParseLegacy(string text, out byte[] key)
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

        if (!CryptographicOperations.FixedTimeEquals(checksum, Checksum(candidate))) return false;

        key = candidate;
        return true;
    }

    private static byte[] Checksum(byte[] key) => SHA256.HashData(key)[..ChecksumSize];
}
