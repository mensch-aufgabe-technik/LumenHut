using LumenHut.Services;

namespace LumenHut.Tests;

/// <summary>
/// Completing a typed address into a URL. Runs without a browser, so it is a unit test rather
/// than part of the functional suite.
/// </summary>
public class UrlNormalizationTests
{
    [Theory]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("http://example.com/path?a=1", "http://example.com/path?a=1")]
    [InlineData("HTTPS://Example.com", "HTTPS://Example.com")]
    public void KeepsAnExplicitScheme(string input, string expected) =>
        Assert.Equal(expected, PlaywrightPerfService.NormalizeUrl(input));

    [Theory]
    [InlineData("example.com", "https://example.com")]
    [InlineData("example.com:8443/status", "https://example.com:8443/status")]
    [InlineData("  example.com  ", "https://example.com")]
    public void PrefixesHttpsForPublicHosts(string input, string expected) =>
        Assert.Equal(expected, PlaywrightPerfService.NormalizeUrl(input));

    [Theory]
    [InlineData("localhost:3000", "http://localhost:3000")]
    [InlineData("LOCALHOST", "http://LOCALHOST")]
    [InlineData("127.0.0.1:8080/health", "http://127.0.0.1:8080/health")]
    [InlineData("app.test", "http://app.test")]
    [InlineData("nas.local/admin", "http://nas.local/admin")]
    [InlineData("api.localhost:5000", "http://api.localhost:5000")]
    public void PrefixesHttpForLocalDevelopmentTargets(string input, string expected) =>
        Assert.Equal(expected, PlaywrightPerfService.NormalizeUrl(input));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FallsBackToAboutBlankForEmptyInput(string? input) =>
        Assert.Equal("about:blank", PlaywrightPerfService.NormalizeUrl(input!));

    /// <summary>
    /// A non-http scheme keeps getting a scheme prefixed, which makes the address unresolvable.
    /// That is deliberate: it keeps file:// and friends unreachable from the URL field.
    /// </summary>
    [Fact]
    public void DoesNotOpenNonHttpSchemes() =>
        Assert.Equal("https://file:///etc/passwd", PlaywrightPerfService.NormalizeUrl("file:///etc/passwd"));
}
