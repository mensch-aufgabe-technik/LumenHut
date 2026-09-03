using System;
using Microsoft.Playwright;

namespace LumenHut.Models;

/// <summary>
/// Parsed proxy setting. Accepts "host:port", "http://host:port", "socks5://host:port",
/// optionally with embedded credentials ("http://user:pass@host:port").
/// </summary>
public sealed record ProxyConfig(string Server, string? Username, string? Password)
{
    /// <summary>
    /// Parses user input into a proxy config. Returns null for blank input (= use defaults).
    /// Credentials passed explicitly win over any embedded in the address.
    /// Throws FormatException on unparseable input so the caller can show a message — the
    /// exception never repeats the input, which may carry a password.
    /// </summary>
    public static ProxyConfig? Parse(string? input, string? username = null, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var value = input.Trim();
        var hasScheme = value.Contains("://", StringComparison.Ordinal);

        if (!Uri.TryCreate(hasScheme ? value : "http://" + value, UriKind.Absolute, out var uri)
            || string.IsNullOrEmpty(uri.Host))
        {
            throw new FormatException("Invalid proxy address. Expected e.g. http://proxy.local:3128");
        }

        string? embeddedUser = null, embeddedPassword = null;
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            embeddedUser = Uri.UnescapeDataString(parts[0]);
            embeddedPassword = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : null;
        }

        var scheme = hasScheme ? uri.Scheme : "http";
        // Uri reports -1 when the scheme has no default port (e.g. socks5 without explicit port).
        var server = uri.Port >= 0 ? $"{scheme}://{uri.Host}:{uri.Port}" : $"{scheme}://{uri.Host}";

        return new ProxyConfig(
            server,
            Blank(username) ? embeddedUser : username!.Trim(),
            Blank(password) ? embeddedPassword : password);
    }

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    public Proxy ToPlaywrightProxy() => new()
    {
        Server = Server,
        Username = Username,
        Password = Password
    };

    /// <summary>
    /// Value for HTTP(S)_PROXY env vars (the browser installer only reads those,
    /// never OS proxy settings), with credentials re-embedded and escaped.
    /// </summary>
    public string ToEnvironmentValue()
    {
        if (Username == null)
            return Server;

        var idx = Server.IndexOf("://", StringComparison.Ordinal);
        var credentials = Uri.EscapeDataString(Username)
                          + (Password != null ? ":" + Uri.EscapeDataString(Password) : "");
        return $"{Server[..(idx + 3)]}{credentials}@{Server[(idx + 3)..]}";
    }

    /// <summary>
    /// Safe for status messages and logs. The generated record ToString would print the
    /// password, and one line of logging is all it takes for that to leave the process.
    /// </summary>
    public override string ToString() =>
        Username == null ? Server : $"{Server} (user {Username}, password not shown)";

    /// <summary>Removes the credentials of this proxy from arbitrary text, so that a message
    /// coming out of a browser or installer cannot carry them into the database or an export.</summary>
    public string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (!string.IsNullOrEmpty(Password))
            text = text.Replace(Password, "***", StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(Username))
            text = text.Replace(Username, "***", StringComparison.Ordinal);

        return text;
    }
}
