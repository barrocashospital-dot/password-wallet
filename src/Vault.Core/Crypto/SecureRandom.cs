using System.Security.Cryptography;

namespace Vault.Core.Crypto;

public static class SecureRandom
{
    public static byte[] Bytes(int length) => RandomNumberGenerator.GetBytes(length);
}
