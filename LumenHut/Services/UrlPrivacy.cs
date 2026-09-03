using System;

namespace LumenHut.Services;

/// <summary>
/// Reduces a measured URL to what is worth keeping. The URL is the only free text field in the
/// application and goes into an unencrypted database, the history list and every export — so
/// session tokens, reset links, invitation tokens and <c>?email=</c> parameters would live there
/// indefinitely. Credentials always go; the query string goes unless the user opts in.
/// </summary>
public static class UrlPrivacy
{
    public static string ForStorage(string url, bool keepQuery)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var value = url.Trim();

        if (!Uri.TryCreate(PlaywrightPerfService.NormalizeUrl(value), UriKind.Absolute, out var uri))
        {
            // Unparseable input is stored as typed, minus anything that looks like a query.
            return keepQuery ? value : value.Split('?', '#')[0];
        }

        // Authority excludes user information, so credentials are dropped here.
        var stored = $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";

        return keepQuery ? stored + uri.Query + uri.Fragment : stored;
    }
}
