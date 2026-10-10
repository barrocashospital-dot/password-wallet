using Vault.Core.Crypto;

namespace Vault.Tests;

internal static class TestHelpers
{
    public static readonly Argon2Params FastKdf = new(1024, 1, 1);
}
