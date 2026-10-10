using System.Text;
using Vault.Core.Crypto;

namespace Vault.Tests;

public class Base32Tests
{
    // Vetores oficiais da RFC 4648 (sem o padding "="): se o Encode bater com todos, está correto.
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Encode_MatchesRfc4648Vectors(string input, string expected)
    {
        Assert.Equal(expected, Base32.Encode(Encoding.ASCII.GetBytes(input)));
    }

    [Theory]
    [InlineData("MY", "f")]
    [InlineData("MZXW6YTBOI", "foobar")]
    public void TryDecode_MatchesRfc4648Vectors(string input, string expected)
    {
        Assert.True(Base32.TryDecode(input, out var bytes));
        Assert.Equal(expected, Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void RoundTrip_RandomBytes()
    {
        // Comprimentos de 0 a 69 cobrem todos os "restos" de bits (length % 5).
        for (var length = 0; length < 70; length++)
        {
            var data = SecureRandom.Bytes(length);

            Assert.True(Base32.TryDecode(Base32.Encode(data), out var back));
            Assert.Equal(data, back);
        }
    }

    [Fact]
    public void TryDecode_InvalidCharacter_ReturnsFalse()
    {
        Assert.False(Base32.TryDecode("MZXW1", out _));   // '1' não existe no alfabeto
        Assert.False(Base32.TryDecode("mzxw6", out _));   // minúsculas não são aceitas aqui
    }
}
