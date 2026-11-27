using Vault.Core.Crypto;

namespace Vault.Tests;

public class RecoveryKeyTests
{
    private static byte[] FixedKey() => Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    [Fact]
    public void Generate_Returns32DifferentBytes()
    {
        var a = RecoveryKey.Generate();

        Assert.Equal(32, a.Length);
        Assert.NotEqual(a, RecoveryKey.Generate());
    }

    [Fact]
    public void Format_Has24PortugueseWords()
    {
        var words = RecoveryKey.Format(FixedKey()).Split(' ');

        Assert.Equal(24, words.Length);
        Assert.All(words, w => Assert.Contains(w, Mnemonic.Portuguese.Words));
    }

    [Fact]
    public void RoundTrip_ReturnsSameKey()
    {
        var key = RecoveryKey.Generate();

        Assert.True(RecoveryKey.TryParse(RecoveryKey.Format(key), out var parsed));
        Assert.Equal(key, parsed);
    }

    [Fact]
    public void TryParse_AcceptsUppercaseAndLineBreaks()
    {
        var key = FixedKey();
        var text = RecoveryKey.Format(key);

        Assert.True(RecoveryKey.TryParse(text.ToUpperInvariant(), out var a));
        Assert.True(RecoveryKey.TryParse(text.Replace(" ", "\r\n"), out var b));

        Assert.Equal(key, a);
        Assert.Equal(key, b);
    }

    [Fact]
    public void TryParse_WithOneWordReplaced_Fails()
    {
        var words = RecoveryKey.Format(FixedKey()).Split(' ');
        words[0] = words[0] == "abacate" ? "zumbido" : "abacate";

        Assert.False(RecoveryKey.TryParse(string.Join(' ', words), out _));
    }

    [Fact]
    public void LegacyBase32_StillParses_InEveryWayItWasAccepted()
    {
        var key = FixedKey();
        var legacy = RecoveryKey.FormatLegacy(key);

        Assert.Equal(11, legacy.Split('-').Length);
        foreach (var text in new[] { legacy, legacy.ToLowerInvariant(), legacy.Replace("-", " "), legacy.Replace("-", "") })
        {
            Assert.True(RecoveryKey.TryParse(text, out var parsed), text);
            Assert.Equal(key, parsed);
        }
    }

    [Fact]
    public void LegacyBase32_WithOneWrongCharacter_Fails()
    {
        var text = RecoveryKey.FormatLegacy(FixedKey());
        var wrong = (text[0] == 'A' ? 'B' : 'A') + text[1..];

        Assert.False(RecoveryKey.TryParse(wrong, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDE-FGHIJ")]
    [InlineData("11111-11111-11111")]
    [InlineData("abacate abaixo")]
    public void TryParse_GarbageOrTooShort_Fails(string text)
    {
        Assert.False(RecoveryKey.TryParse(text, out _));
    }

    [Fact]
    public void Problem_IsEmptyForValidKeys_AndExplainsMistakes()
    {
        var words = RecoveryKey.Format(FixedKey()).Split(' ');

        Assert.Equal("", RecoveryKey.Problem(string.Join(' ', words)));
        Assert.Equal("", RecoveryKey.Problem(RecoveryKey.FormatLegacy(FixedKey())));
        Assert.Equal("", RecoveryKey.Problem(""));
        Assert.Contains("23", RecoveryKey.Problem(string.Join(' ', words[..23])));

        words[3] = "qqqq9";
        Assert.Contains("4", RecoveryKey.Problem(string.Join(' ', words)));
    }
}
