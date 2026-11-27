using Vault.Core;
using Vault.Core.Model;

namespace Vault.Tests;

public class PasswordHealthTests
{
    [Theory]
    [InlineData("curta1!", true)]
    [InlineData("abcdefghijk", true)]
    [InlineData("abcdefghijkl", true)]
    [InlineData("Abcdefghijk1", false)]
    [InlineData("abcdefghijklmnop", false)]
    [InlineData("Ab3!efgh", true)]
    [InlineData("kV9#mP2$xQ7&", false)]
    public void IsWeak_UsesLengthAndCharacterClasses(string password, bool weak)
    {
        Assert.Equal(weak, PasswordHealth.IsWeak(password));
    }

    [Fact]
    public void Analyze_FlagsWeakAndReused_IgnoresEmpty()
    {
        var weak = new Entry { Title = "fraca", Password = "123456" };
        var reusedA = new Entry { Title = "a", Password = "kV9#mP2$xQ7&wR4" };
        var reusedB = new Entry { Title = "b", Password = "kV9#mP2$xQ7&wR4" };
        var fine = new Entry { Title = "ok", Password = "Zt8@hN5!pL3^cX6" };
        var emptyA = new Entry { Title = "vazia1", Password = "" };
        var emptyB = new Entry { Title = "vazia2", Password = "" };

        var report = PasswordHealth.Analyze(new[] { weak, reusedA, reusedB, fine, emptyA, emptyB });

        Assert.Equal(PasswordIssue.Weak, report[weak.Id]);
        Assert.Equal(PasswordIssue.Reused, report[reusedA.Id]);
        Assert.Equal(PasswordIssue.Reused, report[reusedB.Id]);
        Assert.Equal(PasswordIssue.None, report[fine.Id]);
        Assert.Equal(PasswordIssue.None, report[emptyA.Id]);
        Assert.Equal(PasswordIssue.None, report[emptyB.Id]);
    }

    [Fact]
    public void Analyze_WeakAndReused_ReportsBoth()
    {
        var a = new Entry { Password = "senha123" };
        var b = new Entry { Password = "senha123" };

        var report = PasswordHealth.Analyze(new[] { a, b });

        Assert.Equal(PasswordIssue.Weak | PasswordIssue.Reused, report[a.Id]);
    }

    [Fact]
    public void Analyze_ReuseIsCaseSensitive()
    {
        var a = new Entry { Password = "kV9#mP2$xQ7&wR4" };
        var b = new Entry { Password = "KV9#MP2$XQ7&WR4" };

        var report = PasswordHealth.Analyze(new[] { a, b });

        Assert.Equal(PasswordIssue.None, report[a.Id]);
    }
}
