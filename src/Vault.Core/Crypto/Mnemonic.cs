using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Vault.Core.Crypto;

public sealed class Mnemonic
{
    private const int BitsPerWord = 11;
    private static readonly char[] Separators = { ' ', '\t', '\r', '\n', ',', ';', '.', '-', ')', ':' };

    private readonly Dictionary<string, int> _index;
    private readonly Dictionary<string, int> _prefixIndex;

    public static Mnemonic Portuguese { get; } = FromResource("Vault.Core.bip39-portuguese.txt");

    public IReadOnlyList<string> Words { get; }

    public Mnemonic(IReadOnlyList<string> words)
    {
        if (words.Count != 2048)
            throw new ArgumentException("A lista precisa ter 2048 palavras.", nameof(words));
        Words = words;
        _index = words.Select((w, i) => (w, i)).ToDictionary(p => p.w, p => p.i, StringComparer.Ordinal);
        _prefixIndex = words.Where(w => w.Length >= 4)
            .Select((w, i) => (Prefix: w[..4], Index: _index[w]))
            .GroupBy(p => p.Prefix)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().Index, StringComparer.Ordinal);
    }

    public static Mnemonic FromText(string text) =>
        new(text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static Mnemonic FromResource(string name)
    {
        using var stream = typeof(Mnemonic).Assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"Recurso {name} não encontrado.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return FromText(reader.ReadToEnd());
    }

    public string Encode(byte[] entropy)
    {
        if (entropy.Length is < 16 or > 32 || entropy.Length % 4 != 0)
            throw new ArgumentException("A entropia deve ter 16, 20, 24, 28 ou 32 bytes.", nameof(entropy));

        var checksumBits = entropy.Length * 8 / 32;
        var bits = ToBits(entropy).Concat(ToBits(SHA256.HashData(entropy)).Take(checksumBits)).ToArray();

        var words = new string[bits.Length / BitsPerWord];
        for (var w = 0; w < words.Length; w++)
        {
            var index = 0;
            for (var b = 0; b < BitsPerWord; b++)
                index = (index << 1) | (bits[w * BitsPerWord + b] ? 1 : 0);
            words[w] = Words[index];
        }
        return string.Join(' ', words);
    }

    public static IReadOnlyList<string> Tokenize(string text) =>
        RemoveDiacritics(text).ToLowerInvariant()
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !t.All(char.IsDigit))
            .ToList();

    public bool TryDecode(string text, out byte[] entropy, out string problem, int expectedWords = 0)
    {
        entropy = Array.Empty<byte>();
        var tokens = Tokenize(text);

        if (expectedWords > 0 && tokens.Count != expectedWords)
        {
            problem = tokens.Count < expectedWords
                ? $"Você digitou {tokens.Count} de {expectedWords} palavras."
                : $"Você digitou {tokens.Count} palavras; são só {expectedWords}.";
            return false;
        }
        if (tokens.Count is < 12 or > 24 || tokens.Count % 3 != 0)
        {
            problem = $"Quantidade de palavras inválida ({tokens.Count}).";
            return false;
        }

        var indexes = new int[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
        {
            var index = Resolve(tokens[i]);
            if (index < 0)
            {
                problem = $"A palavra {i + 1} (\"{tokens[i]}\") não está na lista.";
                return false;
            }
            indexes[i] = index;
        }

        var bits = new bool[indexes.Length * BitsPerWord];
        for (var w = 0; w < indexes.Length; w++)
            for (var b = 0; b < BitsPerWord; b++)
                bits[w * BitsPerWord + b] = ((indexes[w] >> (BitsPerWord - 1 - b)) & 1) == 1;

        var checksumBits = bits.Length / 33;
        var entropyBits = bits.Length - checksumBits;
        var candidate = new byte[entropyBits / 8];
        for (var i = 0; i < entropyBits; i++)
            if (bits[i]) candidate[i / 8] |= (byte)(0x80 >> (i % 8));

        var expected = ToBits(SHA256.HashData(candidate)).Take(checksumBits);
        if (!expected.SequenceEqual(bits.Skip(entropyBits)))
        {
            problem = "As palavras não conferem: confira a ordem e a grafia de cada uma.";
            return false;
        }

        entropy = candidate;
        problem = "";
        return true;
    }

    private int Resolve(string token)
    {
        if (_index.TryGetValue(token, out var exact)) return exact;
        if (token.Length >= 4 && _prefixIndex.TryGetValue(token[..4], out var byPrefix)) return byPrefix;
        return -1;
    }

    private static IEnumerable<bool> ToBits(byte[] bytes)
    {
        foreach (var b in bytes)
            for (var i = 7; i >= 0; i--)
                yield return ((b >> i) & 1) == 1;
    }

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
