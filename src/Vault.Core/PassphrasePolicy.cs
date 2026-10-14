namespace Vault.Core;

public static class PassphrasePolicy
{
    public const int MinLength = 12;

    public static bool IsAcceptable(string? passphrase) => passphrase is not null && passphrase.Length >= MinLength;

    public static void Validate(string? passphrase)
    {
        if (!IsAcceptable(passphrase))
            throw new ArgumentException($"A frase deve ter pelo menos {MinLength} caracteres.", nameof(passphrase));
    }
}
