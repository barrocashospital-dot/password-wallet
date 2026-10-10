using Vault.Core.Crypto;

namespace Vault.Tests;

public class KeyDerivationTests
{
    // Salt fixo (16 zeros) para os testes serem repetíveis.
    private static readonly byte[] Salt = new byte[16];

    [Fact]
    public void Passphrase_IsDeterministic()
    {
        // Mesma frase + mesmo salt: tem de dar a mesma chave, senão o cofre nunca reabre.
        var a = KeyDerivation.DeriveFromPassphrase("frase-de-teste", Salt, TestHelpers.FastKdf);
        var b = KeyDerivation.DeriveFromPassphrase("frase-de-teste", Salt, TestHelpers.FastKdf);

        Assert.Equal(32, a.Length);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Passphrase_DifferentSalt_DifferentKey()
    {
        // Muda só 1 byte do salt.
        var otherSalt = new byte[16];
        otherSalt[0] = 1;

        var a = KeyDerivation.DeriveFromPassphrase("frase-de-teste", Salt, TestHelpers.FastKdf);
        var b = KeyDerivation.DeriveFromPassphrase("frase-de-teste", otherSalt, TestHelpers.FastKdf);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Passphrase_DifferentPassphrase_DifferentKey()
    {
        // Diferem por uma única letra (t minúsculo x T maiúsculo).
        var a = KeyDerivation.DeriveFromPassphrase("frase-de-teste", Salt, TestHelpers.FastKdf);
        var b = KeyDerivation.DeriveFromPassphrase("frase-de-tesTe", Salt, TestHelpers.FastKdf);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Passphrase_UnicodeComposedAndDecomposed_AreEqual()
    {
        // Usamos \u... porque o editor pode normalizar um "é" digitado e esconder a diferença.
        var composed = "café da manhã";       // é e ã em um só caractere
        var decomposed = "café da manhã";   // e + acento, a + til

        var a = KeyDerivation.DeriveFromPassphrase(composed, Salt, TestHelpers.FastKdf);
        var b = KeyDerivation.DeriveFromPassphrase(decomposed, Salt, TestHelpers.FastKdf);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Passphrase_EmojiWorks()
    {
        // Emoji ocupa 4 bytes em UTF-8: não pode quebrar a derivação.
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
        var b = KeyDerivation.DeriveKek(secret, keyfileB, "ctx");                    // outro arquivo-chave
        var c = KeyDerivation.DeriveKek(SecureRandom.Bytes(32), keyfileA, "ctx");    // outro segredo

        Assert.Equal(32, a.Length);
        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Kek_DependsOnInfoString()
    {
        // Mesmo segredo, sem extra, só o "info" muda.
        var secret = SecureRandom.Bytes(32);

        Assert.NotEqual(
            KeyDerivation.DeriveKek(secret, null, "um"),
            KeyDerivation.DeriveKek(secret, null, "dois"));
    }
}
