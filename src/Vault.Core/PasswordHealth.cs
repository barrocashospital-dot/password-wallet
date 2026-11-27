using Vault.Core.Model;

namespace Vault.Core;

[Flags]
public enum PasswordIssue
{
    None = 0,
    Weak = 1,
    Reused = 2
}

public static class PasswordHealth
{
    public const int MinLength = 12;
    public const int ComfortableLength = 16;

    public static bool IsWeak(string password)
    {
        if (password.Length < MinLength) return true;
        return password.Length < ComfortableLength && CharacterClasses(password) < 3;
    }

    public static Dictionary<Guid, PasswordIssue> Analyze(IEnumerable<Entry> entries)
    {
        var list = entries.ToList();
        var counts = list
            .Where(e => e.Password.Length > 0)
            .GroupBy(e => e.Password, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var report = new Dictionary<Guid, PasswordIssue>();
        foreach (var entry in list)
        {
            var issue = PasswordIssue.None;
            if (entry.Password.Length > 0)
            {
                if (IsWeak(entry.Password)) issue |= PasswordIssue.Weak;
                if (counts[entry.Password] > 1) issue |= PasswordIssue.Reused;
            }
            report[entry.Id] = issue;
        }
        return report;
    }

    private static int CharacterClasses(string password)
    {
        var classes = 0;
        if (password.Any(char.IsLower)) classes++;
        if (password.Any(char.IsUpper)) classes++;
        if (password.Any(char.IsDigit)) classes++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) classes++;
        return classes;
    }
}
