using LumenHut.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace LumenHut.Data;

/// <summary>
/// Brings a database up to the current schema.
/// </summary>
/// <remarks>
/// Earlier versions created the schema with <c>EnsureCreated</c>, which writes no migration
/// history. Running <c>Migrate</c> against such a database would try to create tables that are
/// already there and fail. Existing databases are therefore stamped with the initial migration
/// once — the schema it describes is exactly what <c>EnsureCreated</c> produced — after which
/// every later migration applies normally. New databases are created by the migrations
/// themselves, so from here on a schema change is a migration and no longer a data loss risk.
/// </remarks>
public static class DatabaseInitializer
{
    private static readonly ILogger Log = AppLog.For<PerfDbContext>();

    public static async Task InitializeAsync(PerfDbContext db)
    {
        var applied = await db.Database.GetAppliedMigrationsAsync();

        if (!applied.Any() && await HasLegacySchemaAsync(db))
        {
            Log.LogInformation("Database without migration history found; stamping it before migrating");
            await StampInitialMigrationAsync(db);
        }

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
            Log.LogInformation("Applying migrations: {Migrations}", string.Join(", ", pending));

        await db.Database.MigrateAsync();
    }

    /// <summary>True when the tables exist but no migration has ever been recorded.</summary>
    private static async Task<bool> HasLegacySchemaAsync(PerfDbContext db)
    {
        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT name AS \"Value\" FROM sqlite_master WHERE type = 'table' AND name = 'TestRuns'")
            .ToListAsync();

        return tables.Count > 0;
    }

    private static async Task StampInitialMigrationAsync(PerfDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (" +
            "\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, " +
            "\"ProductVersion\" TEXT NOT NULL)");

        var migrations = db.Database.GetMigrations().ToList();

        // A database created by EnsureCreated from a newer model already has the later columns.
        // Stamping only the initial migration would then make the next one try to add a column
        // that exists — so check the schema instead of assuming which version wrote it.
        var alreadyCurrent = await ColumnExistsAsync(db, "TestRuns", "OsDescription");
        var toStamp = alreadyCurrent ? migrations : migrations.Take(1);

        foreach (var migration in toStamp)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1})",
                migration,
                ProductInfo.Version);
        }
    }

    private static async Task<bool> ColumnExistsAsync(PerfDbContext db, string table, string column)
    {
        // Both values parameterized: pragma_table_info is a table-valued function and takes the
        // table name as an argument, so nothing has to be pasted into the statement.
        var found = await db.Database
            .SqlQueryRaw<string>(
                "SELECT name AS \"Value\" FROM pragma_table_info({0}) WHERE name = {1}", table, column)
            .ToListAsync();

        return found.Count > 0;
    }
}

/// <summary>EF Core version recorded in the migration history, kept in one place.</summary>
internal static class ProductInfo
{
    internal const string Version = "10.0.9";
}
