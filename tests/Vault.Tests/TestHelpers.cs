using Vault.Core.Crypto;

namespace Vault.Tests;

/// <summary>Itens compartilhados entre os arquivos de teste.</summary>
internal static class TestHelpers
{
    /// <summary>
    /// Parâmetros baratos do Argon2 (1 MiB, 1 passada, 1 lane): deixam os testes rápidos.
    /// O padrão de produção (64 MiB, 3 passadas) é lento de propósito. Nunca use este em produção.
    /// </summary>
    public static readonly Argon2Params FastKdf = new(1024, 1, 1);
}
