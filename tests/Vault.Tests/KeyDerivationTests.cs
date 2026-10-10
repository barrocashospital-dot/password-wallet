using Vault.Core.Crypto;

namespace Vault.Tests;

public class KeyDerivationTests
{
    private static readonly byte[] Salt = new byte[16];

    [Fact]
    public void Passphrase_IsDeterministic()
    {
        var a = KeyDerivation.DeriveFromPassphrase("frase-de-teste", Salt, TestHelpers.FastKdf);
        var b = KeyDerivation.DeriveFromPassphrase("frase-de-teste", Salt, TestHelpers.FastKdf);

        Assert.Equal(32, a.Length);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Passphrase_DifferentSalt_DifferentKey()
    {
        var otherSalt = new byte[16];
        otherSalt[0] = 1;

        var a = KeyDerivation.DeriveFromPassphrase("frase-de-teste", Salt, TestHelpers.FastKdf);
        var b = KeyDerivation.DeriveFromPassphrase("frase-de-teste", otherSalt, TestHelpers.FastKdf);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Passphrase_DifferentPassphrase_DifferentKey()
    {
        var a = KeyDerivation.DeriveFromPassphrase("frase-de-teste", Salt, TestHelpers.FastKdf);
        var b = KeyDerivation.DeriveFromPassphrase("frase-de-tesTe", Salt, TestHelpers.FastKdf);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Passphrase_UnicodeComposedAndDecomposed_AreEqual()
    {
        var composed = "caf" + (char)0x00E9 + " da manh" + (char)0x00E3;
        var decomposed = "cafe" + (char)0x0301 + " da manha" + (char)0x0303;

        var a = KeyDerivation.DeriveFromPassphrase(composed, Salt, TestHelpers.FastKdf);
        var b = KeyDerivation.DeriveFromPassphrase(decomposed, Salt, TestHelpers.FastKdf);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Passphrase_EmojiWorks()
    {
        var key = KeyDerivation.DeriveFromPassphrase("senha-\U0001F510-segura", Salt, TestHelpers.FastKdf);

        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void Kek_DependsOnBothSecretAndExtra()
    {
        var secret = SecureRandom.Bytes(32);
        var keyfileA = SecureRandom.Bytes(32);
        var keyfileB = SecureRandom.Bytes(32);

        var a = KeyDerivation.DeriveKek(secret, keyfileA, "ctx");
        var b = KeyDerivation.DeriveKek(secret, keyfileB, "ctx");
        var c = KeyDerivation.DeriveKek(SecureRandom.Bytes(32), keyfileA, "ctx");

        Assert.Equal(32, a.Length);
        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Kek_DependsOnInfoString()
    {
        var secret = SecureRandom.Bytes(32);

        Assert.NotEqual(
            KeyDerivation.DeriveKek(secret, null, "um"),
            KeyDerivation.DeriveKek(secret, null, "dois"));
    }
}
