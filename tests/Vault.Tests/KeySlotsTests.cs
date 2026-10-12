using Vault.Core;
using Vault.Core.Crypto;
using Vault.Core.Keyslots;

namespace Vault.Tests;

public class KeySlotsTests
{
    private const string Pass = "uma frase bem longa";

    [Fact]
    public void PassphraseSlot_RoundTrip_ReturnsDek()
    {
        var dek = SecureRandom.Bytes(32);
        var keyfile = SecureRandom.Bytes(32);

        var slot = KeySlots.CreatePassphraseSlot(dek, Pass, keyfile, TestHelpers.FastKdf);
        var opened = KeySlots.OpenPassphraseSlot(slot, Pass, keyfile);

        Assert.Equal(dek, opened);
        Assert.Equal(KeySlots.PassphraseType, slot.Type);
    }

    [Fact]
    public void PassphraseSlot_WrongPassphrase_Throws()
    {
        var keyfile = SecureRandom.Bytes(32);
        var slot = KeySlots.CreatePassphraseSlot(SecureRandom.Bytes(32), Pass, keyfile, TestHelpers.FastKdf);

        Assert.Throws<WrongCredentialsException>(() =>
            KeySlots.OpenPassphraseSlot(slot, "outra frase longa", keyfile));
    }

    [Fact]
    public void PassphraseSlot_WrongKeyfile_Throws()
    {
        var slot = KeySlots.CreatePassphraseSlot(
            SecureRandom.Bytes(32), Pass, SecureRandom.Bytes(32), TestHelpers.FastKdf);

        Assert.Throws<WrongCredentialsException>(() =>
            KeySlots.OpenPassphraseSlot(slot, Pass, SecureRandom.Bytes(32)));
    }

    [Fact]
    public void PassphraseSlot_ChangedType_Throws()
    {
        var keyfile = SecureRandom.Bytes(32);
        var slot = KeySlots.CreatePassphraseSlot(SecureRandom.Bytes(32), Pass, keyfile, TestHelpers.FastKdf);

        slot.Type = "recovery";

        Assert.Throws<WrongCredentialsException>(() =>
            KeySlots.OpenPassphraseSlot(slot, Pass, keyfile));
    }

    [Fact]
    public void PassphraseSlot_AbsurdKdfParameters_AreRejectedBeforeAllocating()
    {
        var keyfile = SecureRandom.Bytes(32);
        var slot = KeySlots.CreatePassphraseSlot(SecureRandom.Bytes(32), Pass, keyfile, TestHelpers.FastKdf);

        slot.Kdf!.MemKiB = int.MaxValue;

        Assert.Throws<CorruptVaultException>(() => KeySlots.OpenPassphraseSlot(slot, Pass, keyfile));
    }

    [Fact]
    public void PassphraseSlot_MissingSalt_Throws()
    {
        var keyfile = SecureRandom.Bytes(32);
        var slot = KeySlots.CreatePassphraseSlot(SecureRandom.Bytes(32), Pass, keyfile, TestHelpers.FastKdf);

        slot.Salt = null;

        Assert.Throws<CorruptVaultException>(() => KeySlots.OpenPassphraseSlot(slot, Pass, keyfile));
    }

    [Fact]
    public void RecoverySlot_RoundTrip_ReturnsDek()
    {
        var dek = SecureRandom.Bytes(32);
        var recovery = RecoveryKey.Generate();

        var slot = KeySlots.CreateRecoverySlot(dek, recovery);

        Assert.Equal(dek, KeySlots.OpenRecoverySlot(slot, recovery));
        Assert.Equal(KeySlots.RecoveryType, slot.Type);
    }

    [Fact]
    public void RecoverySlot_WrongKey_Throws()
    {
        var slot = KeySlots.CreateRecoverySlot(SecureRandom.Bytes(32), RecoveryKey.Generate());

        Assert.Throws<WrongCredentialsException>(() =>
            KeySlots.OpenRecoverySlot(slot, RecoveryKey.Generate()));
    }
}
