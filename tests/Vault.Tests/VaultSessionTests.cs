using Vault.Core;
using Vault.Core.Crypto;
using Vault.Core.Model;
using Vault.Core.Storage;

namespace Vault.Tests;

public class VaultSessionTests
{
    private const string Pass = "correct horse battery";

    private static (byte[] File, byte[] Keyfile, string Recovery) MakeVault(params Entry[] entries)
    {
        var keyfile = SecureRandom.Bytes(32);
        var (session, recovery) = VaultSession.Create(Pass, keyfile, TestHelpers.FastKdf);
        session.Entries.AddRange(entries);
        return (session.Save(), keyfile, recovery);
    }

    private static Entry SampleEntry() => new()
    {
        Title = "Banco", Username = "camilo", Password = "p@ss-ç-🔐", Notes = "nota"
    };

    [Fact]
    public void CreateSaveOpen_PreservesEntries()
    {
        var (file, keyfile, _) = MakeVault(SampleEntry());

        using var opened = VaultSession.Open(file, Pass, keyfile);

        var entry = Assert.Single(opened.Entries);
        Assert.Equal("Banco", entry.Title);
        Assert.Equal("p@ss-ç-🔐", entry.Password);
    }

    [Fact]
    public void EmptyVault_RoundTrips()
    {
        var (file, keyfile, _) = MakeVault();

        using var opened = VaultSession.Open(file, Pass, keyfile);

        Assert.Empty(opened.Entries);
    }

    [Fact]
    public void Open_WrongPassphrase_Throws()
    {
        var (file, keyfile, _) = MakeVault();

        Assert.Throws<WrongCredentialsException>(() => VaultSession.Open(file, "frase errada demais", keyfile));
    }

    [Fact]
    public void Open_WrongKeyfile_Throws()
    {
        var (file, _, _) = MakeVault();

        Assert.Throws<WrongCredentialsException>(() => VaultSession.Open(file, Pass, SecureRandom.Bytes(32)));
    }

    [Fact]
    public void Open_UnicodePassphrase_Works()
    {
        var keyfile = SecureRandom.Bytes(32);
        var (session, _) = VaultSession.Create("señora-contraseña-🔐", keyfile, TestHelpers.FastKdf);
        var file = session.Save();

        using var opened = VaultSession.Open(file, "señora-contraseña-🔐", keyfile);

        Assert.False(opened.IsLocked);
    }

    [Fact]
    public void Open_TamperedData_ThrowsCorrupt()
    {
        var (file, keyfile, _) = MakeVault(SampleEntry());
        var parsed = VaultFile.Parse(file);
        parsed.Data.Ciphertext[0] ^= 0xFF;

        Assert.Throws<CorruptVaultException>(() => VaultSession.Open(parsed.ToBytes(), Pass, keyfile));
    }

    [Fact]
    public void Open_FileWithoutPassphraseSlot_ThrowsCorrupt()
    {
        var (file, keyfile, _) = MakeVault();
        var parsed = VaultFile.Parse(file);
        parsed.Slots.Clear();

        Assert.Throws<CorruptVaultException>(() => VaultSession.Open(parsed.ToBytes(), Pass, keyfile));
    }

    [Fact]
    public void Open_TruncatedFile_ThrowsCorrupt()
    {
        var (file, keyfile, _) = MakeVault();
        var truncated = file[..^20];

        Assert.Throws<CorruptVaultException>(() => VaultSession.Open(truncated, Pass, keyfile));
    }

    [Fact]
    public void Open_EmptyFile_ThrowsCorrupt()
    {
        Assert.Throws<CorruptVaultException>(() =>
            VaultSession.Open(Array.Empty<byte>(), Pass, SecureRandom.Bytes(32)));
    }

    [Fact]
    public void Create_RejectsShortPassphrase_AndAcceptsExactly12()
    {
        Assert.Throws<ArgumentException>(() =>
            VaultSession.Create("12345678901", SecureRandom.Bytes(32), TestHelpers.FastKdf));

        var (session, _) = VaultSession.Create("123456789012", SecureRandom.Bytes(32), TestHelpers.FastKdf);

        Assert.False(session.IsLocked);
    }

