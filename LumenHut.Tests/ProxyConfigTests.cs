using LumenHut.Models;

namespace LumenHut.Tests;

public class ProxyConfigTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_BlankInput_ReturnsNull(string? input)
    {
        Assert.Null(ProxyConfig.Parse(input));
    }

    [Fact]
    public void Parse_HostPortWithoutScheme_DefaultsToHttp()
    {
        var config = ProxyConfig.Parse("proxy.local:3128");

        Assert.NotNull(config);
        Assert.Equal("http://proxy.local:3128", config.Server);
        Assert.Null(config.Username);
        Assert.Null(config.Password);
    }

    [Fact]
    public void Parse_HttpUrl_KeepsSchemeAndPort()
    {
        var config = ProxyConfig.Parse("http://proxy.local:8080");

        Assert.NotNull(config);
        Assert.Equal("http://proxy.local:8080", config.Server);
    }

    [Fact]
    public void Parse_Socks5_PreservesScheme()
    {
        var config = ProxyConfig.Parse("socks5://127.0.0.1:1080");

        Assert.NotNull(config);
        Assert.Equal("socks5://127.0.0.1:1080", config.Server);
    }

    [Fact]
    public void Parse_EmbeddedCredentials_ExtractsAndUnescapes()
    {
        var config = ProxyConfig.Parse("http://alice:p%40ss@proxy.local:3128");

        Assert.NotNull(config);
        Assert.Equal("http://proxy.local:3128", config.Server);
        Assert.Equal("alice", config.Username);
        Assert.Equal("p@ss", config.Password);
    }

    [Fact]
    public void Parse_Garbage_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => ProxyConfig.Parse("::::"));
    }

    [Fact]
    public void ToEnvironmentValue_WithoutCredentials_IsServer()
    {
        var config = ProxyConfig.Parse("http://proxy.local:3128")!;

        Assert.Equal("http://proxy.local:3128", config.ToEnvironmentValue());
    }

    [Fact]
    public void ToEnvironmentValue_WithCredentials_ReEmbedsEscaped()
    {
        var config = ProxyConfig.Parse("http://alice:p%40ss@proxy.local:3128")!;

        Assert.Equal("http://alice:p%40ss@proxy.local:3128", config.ToEnvironmentValue());
    }

    [Fact]
    public void ToPlaywrightProxy_MapsAllFields()
    {
        var proxy = ProxyConfig.Parse("http://alice:secret@proxy.local:3128")!.ToPlaywrightProxy();

        Assert.Equal("http://proxy.local:3128", proxy.Server);
        Assert.Equal("alice", proxy.Username);
        Assert.Equal("secret", proxy.Password);
    }

    [Fact]
    public void Parse_SeparateCredentials_WinOverEmbeddedOnes()
    {
        var config = ProxyConfig.Parse("http://old:stale@proxy.local:3128", "alice", "secret");

        Assert.NotNull(config);
        Assert.Equal("http://proxy.local:3128", config.Server);
        Assert.Equal("alice", config.Username);
        Assert.Equal("secret", config.Password);
    }

    [Fact]
    public void Parse_BlankCredentials_FallBackToEmbeddedOnes()
    {
        var config = ProxyConfig.Parse("http://alice:secret@proxy.local:3128", "  ", "");

        Assert.NotNull(config);
        Assert.Equal("alice", config.Username);
        Assert.Equal("secret", config.Password);
    }

    /// <summary>The message ends up in the status line, where a password must not appear.</summary>
    [Fact]
    public void Parse_Garbage_DoesNotRepeatTheInput()
    {
        var ex = Assert.Throws<FormatException>(
            () => ProxyConfig.Parse("http://alice:s3cret@::::"));

        Assert.DoesNotContain("s3cret", ex.Message);
        Assert.DoesNotContain("alice", ex.Message);
    }

    /// <summary>The generated record ToString would print the password.</summary>
    [Fact]
    public void ToString_HidesThePassword()
    {
        var text = ProxyConfig.Parse("http://alice:s3cret@proxy.local:3128")!.ToString();

        Assert.DoesNotContain("s3cret", text);
        Assert.Contains("proxy.local", text);
        Assert.Contains("alice", text);
    }

    [Fact]
    public void Redact_RemovesCredentialsFromForeignText()
    {
        var config = ProxyConfig.Parse("http://alice:s3cret@proxy.local:3128")!;

        var cleaned = config.Redact("tunnel to proxy.local failed for alice with password s3cret");

        Assert.DoesNotContain("s3cret", cleaned);
        Assert.DoesNotContain("alice", cleaned);
        Assert.Contains("proxy.local", cleaned);
    }

    [Fact]
    public void Redact_WithoutCredentials_LeavesTextAlone()
    {
        var config = ProxyConfig.Parse("http://proxy.local:3128")!;

        Assert.Equal("no credentials here", config.Redact("no credentials here"));
    }
}
