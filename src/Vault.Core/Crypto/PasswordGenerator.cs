using System.Security.Cryptography;

namespace Vault.Core.Crypto;

public static class PasswordGenerator
{
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%&*?-_+=";
    private const string All = Lower + Upper + Digits + Symbols;

    public static string Generate(int length = 20)
    {
        if (length < 8)
            throw new ArgumentOutOfRangeException(nameof(length), "O mínimo é 8 caracteres.");
        var chars = new char[length];
        chars[0] = Pick(Lower);
        chars[1] = Pick(Upper);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);
        for (var i = 4; i < length; i++)
            chars[i] = Pick(All);
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
    private static char Pick(string set ) => 
  set[RandomNumberGenerator.GetInt32(set.Length)];
}