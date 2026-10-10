using System.Text;

namespace Vault.Core.Crypto;

public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(byte[] data)
    {
        var sb = new StringBuilder();
        var buffer = 0;
        var bits = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;

            while (bits >= 5)
            {
                sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }

            buffer &= (1 << bits) - 1;
        }

        if (bits > 0)
            sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);

        return sb.ToString();
    }

    public static bool TryDecode(string text, out byte[] data)
    {
        var result = new List<byte>();
        var buffer = 0;
        var bits = 0;

        foreach (var c in text)
        {
            var value = Alphabet.IndexOf(c);
            if (value < 0)
            {
                data = Array.Empty<byte>();
                return false;
            }

            buffer = (buffer << 5) | value;
            bits += 5;

            if (bits >= 8)
            {
                result.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;

                buffer &= (1 << bits) - 1;
            }
        }

        data = result.ToArray();
        return true;
    }
}
