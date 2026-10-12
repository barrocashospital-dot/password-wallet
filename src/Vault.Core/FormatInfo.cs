using System.Text;

namespace Vault.Core;
public static class FormatInfo
{
    public const int CurrentVersion = 1;

    public static byte[] Aad(string context) => Encoding.UTF8.GetBytes($"Vault v{CurrentVersion} {context}");
}
