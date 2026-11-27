using Vault.Core;
using Vault.Core.Crypto;
using Vault.Core.Keyslots;
using Vault.Core.Model;
using Vault.Core.Storage;

namespace Vault.Tests;

public class CredentialSlotsTests
{
    private const string Pass = "correct horse battery";
    private const string NewPass = "uma frase nova bem longa";

    private static (VaultSession Session, byte[] Keyfile, string Recovery) NewVault()
    {
        var keyfile = SecureRandom.Bytes(32);
        var (session, recovery) = VaultSession.Create(Pass, keyfile, TestHelpers.FastKdf);
        session.Entries.Add(new Entry { Title = "Banco", Password = "segredo" });
        return (session, keyfile, recovery);
    }

    private static void Persist(byte[] _) { }

    [Fact]
    public void AddBackupKeyfile_BothKeyfilesOpen()
    {
        var (session, keyfile, _) = NewVault();
        var backup = SecureRandom.Bytes(32);

        session.AddBackupKeyfile(Pass, keyfile, backup, Persist, TestHelpers.FastKdf);
        var file = session.Save();

        using var viaPrimary = VaultSession.Open(file, Pass, keyfile);
        using var viaBackup = VaultSession.Open(file, Pass, backup);
        Assert.Single(viaBackup.Entries);
        Assert.Equal(2, viaBackup.KeyfileCount);
    }

    [Fact]
    public void AddBackupKeyfile_KeepsPrimarySlotFirst()
    {
        var (session, keyfile, _) = NewVault();
        session.AddBackupKeyfile(Pass, keyfile, SecureRandom.Bytes(32), Persist, TestHelpers.FastKdf);

        var file = VaultFile.Parse(session.Save());
        var first = file.Slots.First(s => s.Type == KeySlots.PassphraseType);

        var dek = KeySlots.OpenPassphraseSlot(first, Pass, keyfile);
        Assert.Equal(32, dek.Length);
    }

    [Fact]
    public void AddBackupKeyfile_WrongCurrentCredentials_Throws_AndChangesNothing()
    {
        var (session, keyfile, _) = NewVault();
        var backup = SecureRandom.Bytes(32);

        Assert.Throws<WrongCredentialsException>(() =>
            session.AddBackupKeyfile("frase errada qualquer", keyfile, backup, Persist, TestHelpers.FastKdf));
        Assert.Throws<WrongCredentialsException>(() =>
            session.AddBackupKeyfile(Pass, SecureRandom.Bytes(32), backup, Persist, TestHelpers.FastKdf));

        Assert.Equal(1, session.KeyfileCount);
        Assert.Throws<WrongCredentialsException>(() => VaultSession.Open(session.Save(), Pass, backup));
    }

    [Fact]
    public void AddBackupKeyfile_FailedPersist_RestoresSlots()
    {
        var (session, keyfile, _) = NewVault();
        var backup = SecureRandom.Bytes(32);

        Assert.Throws<IOException>(() =>
            session.AddBackupKeyfile(Pass, keyfile, backup, _ => throw new IOException("disco cheio"), TestHelpers.FastKdf));

        Assert.Equal(1, session.KeyfileCount);
        var file = session.Save();
        using var viaPrimary = VaultSession.Open(file, Pass, keyfile);
        Assert.Throws<WrongCredentialsException>(() => VaultSession.Open(file, Pass, backup));
    }

    [Fact]
    public void AddBackupKeyfile_RefusesAboveLimit()
    {
        var (session, keyfile, _) = NewVault();
        for (var i = 1; i < KeySlots.MaxPassphraseSlots; i++)
            session.AddBackupKeyfile(Pass, keyfile, SecureRandom.Bytes(32), Persist, TestHelpers.FastKdf);

        Assert.Throws<InvalidOperationException>(() =>
            session.AddBackupKeyfile(Pass, keyfile, SecureRandom.Bytes(32), Persist, TestHelpers.FastKdf));
    }

