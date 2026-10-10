using Vault.Core.Crypto;

namespace Vault.Tests;

public class SecureRandomTests
{
    [Fact]
    public void Bytes_ReturnsRequestedLength()
    {
        var bytes = SecureRandom.Bytes(32);

        Assert.Equal(32, bytes.Length);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(32)]
    public void Bytes_ReturnsRequestedLength_ForSeveralSizes(int size)
    {
        var bytes = SecureRandom.Bytes(size);

        Assert.Equal(size, bytes.Length);
    }

    [Fact]
    public void Bytes_TwoCallsReturnDifferentValues()
    {
        var a = SecureRandom.Bytes(32);
        var b = SecureRandom.Bytes(32);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Bytes_NegativeLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SecureRandom.Bytes(-1));
    }
}
