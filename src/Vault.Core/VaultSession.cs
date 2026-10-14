using System.Security.Cryptography;
using System.Text.Json;
using Vault.Core.Crypto;
using Vault.Core.Keyslots;
using Vault.Core.Model;
using Vault.Core.Storage;

namespace Vault.Core;

public sealed class VaultSession : IDisposable
{
    private byte[]? _dek;

    private readonly VaultFile _file;

    public List<Entry> Entries { get; }

    public bool IsLocked => _dek is null;

    private VaultSession(byte[] dek, VaultFile file, List<Entry> entries)
    {
        _dek = dek;
        _file = file;
        Entries = entries;
    }

    public static (VaultSession Session, string RecoveryKey) Create(string passphrase, byte[] keyfile, Argon2Params? kdf = null)
    {
        PassphrasePolicy.Validate(passphrase);

        var session = new VaultSession(SecureRandom.Bytes(32), new VaultFile(), new List<Entry>());

        var recovery = session.BuildSlots(passphrase, keyfile, kdf ?? Argon2Params.Default);
        return (session, recovery);
    }

    public static VaultSession Open(byte[] fileBytes, string passphrase, byte[] keyfile)
    {
        var file = VaultFile.Parse(fileBytes);

        var slot = file.Slots.FirstOrDefault(s => s.Type == KeySlots.PassphraseType)
                   ?? throw new CorruptVaultException("O cofre não tem o slot da frase.");

        var dek = KeySlots.OpenPassphraseSlot(slot, passphrase, keyfile);
        return new VaultSession(dek, file, DecryptEntries(dek, file));
    }

    public static VaultSession OpenWithRecovery(byte[] fileBytes, string recoveryKeyText)
    {
        if (!RecoveryKey.TryParse(recoveryKeyText, out var recoveryKey))
            throw new WrongCredentialsException();

        var file = VaultFile.Parse(fileBytes);

        var slot = file.Slots.FirstOrDefault(s => s.Type == KeySlots.RecoveryType)
                   ?? throw new CorruptVaultException("O cofre não tem o slot de recuperação.");

        var dek = KeySlots.OpenRecoverySlot(slot, recoveryKey);

        CryptographicOperations.ZeroMemory(recoveryKey);
        return new VaultSession(dek, file, DecryptEntries(dek, file));
    }

    public byte[] Save()
    {
        RequireUnlocked();
        var json = JsonSerializer.SerializeToUtf8Bytes(Entries);
        try
        {
            var box = AesGcmCipher.Encrypt(_dek!, json, FormatInfo.Aad("data"));
            _file.Data = new SealedData { Nonce = box.Nonce, Ciphertext = box.Ciphertext };
            return _file.ToBytes();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    public string Rekey(string passphrase, byte[] newKeyfile, Argon2Params? kdf = null)
    {
        RequireUnlocked();

        PassphrasePolicy.Validate(passphrase);

        CryptographicOperations.ZeroMemory(_dek!);

        _dek = SecureRandom.Bytes(32);

        return BuildSlots(passphrase, newKeyfile, kdf ?? Argon2Params.Default);
    }

    public void Lock()
    {
        if (_dek is not null)
        {
            CryptographicOperations.ZeroMemory(_dek);
            _dek = null;
            Entries.Clear();
        }
    }

    public void Dispose() => Lock();

    private string BuildSlots(string passphrase, byte[] keyfile, Argon2Params kdf)
    {
        var recoveryKey = RecoveryKey.Generate();
        try
        {
            _file.Slots = new List<KeySlot>
            {
                KeySlots.CreatePassphraseSlot(_dek!, passphrase, keyfile, kdf),
                KeySlots.CreateRecoverySlot(_dek!, recoveryKey)
            };
            return RecoveryKey.Format(recoveryKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recoveryKey);
        }
    }

    private static List<Entry> DecryptEntries(byte[] dek, VaultFile file)
    {
        try
        {
            var box = new SealedBox(file.Data.Nonce, file.Data.Ciphertext);
            var json = AesGcmCipher.Decrypt(dek, box, FormatInfo.Aad("data"));
            try
            {
                return JsonSerializer.Deserialize<List<Entry>>(json)
                       ?? throw new CorruptVaultException("Dados do cofre vazios.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(json);
            }
        }
        catch (CryptographicException ex)
        {
            throw new CorruptVaultException("Os dados do cofre foram alterados ou estão corrompidos.", ex);
        }
        catch (JsonException ex)
        {
            throw new CorruptVaultException("Os dados do cofre estão ilegíveis.", ex);
        }
    }

    private void RequireUnlocked()
    {
        if (_dek is null)
            throw new InvalidOperationException("O cofre está bloqueado.");
    }
}
