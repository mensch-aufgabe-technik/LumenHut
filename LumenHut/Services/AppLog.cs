using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;

namespace LumenHut.Services;

/// <summary>
/// The application's log: one file next to the database, in the directory that is restricted to
/// the current user.
/// </summary>
/// <remarks>
/// A tool that runs behind corporate proxies and downloads several hundred megabytes on first
/// use cannot be supported without a log. What may be logged is therefore fixed here: never a
/// proxy password (ProxyConfig.ToString masks it), never a full URL with its query string (call
/// sites reduce it through <see cref="UrlPrivacy"/> first), and no measured page content. EF
/// Core's own logging stays off, because its SQL parameters would contain exactly that.
/// </remarks>
public static class AppLog
{
    private const long MaxBytes = 1_000_000;

    private static readonly Lazy<ILoggerFactory> LazyFactory = new(CreateFactory);

    /// <summary>
    /// Log file location. LUMENHUT_LOG_DIR redirects it — the test suite points it at a temporary
    /// directory so a test run does not write into the user's log.
    /// </summary>
    public static string LogPath
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable("LUMENHUT_LOG_DIR");
            var directory = string.IsNullOrWhiteSpace(overridden)
                ? SettingsService.GetDataDirectory()
                : overridden;

            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "lumenhut.log");
        }
    }

    public static ILogger<T> For<T>() => LazyFactory.Value.CreateLogger<T>();

    public static void Shutdown()
    {
        if (LazyFactory.IsValueCreated)
            LazyFactory.Value.Dispose();
    }

    private static ILoggerFactory CreateFactory() => LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddProvider(new FileLoggerProvider(LogPath, MaxBytes));
    });
}

/// <summary>Minimal file sink: one line per entry, one rotation, failures stay silent.</summary>
internal sealed class FileLoggerProvider(string path, long maxBytes) : ILoggerProvider
{
    private readonly object _gate = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    internal void Write(string line)
    {
        lock (_gate)
        {
            try
            {
                Rotate();
                File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                RestrictToOwner();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never be the reason something fails.
            }
        }
    }

    private void Rotate()
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < maxBytes)
            return;

        var previous = path + ".1";
        File.Move(path, previous, overwrite: true);
    }

    private void RestrictToOwner()
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

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var shortCategory = category[(category.LastIndexOf('.') + 1)..];
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {Level(logLevel)} {shortCategory} {formatter(state, exception)}";

            if (exception != null)
                line += $" | {exception.GetType().Name}: {exception.Message}";

            provider.Write(line);
        }

        private static string Level(LogLevel level) => level switch
        {
            LogLevel.Critical => "CRIT",
            LogLevel.Error => "ERR ",
            LogLevel.Warning => "WARN",
            LogLevel.Information => "INFO",
            _ => "DBG "
        };
    }
}
