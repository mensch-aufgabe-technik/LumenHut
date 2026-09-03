using System.Reflection;

namespace LumenHut.Services;

/// <summary>
/// Facts about this build that belong in an exported report: without them a number cannot be
/// reproduced later. The measurement conditions themselves (engine versions, OS) still need the
/// data model work in phase 6.
/// </summary>
public static class AppInfo
{
    /// <summary>Viewport every measurement uses; kept in one place so report and service agree.</summary>
    public const string Viewport = "1280x720";

    public static string Version { get; } = ReadVersion();

    public static string NameAndVersion => $"LumenHut {Version}";

    private static string ReadVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString()
                      ?? "unknown";

        // The SDK appends "+<commit sha>" to InformationalVersion.
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }
}
