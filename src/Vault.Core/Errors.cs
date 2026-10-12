namespace Vault.Core;

public sealed class WrongCredentialsException : Exception
{
    public WrongCredentialsException() : base("Credenciais incorretas.") { }
}

public sealed class CorruptVaultException : Exception
{
    public CorruptVaultException(string message, Exception? inner = null) : base(message, inner) { }
}

public sealed class UnsupportedVersionException : Exception
{
    public UnsupportedVersionException(int version) : base($"Versão {version} não suportada.") { }
}
