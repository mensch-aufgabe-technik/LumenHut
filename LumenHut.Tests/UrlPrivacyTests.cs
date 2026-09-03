using LumenHut.Services;

namespace LumenHut.Tests;

/// <summary>
/// What a measured URL looks like once it reaches the database. The query string is where
/// session tokens, reset links and mail addresses live, so it is dropped unless asked for;
/// credentials in the URL are dropped either way.
/// </summary>
public class UrlPrivacyTests
{
    [Theory]
    [InlineData("https://example.com/reset?token=abc123", "https://example.com/reset")]
    [InlineData("https://example.com/p?email=a@b.de&id=7", "https://example.com/p")]
    [InlineData("https://example.com/page#section", "https://example.com/page")]
    [InlineData("example.com", "https://example.com/")]
    public void DropsTheQueryStringByDefault(string input, string expected) =>
        Assert.Equal(expected, UrlPrivacy.ForStorage(input, keepQuery: false));

    [Theory]
    [InlineData("https://example.com/reset?token=abc123", "https://example.com/reset?token=abc123")]
    [InlineData("localhost:3000/app?x=1", "http://localhost:3000/app?x=1")]
    public void KeepsTheQueryStringWhenAsked(string input, string expected) =>
        Assert.Equal(expected, UrlPrivacy.ForStorage(input, keepQuery: true));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AlwaysRemovesCredentialsFromTheUrl(bool keepQuery)
    {
        var stored = UrlPrivacy.ForStorage("https://alice:s3cret@example.com/area?a=1", keepQuery);

        Assert.DoesNotContain("alice", stored);
        Assert.DoesNotContain("s3cret", stored);
        Assert.StartsWith("https://example.com/area", stored);
    }

    [Fact]
    public void KeepsTheHostAndPortIntact() =>
        Assert.Equal("https://staging.example.com:8443/shop",
            UrlPrivacy.ForStorage("staging.example.com:8443/shop?ref=mail", keepQuery: false));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReturnsEmptyForBlankInput(string input) =>
        Assert.Equal(string.Empty, UrlPrivacy.ForStorage(input, keepQuery: false));
}