    [Fact]
    public void OpenWithRecovery_Works_EvenWhenTypedLowercaseWithSpaces()
    {
        var (file, _, recovery) = MakeVault(SampleEntry());

        using var a = VaultSession.OpenWithRecovery(file, recovery);
        using var b = VaultSession.OpenWithRecovery(file, recovery.ToLowerInvariant().Replace("-", " "));

        Assert.Single(a.Entries);
        Assert.Single(b.Entries);
    }

    [Fact]
    public void OpenWithRecovery_WrongOrGarbageKey_Throws()
    {
        var (file, _, _) = MakeVault();
        var (_, _, otherRecovery) = MakeVault();

        Assert.Throws<WrongCredentialsException>(() => VaultSession.OpenWithRecovery(file, otherRecovery));
        Assert.Throws<WrongCredentialsException>(() => VaultSession.OpenWithRecovery(file, "lixo"));
    }

    [Fact]
    public void OpenWithRecovery_TypoInKey_Throws()
    {
        var (file, _, recovery) = MakeVault();
        var typo = (recovery[0] == 'A' ? 'B' : 'A') + recovery[1..];

        Assert.Throws<WrongCredentialsException>(() => VaultSession.OpenWithRecovery(file, typo));
    }

    [Fact]
    public void Save_UsesNewNonceEachTime()
    {
        var (session, _) = VaultSession.Create(Pass, SecureRandom.Bytes(32), TestHelpers.FastKdf);

        var first = VaultFile.Parse(session.Save());
        var second = VaultFile.Parse(session.Save());

        Assert.NotEqual(first.Data.Nonce, second.Data.Nonce);
    }

    [Fact]
    public void Lock_ClearsEntriesAndBlocksSave()
    {
        var (session, _) = VaultSession.Create(Pass, SecureRandom.Bytes(32), TestHelpers.FastKdf);
        session.Entries.Add(SampleEntry());

        session.Lock();

        Assert.True(session.IsLocked);
        Assert.Empty(session.Entries);
        Assert.Throws<InvalidOperationException>(() => session.Save());
    }

    [Fact]
    public void Rekey_InvalidatesOldCredentials_AndKeepsEntries()
    {
        var oldKeyfile = SecureRandom.Bytes(32);
        var (session, oldRecovery) = VaultSession.Create(Pass, oldKeyfile, TestHelpers.FastKdf);
        session.Entries.Add(SampleEntry());
        var oldFile = session.Save();

        var newKeyfile = SecureRandom.Bytes(32);
        const string newPass = "uma frase totalmente nova";
        var newRecovery = session.Rekey(newPass, newKeyfile, TestHelpers.FastKdf);
        var newFile = session.Save();

        Assert.Throws<WrongCredentialsException>(() => VaultSession.Open(newFile, Pass, oldKeyfile));
        Assert.Throws<WrongCredentialsException>(() => VaultSession.OpenWithRecovery(newFile, oldRecovery));

        using var viaPass = VaultSession.Open(newFile, newPass, newKeyfile);
        using var viaRecovery = VaultSession.OpenWithRecovery(newFile, newRecovery);
        Assert.Single(viaPass.Entries);
        Assert.Single(viaRecovery.Entries);

        Assert.NotEqual(oldRecovery, newRecovery);

        using var stillOld = VaultSession.Open(oldFile, Pass, oldKeyfile);
        Assert.Single(stillOld.Entries);
    }

    [Fact]
    public void Rekey_WithShortPassphrase_Throws_AndLeavesSessionUsable()
    {
        var keyfile = SecureRandom.Bytes(32);
        var (session, _) = VaultSession.Create(Pass, keyfile, TestHelpers.FastKdf);

        Assert.Throws<ArgumentException>(() => session.Rekey("curta", SecureRandom.Bytes(32), TestHelpers.FastKdf));

        var file = session.Save();
        using var reopened = VaultSession.Open(file, Pass, keyfile);
        Assert.False(reopened.IsLocked);
    }
}
