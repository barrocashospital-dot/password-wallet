using System.Text.Json;
using Vault.Core.Keyslots;

namespace Vault.Core.Storage;

public sealed class SealedData
{
    public byte[] Nonce { get; set; } = Array.Empty<byte>();
    public byte[] Ciphertext { get; set; } = Array.Empty<byte>();
}

public sealed class VaultFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public int Version { get; set; } = FormatInfo.CurrentVersion;

    public List<KeySlot> Slots { get; set; } = new();
    public SealedData Data { get; set; } = new();

    public byte[] ToBytes() => JsonSerializer.SerializeToUtf8Bytes(this, Options);

    public static VaultFile Parse(byte[] bytes)
    {
        VaultFile? file;
        try
        {
            file = JsonSerializer.Deserialize<VaultFile>(bytes, Options);
        }

        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            throw new CorruptVaultException("O arquivo do cofre está ilegível.", ex);
        }
        if (file is null)
            throw new CorruptVaultException("O arquivo do cofre está vazio.");

        if (file.Version != FormatInfo.CurrentVersion)
            throw new UnsupportedVersionException(file.Version);

        return file;
    }
}
