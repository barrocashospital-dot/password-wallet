using System.Text;
using Vault.Core.Model;

namespace Vault.Core.Import;

public sealed class CsvImportException : Exception
{
    public CsvImportException(string message) : base(message) { }
}

public sealed record CsvImportResult(List<Entry> Entries, int Skipped, string Format);

public static class CsvImporter
{
    private static readonly string[] TitleColumns = { "name", "title", "account" };
    private static readonly string[] UrlColumns = { "url", "login_uri", "web site" };
    private static readonly string[] UsernameColumns = { "username", "login_username", "login name" };
    private static readonly string[] PasswordColumns = { "password", "login_password" };
    private static readonly string[] NotesColumns = { "note", "notes", "comments" };

    public static CsvImportResult Parse(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];

        var backslashEscapes = text.StartsWith("\"Account\",\"Login Name\"", StringComparison.Ordinal);
        var rows = ReadRows(text, backslashEscapes)
            .Where(r => r.Any(f => f.Length > 0))
            .ToList();
        if (rows.Count == 0)
            throw new CsvImportException("O arquivo está vazio.");

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        var password = Find(header, PasswordColumns);
        var title = Find(header, TitleColumns);
        var url = Find(header, UrlColumns);
        var username = Find(header, UsernameColumns);
        var notes = Find(header, NotesColumns);
        if (password < 0 || (title < 0 && url < 0 && username < 0))
            throw new CsvImportException("Não reconheci o formato deste arquivo. Exporte as senhas em CSV pelo Chrome, Edge, Firefox, Bitwarden, KeePass ou KeePassXC.");

        var entries = new List<Entry>();
        var skipped = 0;
        foreach (var row in rows.Skip(1))
        {
            var entry = new Entry
            {
                Title = Field(row, title),
                Url = Field(row, url),
                Username = Field(row, username),
                Password = Field(row, password, trim: false),
                Notes = Field(row, notes)
            };
            if (entry.Username.Length == 0 && entry.Password.Length == 0 && entry.Url.Length == 0)
            {
                skipped++;
                continue;
            }
            if (entry.Title.Length == 0)
                entry.Title = TitleFrom(entry.Url) ?? entry.Username;
            entries.Add(entry);
        }

        return new CsvImportResult(entries, skipped, DetectFormat(header));
    }

    private static string DetectFormat(List<string> header)
    {
        if (header.Contains("login_password")) return "Bitwarden";
        if (header.Contains("login name")) return "KeePass";
        if (header.Contains("group") && header.Contains("title")) return "KeePassXC";
        if (header.Contains("httprealm")) return "Firefox";
        if (header.Contains("name") && header.Contains("note")) return "Chrome/Edge";
        return "CSV";
    }

    private static int Find(List<string> header, string[] names)
    {
        foreach (var name in names)
        {
            var index = header.IndexOf(name);
            if (index >= 0) return index;
        }
        return -1;
    }

    private static string Field(List<string> row, int index, bool trim = true)
    {
        if (index < 0 || index >= row.Count) return "";
        return trim ? row[index].Trim() : row[index];
    }

    private static string? TitleFrom(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host.Length == 0) return null;
        var host = uri.Host;
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    private static IEnumerable<List<string>> ReadRows(string text, bool backslashEscapes)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];
            if (quoted)
            {
                if (backslashEscapes && c == '\\' && i + 1 < text.Length)
                {
                    field.Append(text[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == '"')
                {
                    if (!backslashEscapes && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }
                    quoted = false;
                    i++;
                    continue;
                }
                field.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    yield return row;
                    row = new List<string>();
                    break;
                default:
                    field.Append(c);
                    break;
            }
            i++;
        }

        if (quoted)
            throw new CsvImportException("O arquivo está incompleto: há aspas sem fechar.");
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            yield return row;
        }
    }
}
