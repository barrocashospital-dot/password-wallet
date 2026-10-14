using Vault.Core.Storage;

namespace Vault.Tests;

public class VaultStorageTests
{
    [Fact]
    public void SaveAtomic_CreatesFile_AndLoadReturnsSameBytes()
    {
        using var dir = new TempDir();
        var path = dir.File("a.vault");

        VaultStorage.SaveAtomic(path, new byte[] { 1, 2, 3 });

        Assert.Equal(new byte[] { 1, 2, 3 }, VaultStorage.Load(path));
    }

    [Fact]
    public void SaveAtomic_Twice_KeepsPreviousVersionAsBak_AndNoTempLeft()
    {
        using var dir = new TempDir();
        var path = dir.File("a.vault");

        VaultStorage.SaveAtomic(path, new byte[] { 1 });
        VaultStorage.SaveAtomic(path, new byte[] { 2 });

        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(path));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(path + ".bak"));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        using var dir = new TempDir();

        Assert.Throws<FileNotFoundException>(() => VaultStorage.Load(dir.File("nao-existe.vault")));
    }

    [Fact]
    public void SaveAtomic_ToMissingFolder_ThrowsIoException()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Root, "pasta-inexistente", "a.vault");

        Assert.ThrowsAny<IOException>(() => VaultStorage.SaveAtomic(path, new byte[] { 1 }));
    }
}
