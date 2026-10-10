using System.Text;

namespace Vault.Core.Crypto;

/// <summary>
/// Codificação Base32 (alfabeto da RFC 4648, sem padding "="), usada na chave de recuperação.
/// Só tem A–Z e 2–7: sem 0/O nem 1/I/l confundíveis, e sem diferença de maiúscula/minúscula.
/// </summary>
public static class Base32
{
    // 32 símbolos = 5 bits cada. A posição do caractere no texto É o valor de 5 bits.
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Cada 5 bits viram 1 caractere. Sem padding.</summary>
    public static string Encode(byte[] data)
    {
        var sb = new StringBuilder();
        var buffer = 0; // bits ainda não emitidos (sempre menos de 5 depois do while)
        var bits = 0;   // quantos bits válidos há no buffer

        foreach (var b in data)
        {
            // Empurra o buffer 8 casas para a esquerda e encaixa o novo byte nos 8 bits baixos.
            buffer = (buffer << 8) | b;
            bits += 8;

            // Enquanto houver 5 bits ou mais, emite um caractere.
            while (bits >= 5)
            {
                // Pega os 5 bits mais altos dos 'bits' válidos e isola com a máscara 31 (0b11111).
                sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }

            // Descarta os bits já emitidos, mantendo só os 'bits' restantes (máscara com 'bits' uns).
            buffer &= (1 << bits) - 1;
        }

        // Sobrou menos de 5 bits: completa com zeros à direita e emite o último caractere.
        if (bits > 0)
            sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);

        return sb.ToString();
    }

    /// <summary>Faz o caminho inverso. Bits sobrando no final (menos de 8) são descartados.</summary>
    /// <returns>false se houver caractere fora do alfabeto (inclusive minúsculas).</returns>
    public static bool TryDecode(string text, out byte[] data)
    {
        var result = new List<byte>();
        var buffer = 0; // bits acumulados que ainda não formaram um byte
        var bits = 0;   // quantos bits válidos há no buffer

        foreach (var c in text)
        {
            // Posição do caractere no alfabeto = valor de 5 bits. -1 se não existir.
            var value = Alphabet.IndexOf(c);
            if (value < 0)
            {
                data = Array.Empty<byte>();
                return false;
            }

            // Encaixa os 5 novos bits no fim do buffer.
            buffer = (buffer << 5) | value;
            bits += 5;

            // Com 8 bits ou mais, já dá para formar um byte.
            if (bits >= 8)
            {
                // Pega os 8 bits mais altos dos 'bits' válidos.
                result.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;

                // Descarta os bits já usados.
                buffer &= (1 << bits) - 1;
            }
        }

        data = result.ToArray();
        return true;
    }
}
