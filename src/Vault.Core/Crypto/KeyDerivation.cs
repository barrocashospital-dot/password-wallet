using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Vault.Core.Crypto;

/// <summary>
/// Custo do Argon2id: quanto de memória, quantas passadas e quantas "pistas" paralelas (lanes).
/// Esses valores ficam gravados no slot do cofre, então podem ser ajustados no futuro
/// sem quebrar cofres antigos.
/// </summary>
/// <param name="MemoryKiB">Memória usada em KiB (65536 = 64 MiB). Mais memória = ataque mais caro.</param>
/// <param name="Iterations">Número de passadas sobre a memória. Mais passadas = mais lento.</param>
/// <param name="Lanes">Grau de paralelismo (threads usadas pelo Argon2).</param>
public sealed record Argon2Params(int MemoryKiB, int Iterations, int Lanes)
{
    /// <summary>Valores usados em produção: 64 MiB, 3 passadas, 4 lanes (recomendação da OWASP).</summary>
    public static Argon2Params Default { get; } = new(65536, 3, 4);
}

/// <summary>
/// Funções que transformam segredos em chaves de 32 bytes:
/// Argon2id para a frase (segredo fraco) e HKDF para misturar segredos já fortes.
/// </summary>
public static class KeyDerivation
{
    /// <summary>Tamanho da chave produzida, em bytes (32 bytes = 256 bits, o que o AES-256 exige).</summary>
    public const int OutputSize = 32;

    /// <summary>
    /// Frase humana (segredo fraco) → 32 bytes, de forma deliberadamente lenta e cara em memória.
    /// </summary>
    /// <param name="passphrase">A frase digitada pelo usuário.</param>
    /// <param name="salt">Bytes aleatórios do slot. Impedem tabelas pré-calculadas.</param>
    /// <param name="p">Custo do Argon2id.</param>
    public static byte[] DeriveFromPassphrase(string passphrase, byte[] salt, Argon2Params p)
    {
        // NFKC: "é" (1 caractere) e "e" + acento (2 caracteres) viram a mesma sequência de bytes.
        // Sem isso, a mesma frase digitada em teclados diferentes daria chaves diferentes.
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
            // Roda sempre (com sucesso ou exceção): apaga a frase da memória.
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    /// <summary>
    /// Mistura um segredo com bytes extras (ex.: arquivo-chave do pendrive) numa KEK de 32 bytes.
    /// Sem o segredo, ou sem o extra, a KEK não é reproduzida.
    /// </summary>
    /// <param name="secret">Segredo principal (ex.: saída do Argon2id).</param>
    /// <param name="extra">Bytes adicionais, ou null se não houver.</param>
    /// <param name="info">Etiqueta de contexto: o mesmo segredo com "info" diferente dá chaves diferentes.</param>
    public static byte[] DeriveKek(byte[] secret, byte[]? extra, string info)
    {
        // Monta secret ‖ extra (concatenação) num único buffer.
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
            // Apaga o buffer com o material das chaves.
            CryptographicOperations.ZeroMemory(ikm);
        }
    }
}
