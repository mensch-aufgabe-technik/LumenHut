using Microsoft.Data.Sqlite;
using System.Runtime.CompilerServices;

namespace LumenHut.Tests;

internal static class TestEnvironment
{
    /// <summary>
    /// Runs before any test. Redirects the application log into a temporary directory: without
    /// this, a test run appends to the log file of whoever is running the tests.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lumenhut-tests");
        Directory.CreateDirectory(directory);
        Environment.SetEnvironmentVariable("LUMENHUT_LOG_DIR", directory);
    }

    /// <summary>
    /// Deletes a temporary database and its journal files. Windows refuses to delete a file while
    /// a handle is open, and Microsoft.Data.Sqlite pools its connections — so the pool has to be
    /// cleared first. macOS and Linux delete an open file without complaint, which is why this
    /// only ever failed on the Windows runner.
    /// </summary>
    internal static void DeleteDatabase(string dbPath)
    {
        SqliteConnection.ClearAllPools();

        foreach (var file in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
            if (File.Exists(file))
                File.Delete(file);
    }
}
