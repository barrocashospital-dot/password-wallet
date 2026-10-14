using Vault.Core;
using Vault.Core.Storage;

namespace Vault.Tests;

public class KeyfileTests
{
    [Fact]
    public void Create_ThenRead_ReturnsSame32Bytes()
    {
        using var dir = new TempDir();
        var path = dir.File(Keyfile.FileName);

        var created = Keyfile.Create(path);

        Assert.Equal(32, created.Length);
        Assert.Equal(created, Keyfile.Read(path));
    }

    [Fact]
    public void Create_WhenFileExists_ThrowsAndDoesNotOverwrite()
    {
        using var dir = new TempDir();
        var path = dir.File(Keyfile.FileName);
        var original = Keyfile.Create(path);

        Assert.Throws<IOException>(() => Keyfile.Create(path));
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public void Read_WrongSize_ThrowsWrongCredentials()
    {
        using var dir = new TempDir();
        var path = dir.File("x.keyfile");
        File.WriteAllBytes(path, new byte[10]);

        Assert.Throws<WrongCredentialsException>(() => Keyfile.Read(path));
    }

    [Fact]
    public void Read_MissingFile_ThrowsFileNotFound()
    {
        using var dir = new TempDir();

        Assert.Throws<FileNotFoundException>(() => Keyfile.Read(dir.File("nao-existe.keyfile")));
    }

    [Fact]
    public void Find_ReturnsPathOfFirstRootThatHasKeyfile_OrNull()
    {
        using var empty = new TempDir();
        using var withKey = new TempDir();
        Keyfile.Create(withKey.File(Keyfile.FileName));

        Assert.Equal(
            withKey.File(Keyfile.FileName),
            Keyfile.Find(new[] { empty.Root, withKey.Root }));
        Assert.Null(Keyfile.Find(new[] { empty.Root }));
        Assert.Null(Keyfile.Find(Array.Empty<string>()));
    }
}
