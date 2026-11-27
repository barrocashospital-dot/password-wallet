using System.IO;
using Vault.Core.Storage;

namespace Vault.App.Services;

public static class KeyfileWriter
{
    public static string? CreateThenSwap(string destination, Action<byte[]> applyAndSave)
    {
        var folder = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        var replacing = File.Exists(destination);
        var writtenPath = replacing ? destination + ".new" : destination;
        if (replacing) File.Delete(writtenPath);

        var keyfile = Keyfile.Create(writtenPath);
        try
        {
            applyAndSave(keyfile);
        }
        catch
        {
            File.Delete(writtenPath);
            throw;
        }

        if (!replacing) return null;
        try
        {
            File.Replace(writtenPath, destination, destination + ".old");
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"O cofre foi salvo, mas não foi possível trocar o arquivo-chave antigo ({ex.Message}). " +
                   $"O novo arquivo-chave está em {writtenPath}. Ao desbloquear, use \"Escolher arquivo…\" e aponte para ele, " +
                   $"ou apague o antigo e renomeie o novo para {Keyfile.FileName}.";
        }
    }
}