    [Fact]
    public void ChangeCredentials_OldFail_NewOpen_RecoveryKeyStillWorks()
    {
        var (session, keyfile, recovery) = NewVault();
        var newKeyfile = SecureRandom.Bytes(32);
        byte[]? persisted = null;

        session.ChangeCredentials(Pass, keyfile, NewPass, newKeyfile, bytes => persisted = bytes, TestHelpers.FastKdf);

        Assert.NotNull(persisted);
        Assert.Throws<WrongCredentialsException>(() => VaultSession.Open(persisted!, Pass, keyfile));
        Assert.Throws<WrongCredentialsException>(() => VaultSession.Open(persisted!, NewPass, keyfile));
        using var viaNew = VaultSession.Open(persisted!, NewPass, newKeyfile);
        using var viaRecovery = VaultSession.OpenWithRecovery(persisted!, recovery);
        Assert.Single(viaNew.Entries);
        Assert.Single(viaRecovery.Entries);
    }

    [Fact]
    public void ChangeCredentials_KeepingKeyfile_OnlyChangesPassphrase()
    {
        var (session, keyfile, _) = NewVault();

        session.ChangeCredentials(Pass, keyfile, NewPass, keyfile, Persist, TestHelpers.FastKdf);
        var file = session.Save();

        Assert.Throws<WrongCredentialsException>(() => VaultSession.Open(file, Pass, keyfile));
        using var reopened = VaultSession.Open(file, NewPass, keyfile);
        Assert.Single(reopened.Entries);
    }

    [Fact]
    public void ChangeCredentials_RemovesBackupKeyfiles()
    {
        var (session, keyfile, _) = NewVault();
        var backup = SecureRandom.Bytes(32);
        session.AddBackupKeyfile(Pass, keyfile, backup, Persist, TestHelpers.FastKdf);

        session.ChangeCredentials(Pass, backup, NewPass, keyfile, Persist, TestHelpers.FastKdf);
        var file = session.Save();

        Assert.Equal(1, session.KeyfileCount);
        Assert.Throws<WrongCredentialsException>(() => VaultSession.Open(file, Pass, backup));
        Assert.Throws<WrongCredentialsException>(() => VaultSession.Open(file, NewPass, backup));
        using var reopened = VaultSession.Open(file, NewPass, keyfile);
    }

    [Fact]
    public void ChangeCredentials_WrongCurrentCredentials_Throws_AndChangesNothing()
    {
        var (session, keyfile, _) = NewVault();

        Assert.Throws<WrongCredentialsException>(() =>
            session.ChangeCredentials("frase errada qualquer", keyfile, NewPass, SecureRandom.Bytes(32), Persist, TestHelpers.FastKdf));

        using var reopened = VaultSession.Open(session.Save(), Pass, keyfile);
    }

    [Fact]
    public void ChangeCredentials_ShortNewPassphrase_Throws()
    {
        var (session, keyfile, _) = NewVault();

        Assert.Throws<ArgumentException>(() =>
            session.ChangeCredentials(Pass, keyfile, "curta", keyfile, Persist, TestHelpers.FastKdf));

        using var reopened = VaultSession.Open(session.Save(), Pass, keyfile);
    }

    [Fact]
    public void ChangeCredentials_FailedPersist_OldCredentialsStillOpenNextSave()
    {
        var (session, keyfile, _) = NewVault();
        var newKeyfile = SecureRandom.Bytes(32);

        Assert.Throws<IOException>(() =>
            session.ChangeCredentials(Pass, keyfile, NewPass, newKeyfile, _ => throw new IOException("pendrive removido"), TestHelpers.FastKdf));

        var file = session.Save();
        using var reopened = VaultSession.Open(file, Pass, keyfile);
        Assert.Throws<WrongCredentialsException>(() => VaultSession.Open(file, NewPass, newKeyfile));
    }

    [Fact]
    public void VerifyCredentials_ChecksPassphraseAndKeyfile()
    {
        var (session, keyfile, _) = NewVault();

        Assert.True(session.VerifyCredentials(Pass, keyfile));
        Assert.False(session.VerifyCredentials("frase errada qualquer", keyfile));
        Assert.False(session.VerifyCredentials(Pass, SecureRandom.Bytes(32)));
    }

    [Fact]
    public void Parse_TooManyPassphraseSlots_ThrowsCorrupt()
    {
        var (session, keyfile, _) = NewVault();
        var file = VaultFile.Parse(session.Save());
        var slot = file.Slots.First(s => s.Type == KeySlots.PassphraseType);
        for (var i = 0; i < KeySlots.MaxPassphraseSlots; i++)
            file.Slots.Add(slot);

        Assert.Throws<CorruptVaultException>(() => VaultFile.Parse(file.ToBytes()));
    }
}
