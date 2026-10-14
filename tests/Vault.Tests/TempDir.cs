namespace Vault.Tests;

internal sealed class TempDir : IDisposable
{
    public string Root { get; } =
        Path.Combine(Path.GetTempPath(), "vault-tests-" + Guid.NewGuid().ToString("N"));
    
    public TempDir() => Directory.CreateDirectory(Root);
    
    public string File(string name) => Path.Combine(Root, name);

    public void Dispose()
    {
        try {Directory.Delete(Root, recursive: true); } catch { }
    }
}
