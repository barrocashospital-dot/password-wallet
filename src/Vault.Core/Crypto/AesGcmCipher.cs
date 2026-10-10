using System.Security.Cryptography;

namespace Vault.Core.Crypto;

/// <summary>
/// Resultado de uma cifragem: o nonce usado e o texto cifrado seguido da tag de 16 bytes
/// (texto cifrado ‖ tag). Os dois são necessários para decifrar.
/// </summary>
/// <param name="Nonce">12 bytes aleatórios, únicos para cada cifragem. Não é segredo.</param>
/// <param name="Ciphertext">Dados cifrados com a tag de autenticação no final.</param>
public sealed record SealedBox(byte[] Nonce, byte[] Ciphertext);

/// <summary>
/// Cifra e decifra com AES-256-GCM (criptografia autenticada): além de esconder o conteúdo,
/// detecta qualquer alteração dos dados ou do contexto (AAD).
/// </summary>
public static class AesGcmCipher
{
    /// <summary>Tamanho da chave em bytes (256 bits).</summary>
    public const int KeySize = 32;

    /// <summary>Tamanho do nonce em bytes (96 bits, o padrão do GCM).</summary>
    public const int NonceSize = 12;

    /// <summary>Tamanho da tag de autenticação em bytes (128 bits).</summary>
    public const int TagSize = 16;

    /// <summary>Cifra <paramref name="plaintext"/> com um nonce novo a cada chamada.</summary>
    /// <param name="key">Chave de 32 bytes.</param>
    /// <param name="plaintext">Dados a cifrar (pode ser vazio).</param>
    /// <param name="aad">Contexto autenticado mas não cifrado. A decifragem exige o mesmo valor.</param>
    public static SealedBox Encrypt(byte[] key, byte[] plaintext, byte[] aad)
    {
        // Regra de ouro: nunca reutilizar um nonce com a mesma chave.
        var nonce = SecureRandom.Bytes(NonceSize);

        // Layout da saída: [ texto cifrado (plaintext.Length) | tag (16) ]
        var output = new byte[plaintext.Length + TagSize];

        // 'using var' libera os recursos nativos do AES ao fim do método.
        using var aes = new AesGcm(key, TagSize);

        // AsSpan cria "janelas" sobre o array, sem copiar: o AES escreve direto em cada parte.
        aes.Encrypt(
            nonce,
            plaintext,
            output.AsSpan(0, plaintext.Length),
            output.AsSpan(plaintext.Length, TagSize),
            aad);

        return new SealedBox(nonce, output);
    }

    /// <summary>Decifra e confere a autenticidade. Qualquer alteração faz a operação falhar.</summary>
    /// <exception cref="CryptographicException">
    /// Chave errada, dados adulterados, AAD diferente ou formato inválido.
    /// </exception>
    public static byte[] Decrypt(byte[] key, SealedBox box, byte[] aad)
    {
        // Valida o formato antes de chamar o AES, para devolver sempre o mesmo tipo de exceção.
        if (box.Nonce.Length != NonceSize || box.Ciphertext.Length < TagSize)
            throw new CryptographicException("Dados cifrados em formato inválido.");

        // Separa [ texto cifrado | tag ].
        var dataLength = box.Ciphertext.Length - TagSize;
        var plaintext = new byte[dataLength];

        using var aes = new AesGcm(key, TagSize);

        // Se a tag não bater, o AES lança exceção e nenhum dado é devolvido.
        aes.Decrypt(
            box.Nonce,
            box.Ciphertext.AsSpan(0, dataLength),
            box.Ciphertext.AsSpan(dataLength, TagSize),
            plaintext,
            aad);

        return plaintext;
    }
}
