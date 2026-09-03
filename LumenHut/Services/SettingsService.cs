using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using LumenHut.Models;

namespace LumenHut.Services;

public sealed class AppSettings
{
    /// <summary>Proxy address without credentials, e.g. "http://proxy.local:3128".</summary>
    public string ProxyServer { get; set; } = string.Empty;

    /// <summary>Proxy user name. The password is deliberately not part of this file.</summary>
    public string ProxyUsername { get; set; } = string.Empty;

    /// <summary>Null = never chosen; the UI then follows the system language.</summary>
    public AppLanguage? Language { get; set; }

    /// <summary>
    /// Whether the query string of a measured URL is stored. Off by default: query strings
    /// routinely carry session tokens, reset links and mail addresses.
    /// </summary>
    public bool StoreFullUrl { get; set; }

    /// <summary>
    /// Days after which stored runs are removed on startup. 0 = keep everything, which stays the
    /// default: silently deleting a user's existing measurements would be the wrong surprise.
    /// </summary>
    public int RetentionDays { get; set; }
}

/// <summary>
/// Persists app settings as JSON next to the SQLite database in LocalApplicationData/LumenHut.
/// Kept out of the EF database on purpose: the file stays readable and editable by hand, and a
/// broken settings file cannot take the measurement data with it. (The original reason — that
/// EnsureCreated never adds tables to an existing database — no longer applies now that the
/// database is migrated, but the separation has earned its place.)
/// </summary>
public static class SettingsService
{
    private static string GetSettingsPath() => Path.Combine(EnsureDataDirectory(), "settings.json");

    /// <summary>Directory holding settings.json and perfdata.db; shown in the settings view.</summary>
    public static string GetDataDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LumenHut");

    /// <summary>
    /// Creates the data directory if needed and restricts it to the current user. Measured URLs
    /// and the proxy user name land here; the default 0755 would expose them to every local
    /// account. On Windows the LocalAppData ACL already does this.
    /// </summary>
    public static string EnsureDataDirectory()
    {
        var dir = GetDataDirectory();
        Directory.CreateDirectory(dir);

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(dir,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A directory we cannot chmod is still usable; nothing to gain from failing here.
            }
        }

        return dir;
    }

    /// <summary>
    /// Applies the persisted UI language. Must run before the first page view model exists:
    /// <see cref="ViewModels.PageViewModelBase"/> subscribes to <see cref="Strings"/> in its own
    /// constructor, so a page that set the language from its constructor received its own change
    /// notification with its fields still unassigned — which crashed the app on every start after
    /// the language had once been switched away from the system language.
    /// </summary>
    public static void ApplyPersistedLanguage()
    {
        var language = Load().Language;
        if (language.HasValue)
            Strings.Instance.Language = language.Value;
    }

    /// <summary>Returns defaults if the file is missing or unreadable — a broken
    /// settings file must not prevent app startup.</summary>
    public static AppSettings Load()
    {
        AppSettings settings;
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path))
                return new AppSettings();

            settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions)
                       ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }

        return MigrateEmbeddedCredentials(settings);
    }

    /// <summary>
    /// Earlier versions stored "http://user:pass@host:port" verbatim. Such a file still holds a
    /// password in plain text, so the credentials are split out and the file is rewritten without
    /// the password on the first start after the upgrade.
    /// </summary>
    private static AppSettings MigrateEmbeddedCredentials(AppSettings settings)
    {
        if (!settings.ProxyServer.Contains('@', StringComparison.Ordinal))
            return settings;

        try
        {
            var parsed = ProxyConfig.Parse(settings.ProxyServer);
            if (parsed == null)
                return settings;

            settings.ProxyServer = parsed.Server;
            if (string.IsNullOrEmpty(settings.ProxyUsername) && parsed.Username != null)
                settings.ProxyUsername = parsed.Username;
        }
        catch (FormatException)
        {
            return settings;
        }

        try
        {
            Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the value in memory is already clean.
        }

        return settings;
    }

    /// <summary>Throws IOException/UnauthorizedAccessException on failure; callers surface it in the UI.</summary>
    public static void Save(AppSettings settings)
    {
        var path = GetSettingsPath();
        var temp = path + ".tmp";

        // Write-then-replace: a crash mid-write must not leave a truncated settings file behind.
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
        RestrictToOwner(temp);
        File.Move(temp, path, overwrite: true);
    }

    private static void RestrictToOwner(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // AppLanguage as "German"/"English" instead of an int: settings.json stays hand-editable.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
