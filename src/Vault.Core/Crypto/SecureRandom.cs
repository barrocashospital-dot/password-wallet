using System.Security.Cryptography;

namespace Vault.Core.Crypto;

/// <summary>
/// Fonte única de bytes aleatórios do projeto. Toda chave, salt e nonce nasce aqui.
/// Usa o gerador criptograficamente seguro do sistema operacional (nunca System.Random,
/// que é previsível).
/// </summary>
public static class SecureRandom
{
    /// <summary>Devolve <paramref name="length"/> bytes aleatórios seguros.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="length"/> for negativo.</exception>
    public static byte[] Bytes(int length) => RandomNumberGenerator.GetBytes(length);
}
