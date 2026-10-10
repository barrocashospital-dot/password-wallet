using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Vault.Core.Crypto;

public sealed record Argon2Params(int MemoryKiB, int Iterations, int Lanes)
{
    public static Argon2Params Default { get; } = new(65536, 3, 4);
}

public static class KeyDerivation
{
    public const int OutputSize = 32;

    public static byte[] DeriveFromPassphrase(string passphrase, byte[] salt, Argon2Params p)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(passphrase.Normalize(NormalizationForm.FormKC));
        try
        {
            using var argon = new Argon2id(passwordBytes)
            {
                Salt = salt,
                MemorySize = p.MemoryKiB,
                Iterations = p.Iterations,
                DegreeOfParallelism = p.Lanes
            };
            return argon.GetBytes(OutputSize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public static byte[] DeriveKek(byte[] secret, byte[]? extra, string info)
    {
        var ikm = new byte[secret.Length + (extra?.Length ?? 0)];
        secret.CopyTo(ikm, 0);
        extra?.CopyTo(ikm, secret.Length);
        try
        {
            return HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                ikm,
                OutputSize,
                salt: null,
                info: Encoding.UTF8.GetBytes(info));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ikm);
        }
    }
}
