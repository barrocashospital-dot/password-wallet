using Vault.Core.Crypto;

namespace Vault.Tests;

public class PasswordGeneratorTests
{
    [Fact]
    public void Generate_HasRequestedLength()
    {
        Assert.Equal(20, PasswordGenerator.Generate().Length);
        Assert.Equal(8, PasswordGenerator.Generate(8).Length);
        Assert.Equal(64, PasswordGenerator.Generate(64).Length);
    }

    [Fact]
    public void Generate_AlwaysContainsEveryCharacterClass()
    {
        for (var i = 0; i < 200; i++)
        {
            var p = PasswordGenerator.Generate(8);

            Assert.Contains(p, char.IsLower);
            Assert.Contains(p, char.IsUpper);
            Assert.Contains(p, char.IsDigit);
            Assert.Contains(p, c => "!@#$%&*?-_+=".Contains(c));
        }
    }

    [Fact]
    public void Generate_ProducesDifferentPasswords()
    {
        Assert.NotEqual(PasswordGenerator.Generate(), PasswordGenerator.Generate());
    }

    [Fact]
    public void Generate_TooShort_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordGenerator.Generate(7));
    }
}
