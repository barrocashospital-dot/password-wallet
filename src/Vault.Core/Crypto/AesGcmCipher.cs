using System.Security.Cryptography;

namespace Vault.Core.Crypto;

///<summary>
/// Representa uma caixa selada contendo dados cifrados e um nonce.
///</summary>
public sealed record SealedBox(byte[] Nonce, byte[] Ciphertext);

public static class AesGcmCipher
{
    public const int KeySize = 32;
    public const int NonceSize = 12; // 96 bits
    public const int TagSize = 16;   // 128 bits

    public static SealedBox Encrypt(byte[] key, byte[] plaintext, byte[] aad)
    {
      var nonce = SecureRandom.Bytes(NonceSize);
      var output = new byte[plaintext.Length + TagSize];

      using var aes = new AesGcm(key,TagSize);
      aes.Encrypt(
            nonce,
            plaintext,
            output.AsSpan(0, plaintext.Length),
            output.AsSpan(plaintext.Length, TagSize),
            aad);

        return new SealedBox(nonce, output);
    }


    public static byte[] Decrypt(byte[] key, SealedBox box,byte[] aad)
    {
        if (box.Nonce.Length != NonceSize || box.Ciphertext.Length < TagSize)
            throw new CryptographicException("Dados cifrados em formato inválido.");
        var dataLength = box.Ciphertext.Length - TagSize;
        var plaintext = new byte[dataLength];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(
            box.Nonce,
            box.Ciphertext.AsSpan(0, dataLength),
            box.Ciphertext.AsSpan(dataLength, TagSize),
            plaintext,
            aad);

        return plaintext;

    }
}