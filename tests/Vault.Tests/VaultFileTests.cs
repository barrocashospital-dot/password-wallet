using System.Text;
using Vault.Core;
using Vault.Core.Keyslots;
using Vault.Core.Storage;

namespace Vault.Tests;

public class VaultFileTests
{
    [Fact]
    public void ToBytes_Parse_RoundTrips()
    {
        var file = new VaultFile();
        file.Slots.Add(new KeySlot { Type = "recovery", Nonce = new byte[] { 1, 2, 3 }, WrappedKey = new byte[] { 4, 5 } });
        file.Data = new SealedData { Nonce = new byte[] { 9 }, Ciphertext = new byte[] { 8, 7 } };

        var parsed = VaultFile.Parse(file.ToBytes());

        Assert.Equal(FormatInfo.CurrentVersion, parsed.Version);
        Assert.Single(parsed.Slots);
        Assert.Equal("recovery", parsed.Slots[0].Type);
        Assert.Equal(new byte[] { 4, 5 }, parsed.Slots[0].WrappedKey);
        Assert.Equal(new byte[] { 8, 7 }, parsed.Data.Ciphertext);
    }

    [Fact]
    public void ToBytes_UsesCamelCaseJsonNames()
    {
        var json = Encoding.UTF8.GetString(new VaultFile().ToBytes());

        Assert.Contains("\"version\"", json);
        Assert.Contains("\"slots\"", json);
        Assert.Contains("\"data\"", json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("isso não é json")]
    [InlineData("null")]
    [InlineData("{\"version\": 1, \"slots\": [")]
    public void Parse_GarbageOrEmptyOrTruncated_ThrowsCorrupt(string text)
    {
        Assert.Throws<CorruptVaultException>(() => VaultFile.Parse(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void Parse_InvalidBase64_ThrowsCorrupt()
    {
        var json = "{\"version\":1,\"slots\":[{\"type\":\"x\",\"nonce\":\"@@@\"}],\"data\":{}}";

        Assert.Throws<CorruptVaultException>(() => VaultFile.Parse(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void Parse_OtherVersion_ThrowsUnsupported()
    {
        var json = "{\"version\":2,\"slots\":[],\"data\":{}}";

        var ex = Assert.Throws<UnsupportedVersionException>(() => VaultFile.Parse(Encoding.UTF8.GetBytes(json)));

        Assert.Contains("2", ex.Message);
    }
}
