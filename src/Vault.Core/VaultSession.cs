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

    public int KeyfileCount => _file.Slots.Count(s => s.Type == KeySlots.PassphraseType);

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

        if (!file.Slots.Any(s => s.Type == KeySlots.PassphraseType))
            throw new CorruptVaultException("O cofre não tem o slot da frase.");

        var dek = TryOpenPassphraseSlots(file, passphrase, keyfile) ?? throw new WrongCredentialsException();
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

    public bool VerifyCredentials(string passphrase, byte[] keyfile)
    {
        RequireUnlocked();
        var dek = TryOpenPassphraseSlots(_file, passphrase, keyfile);
        if (dek is null) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(dek, _dek);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    public void ChangeCredentials(string currentPassphrase, byte[] currentKeyfile, string newPassphrase, byte[] newKeyfile,
        Action<byte[]> persist, Argon2Params? kdf = null)
    {
        RequireUnlocked();
        PassphrasePolicy.Validate(newPassphrase);
        RequireCredentials(currentPassphrase, currentKeyfile);

        var slots = new List<KeySlot> { KeySlots.CreatePassphraseSlot(_dek!, newPassphrase, newKeyfile, kdf ?? Argon2Params.Default) };
        slots.AddRange(_file.Slots.Where(s => s.Type != KeySlots.PassphraseType));
        ReplaceSlotsAndPersist(slots, persist);
    }

    public void AddBackupKeyfile(string currentPassphrase, byte[] currentKeyfile, byte[] backupKeyfile,
        Action<byte[]> persist, Argon2Params? kdf = null)
    {
        RequireUnlocked();
        RequireCredentials(currentPassphrase, currentKeyfile);
        if (KeyfileCount >= KeySlots.MaxPassphraseSlots)
            throw new InvalidOperationException($"O limite é de {KeySlots.MaxPassphraseSlots} arquivos-chave por cofre.");

        var slots = new List<KeySlot>(_file.Slots)
        {
            KeySlots.CreatePassphraseSlot(_dek!, currentPassphrase, backupKeyfile, kdf ?? Argon2Params.Default)
        };
        ReplaceSlotsAndPersist(slots, persist);
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
                var entries = JsonSerializer.Deserialize<List<Entry>>(json)
                              ?? throw new CorruptVaultException("Dados do cofre vazios.");
                foreach (var entry in entries)
                {
                    if (entry is null)
                        throw new CorruptVaultException("Os dados do cofre contêm uma entrada vazia.");
                    Normalise(entry);
                }
                return entries;
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

    private static void Normalise(Entry entry)
    {
        entry.Title ??= "";
        entry.Url ??= "";
        entry.Username ??= "";
        entry.Password ??= "";
        entry.Notes ??= "";
    }

    private void RequireCredentials(string passphrase, byte[] keyfile)
    {
        if (!VerifyCredentials(passphrase, keyfile))
            throw new WrongCredentialsException();
    }

    private void ReplaceSlotsAndPersist(List<KeySlot> slots, Action<byte[]> persist)
    {
        var previous = _file.Slots;
        _file.Slots = slots;
        try
        {
            persist(Save());
        }
        catch
        {
            _file.Slots = previous;
            throw;
        }
    }

    private static byte[]? TryOpenPassphraseSlots(VaultFile file, string passphrase, byte[] keyfile)
    {
        foreach (var slot in file.Slots.Where(s => s.Type == KeySlots.PassphraseType))
        {
            try
            {
                return KeySlots.OpenPassphraseSlot(slot, passphrase, keyfile);
            }
            catch (WrongCredentialsException)
            {
            }
        }
        return null;
    }

    private void RequireUnlocked()
    {
        if (_dek is null)
            throw new InvalidOperationException("O cofre está bloqueado.");
    }
}
