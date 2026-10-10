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
    public void Format_Has11GroupsOf5Characters()
    {
        var groups = RecoveryKey.Format(FixedKey()).Split('-');

        Assert.Equal(11, groups.Length);
        Assert.All(groups, g => Assert.Equal(5, g.Length));
    }

    [Fact]
    public void RoundTrip_ReturnsSameKey()
    {
        var key = RecoveryKey.Generate();

        Assert.True(RecoveryKey.TryParse(RecoveryKey.Format(key), out var parsed));
        Assert.Equal(key, parsed);
    }

    [Fact]
    public void TryParse_AcceptsLowercaseAndSpacesAndMissingHyphens()
    {
        var key = FixedKey();
        var text = RecoveryKey.Format(key);

        Assert.True(RecoveryKey.TryParse(text.ToLowerInvariant(), out var a));
        Assert.True(RecoveryKey.TryParse(text.Replace("-", " "), out var b));
        Assert.True(RecoveryKey.TryParse(text.Replace("-", ""), out var c));

        Assert.Equal(key, a);
        Assert.Equal(key, b);
        Assert.Equal(key, c);
    }

    [Fact]
    public void TryParse_WithOneWrongCharacter_Fails()
    {
        var text = RecoveryKey.Format(FixedKey());
        var wrong = (text[0] == 'A' ? 'B' : 'A') + text[1..];

        Assert.False(RecoveryKey.TryParse(wrong, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDE-FGHIJ")]
    [InlineData("11111-11111-11111")]
    public void TryParse_GarbageOrTooShort_Fails(string text)
    {
        Assert.False(RecoveryKey.TryParse(text, out _));
    }
}
