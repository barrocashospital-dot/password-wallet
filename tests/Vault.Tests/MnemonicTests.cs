using System.Security.Cryptography;
using System.Text;
using Vault.Core.Crypto;

namespace Vault.Tests;

public class MnemonicTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "Data");

    private static Mnemonic English() =>
        Mnemonic.FromText(File.ReadAllText(Path.Combine(DataDir, "bip39-english.txt")));

    public static IEnumerable<object[]> EnglishVectors() =>
        File.ReadAllLines(Path.Combine(DataDir, "bip39-english-vectors.txt"))
            .Where(l => l.Length > 0)
            .Select(l => l.Split('|'))
            .Select(p => new object[] { p[0], p[1] });

    [Theory]
    [MemberData(nameof(EnglishVectors))]
    public void Encode_MatchesOfficialVectors(string entropyHex, string expected)
    {
        Assert.Equal(expected, English().Encode(Convert.FromHexString(entropyHex)));
    }

    [Theory]
    [MemberData(nameof(EnglishVectors))]
    public void Decode_MatchesOfficialVectors(string entropyHex, string mnemonic)
    {
        Assert.True(English().TryDecode(mnemonic, out var entropy, out var problem), problem);
        Assert.Equal(entropyHex, Convert.ToHexString(entropy).ToLowerInvariant());
    }

    [Fact]
    public void PortugueseList_IsTheOfficialOne()
    {
        var words = Mnemonic.Portuguese.Words;

        Assert.Equal(2048, words.Count);
        Assert.Equal(2048, words.Distinct().Count());
        Assert.All(words, w => Assert.Matches("^[a-z]+$", w));
        Assert.Equal(2048, words.Select(w => w[..4]).Distinct().Count());
        Assert.Equal("abacate", words[0]);
        Assert.Equal("zumbido", words[^1]);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", words) + "\n"));
        Assert.Equal("2685e9c194c82ae67e10ba59d9ea5345a23dc093e92276fc5361f6667d79cd3f", Convert.ToHexString(hash).ToLowerInvariant());
    }

    [Fact]
    public void Portuguese_32Bytes_Gives24Words_AndRoundTrips()
    {
        var key = Enumerable.Range(0, 32).Select(i => (byte)(i * 7)).ToArray();

        var text = Mnemonic.Portuguese.Encode(key);

        Assert.Equal(24, text.Split(' ').Length);
        Assert.True(Mnemonic.Portuguese.TryDecode(text, out var back, out _));
        Assert.Equal(key, back);
    }

    [Fact]
    public void TryDecode_AcceptsUppercaseAccentsNumbersAndFourLetterPrefixes()
    {
        var key = Enumerable.Range(0, 32).Select(i => (byte)(255 - i)).ToArray();
        var words = Mnemonic.Portuguese.Encode(key).Split(' ');

        var numbered = string.Join("\n", words.Select((w, i) => $"{i + 1}. {w.ToUpperInvariant()}"));
        var prefixes = string.Join(", ", words.Select(w => w[..4]));
        var accented = string.Join(" - ", words.Select(w => w.Replace('a', 'á')));

        foreach (var text in new[] { numbered, prefixes, accented })
        {
            Assert.True(Mnemonic.Portuguese.TryDecode(text, out var back, out var problem), problem);
            Assert.Equal(key, back);
        }
    }

    [Fact]
    public void TryDecode_UnknownWord_SaysWhichOne()
    {
        var words = Mnemonic.Portuguese.Encode(new byte[32]).Split(' ');
        words[6] = "xyzzy";

        Assert.False(Mnemonic.Portuguese.TryDecode(string.Join(' ', words), out _, out var problem));
        Assert.Contains("7", problem);
        Assert.Contains("xyzzy", problem);
    }

    [Fact]
    public void TryDecode_WrongCount_SaysHowMany()
    {
        var words = Mnemonic.Portuguese.Encode(new byte[32]).Split(' ');

        Assert.False(Mnemonic.Portuguese.TryDecode(string.Join(' ', words[..21]), out _, out var problem, 24));
        Assert.Contains("21 de 24", problem);
    }

    [Fact]
    public void TryDecode_SwappedWords_FailChecksum()
    {
        var key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var words = Mnemonic.Portuguese.Encode(key).Split(' ');
        (words[0], words[1]) = (words[1], words[0]);

        Assert.False(Mnemonic.Portuguese.TryDecode(string.Join(' ', words), out _, out var problem));
        Assert.Contains("conferem", problem);
    }

    [Fact]
    public void Encode_RejectsBadLength()
    {
        Assert.Throws<ArgumentException>(() => Mnemonic.Portuguese.Encode(new byte[31]));
    }
}
