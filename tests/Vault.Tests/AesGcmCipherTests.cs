using System.Security.Cryptography;
using Vault.Core.Crypto;

namespace Vault.Tests;

public class AesGcmCipherTests
{
    private static readonly byte[] Aad = "contexto"u8.ToArray();

    [Fact]
    public void RoundTrip_ReturnsOriginalPlaintext()
    {
        var key = SecureRandom.Bytes(32);
        var plaintext = "senha super secreta"u8.ToArray();

        var box = AesGcmCipher.Encrypt(key, plaintext, Aad);
        var result = AesGcmCipher.Decrypt(key, box, Aad);

        Assert.Equal(plaintext, result);
    }

    [Fact]
    public void RoundTrip_EmptyPlaintext_Works()
    {
        var key = SecureRandom.Bytes(32);

        var box = AesGcmCipher.Encrypt(key, Array.Empty<byte>(), Aad);

        Assert.Empty(AesGcmCipher.Decrypt(key, box, Aad));
    }

    [Fact]
    public void Decrypt_WithWrongKey_Throws()
    {
        var box = AesGcmCipher.Encrypt(SecureRandom.Bytes(32), "x"u8.ToArray(), Aad);

        Assert.ThrowsAny<CryptographicException>(() =>
            AesGcmCipher.Decrypt(SecureRandom.Bytes(32), box, Aad));
    }

    [Fact]
    public void Decrypt_WithTamperedCiphertext_Throws()
    {
        var key = SecureRandom.Bytes(32);
        var box = AesGcmCipher.Encrypt(key, "dados"u8.ToArray(), Aad);

        box.Ciphertext[0] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(key, box, Aad));
    }

    [Fact]
    public void Decrypt_WithDifferentAad_Throws()
    {
        var key = SecureRandom.Bytes(32);
        var box = AesGcmCipher.Encrypt(key, "dados"u8.ToArray(), Aad);

        Assert.ThrowsAny<CryptographicException>(() =>
            AesGcmCipher.Decrypt(key, box, "outro"u8.ToArray()));
    }

    [Fact]
    public void Decrypt_WithTruncatedCiphertext_Throws()
    {
        var key = SecureRandom.Bytes(32);
        var box = new SealedBox(SecureRandom.Bytes(12), new byte[5]);

        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(key, box, Aad));
    }

    [Fact]
    public void Decrypt_WithWrongNonceLength_Throws()
    {
        var key = SecureRandom.Bytes(32);
        var box = new SealedBox(new byte[3], new byte[32]);

        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(key, box, Aad));
    }

    [Fact]
    public void Encrypt_UsesNewNonceEveryTime()
    {
        var key = SecureRandom.Bytes(32);

        var a = AesGcmCipher.Encrypt(key, "x"u8.ToArray(), Aad);
        var b = AesGcmCipher.Encrypt(key, "x"u8.ToArray(), Aad);

        Assert.NotEqual(a.Nonce, b.Nonce);
    }
}
