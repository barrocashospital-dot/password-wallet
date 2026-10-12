using System.Security.Cryptography;
using Vault.Core.Crypto;

namespace Vault.Core.Keyslots;

public static class KeySlots
{
    public const string PassphraseType = "usb+passphrase";
    public const string RecoveryType = "recovery";

    private const string PassphraseInfo = "vault-kek-passphrase-v1";
    private const string RecoveryInfo = "vault-kek-recovery-v1";
    private const int SaltSize = 16;

    public static KeySlot CreatePassphraseSlot(byte[] dek, string passphrase, byte[] keyfile, Argon2Params kdf)
    {
        var salt = SecureRandom.Bytes(SaltSize);
        var kek = DerivePassphraseKek(passphrase, keyfile, salt, kdf);
        try
        {
            var box = AesGcmCipher.Encrypt(kek, dek, SlotAad(PassphraseType));
            return new KeySlot
            {
                Type = PassphraseType,
                Kdf = new KdfInfo { MemKiB = kdf.MemoryKiB, Iters = kdf.Iterations, Lanes = kdf.Lanes },
                Salt = salt,
                Nonce = box.Nonce,
                WrappedKey = box.Ciphertext
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    public static byte[] OpenPassphraseSlot(KeySlot slot, string passphrase, byte[] keyfile)
    {
        if (slot.Kdf is null || slot.Salt is null || slot.Kdf.Alg != "argon2id")
            throw new CorruptVaultException("Slot de frase malformado.");
        if (slot.Salt.Length is < 8 or > 64)
            throw new CorruptVaultException("Slot de frase com salt inválido.");

        var kdf = new Argon2Params(slot.Kdf.MemKiB, slot.Kdf.Iters, slot.Kdf.Lanes);
        ValidateKdf(kdf);

        var kek = DerivePassphraseKek(passphrase, keyfile, slot.Salt, kdf);
        try
        {
            return AesGcmCipher.Decrypt(kek, new SealedBox(slot.Nonce, slot.WrappedKey), SlotAad(slot.Type));
        }
        catch (CryptographicException)
        {
            throw new WrongCredentialsException();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    public static KeySlot CreateRecoverySlot(byte[] dek, byte[] recoveryKey)
    {
        var kek = KeyDerivation.DeriveKek(recoveryKey, null, RecoveryInfo);
        try
        {
            var box = AesGcmCipher.Encrypt(kek, dek, SlotAad(RecoveryType));
            return new KeySlot { Type = RecoveryType, Nonce = box.Nonce, WrappedKey = box.Ciphertext };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    public static byte[] OpenRecoverySlot(KeySlot slot, byte[] recoveryKey)
    {
        var kek = KeyDerivation.DeriveKek(recoveryKey, null, RecoveryInfo);
        try
        {
            return AesGcmCipher.Decrypt(kek, new SealedBox(slot.Nonce, slot.WrappedKey), SlotAad(slot.Type));
        }
        catch (CryptographicException)
        {
            throw new WrongCredentialsException();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    private static byte[] DerivePassphraseKek(string passphrase, byte[] keyfile, byte[] salt, Argon2Params kdf)
    {
        var stretched = KeyDerivation.DeriveFromPassphrase(passphrase, salt, kdf);
        try
        {
            return KeyDerivation.DeriveKek(stretched, keyfile, PassphraseInfo);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(stretched);
        }
    }

    private static byte[] SlotAad(string type) => FormatInfo.Aad($"slot|{type}");

    private static void ValidateKdf(Argon2Params p)
    {
        if (p.Lanes is < 1 or > 16 ||
            p.Iterations is < 1 or > 20 ||
            p.MemoryKiB < 8 * p.Lanes ||
            p.MemoryKiB > 1_048_576)
        {
            throw new CorruptVaultException("Parâmetros de derivação de chave inválidos.");
        }
    }
}
