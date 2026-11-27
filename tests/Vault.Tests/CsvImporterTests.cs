using Vault.Core.Import;

namespace Vault.Tests;

public class CsvImporterTests
{
    [Fact]
    public void Chrome_MapsColumns_AndHandlesQuotedCommasAndQuotes()
    {
        var csv = "name,url,username,password,note\r\n" +
                  "Banco,https://banco.com/login,eu@x.com,\"a,b\"\"c\",minha nota\r\n";

        var result = CsvImporter.Parse(csv);

        Assert.Equal("Chrome/Edge", result.Format);
        var e = Assert.Single(result.Entries);
        Assert.Equal("Banco", e.Title);
        Assert.Equal("https://banco.com/login", e.Url);
        Assert.Equal("eu@x.com", e.Username);
        Assert.Equal("a,b\"c", e.Password);
        Assert.Equal("minha nota", e.Notes);
    }

    [Fact]
    public void Firefox_DerivesTitleFromUrl()
    {
        var csv = "\"url\",\"username\",\"password\",\"httpRealm\",\"formActionOrigin\",\"guid\",\"timeCreated\",\"timeLastUsed\",\"timePasswordChanged\"\r\n" +
                  "\"https://www.example.com\",\"joao\",\"p@ss\",,\"https://www.example.com\",\"{abc}\",\"1\",\"2\",\"3\"\r\n";

        var result = CsvImporter.Parse(csv);

        Assert.Equal("Firefox", result.Format);
        var e = Assert.Single(result.Entries);
        Assert.Equal("example.com", e.Title);
        Assert.Equal("joao", e.Username);
        Assert.Equal("p@ss", e.Password);
    }

    [Fact]
    public void Bitwarden_KeepsMultilineNotes_AndSkipsSecureNotes()
    {
        var csv = "folder,favorite,type,name,notes,fields,reprompt,login_uri,login_username,login_password,login_totp\n" +
                  "Social,1,login,Twitter,\"linha1\nlinha2\",,0,https://twitter.com,eu,senha,\n" +
                  ",,note,Nota segura,texto,,0,,,,\n";

        var result = CsvImporter.Parse(csv);

        Assert.Equal("Bitwarden", result.Format);
        var e = Assert.Single(result.Entries);
        Assert.Equal("Twitter", e.Title);
        Assert.Equal("https://twitter.com", e.Url);
        Assert.Equal("eu", e.Username);
        Assert.Equal("senha", e.Password);
        Assert.Equal("linha1\nlinha2", e.Notes);
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public void KeePass1x_UsesBackslashEscapes()
    {
        var csv = "\"Account\",\"Login Name\",\"Password\",\"Web Site\",\"Comments\"\n" +
                  "\"Email\",\"eu\",\"pa\\\"ss\\\\x\",\"https://mail.com\",\"c\"\n";

        var result = CsvImporter.Parse(csv);

        Assert.Equal("KeePass", result.Format);
        var e = Assert.Single(result.Entries);
        Assert.Equal("Email", e.Title);
        Assert.Equal("eu", e.Username);
        Assert.Equal("pa\"ss\\x", e.Password);
        Assert.Equal("https://mail.com", e.Url);
        Assert.Equal("c", e.Notes);
    }

    [Fact]
    public void KeePassXC_MapsColumns()
    {
        var csv = "\"Group\",\"Title\",\"Username\",\"Password\",\"URL\",\"Notes\",\"TOTP\",\"Icon\",\"Last Modified\",\"Created\"\n" +
                  "\"Root/Web\",\"GitHub\",\"dev\",\"gh\"\"pass\",\"https://github.com\",\"n\",\"\",\"0\",\"2024\",\"2024\"\n";

        var result = CsvImporter.Parse(csv);

        Assert.Equal("KeePassXC", result.Format);
        var e = Assert.Single(result.Entries);
        Assert.Equal("GitHub", e.Title);
        Assert.Equal("dev", e.Username);
        Assert.Equal("gh\"pass", e.Password);
        Assert.Equal("https://github.com", e.Url);
    }

    [Fact]
    public void IgnoresBom_BlankLines_AndHeaderCase()
    {
        var csv = "﻿NAME,URL,USERNAME,PASSWORD,NOTE\r\n\r\nSite,https://a.com,u,p,\r\n\r\n";

        var result = CsvImporter.Parse(csv);

        var e = Assert.Single(result.Entries);
        Assert.Equal("Site", e.Title);
        Assert.Equal("p", e.Password);
    }

    [Fact]
    public void RowWithoutTitleOrUrl_UsesUsernameAsTitle()
    {
        var csv = "name,url,username,password,note\n,,so-usuario,p,\n";

        var e = Assert.Single(CsvImporter.Parse(csv).Entries);

        Assert.Equal("so-usuario", e.Title);
    }

    [Fact]
    public void Password_KeepsLeadingAndTrailingSpaces()
    {
        var csv = "name,url,username,password,note\nSite , https://a.com ,u,\" com espaço \",\n";

        var e = Assert.Single(CsvImporter.Parse(csv).Entries);

        Assert.Equal(" com espaço ", e.Password);
        Assert.Equal("Site", e.Title);
        Assert.Equal("https://a.com", e.Url);
    }

    [Fact]
    public void UnknownHeader_Throws()
    {
        var ex = Assert.Throws<CsvImportException>(() => CsvImporter.Parse("coluna1,coluna2\na,b\n"));
        Assert.Contains("formato", ex.Message);
    }

    [Fact]
    public void HeaderWithoutPassword_Throws()
    {
        Assert.Throws<CsvImportException>(() => CsvImporter.Parse("name,url,username\na,b,c\n"));
    }

    [Fact]
    public void UnterminatedQuote_Throws()
    {
        Assert.Throws<CsvImportException>(() => CsvImporter.Parse("name,url,username,password,note\n\"aberto,b,c,d,e\n"));
    }

    [Fact]
    public void EmptyText_Throws()
    {
        Assert.Throws<CsvImportException>(() => CsvImporter.Parse(""));
    }
}
