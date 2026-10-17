namespace Vault.App.Services;

public sealed record DriveOption(string Root, string Label, bool IsRemovable, bool IsSystem)
{
    public string Warning =>
        IsSystem ? "Disco do sistema: o arquivo-chave aqui não protege."
        : !IsRemovable ? "Disco fixo: só protege se ficar longe do computador."
        : "";
}
