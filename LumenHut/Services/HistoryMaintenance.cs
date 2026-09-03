using LumenHut.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LumenHut.Services;

/// <summary>
/// Deleting stored runs. Kept out of the view models because the retention pass runs at startup,
/// before any page is shown. Uses the DbContext directly, like the rest of the application.
/// </summary>
/// <remarks>
/// Every method takes an optional database path so that deletion — the one operation here that
/// cannot be undone — is testable against a temporary database instead of the real user data.
/// </remarks>
public static class HistoryMaintenance
{
    private static PerfDbContext CreateContext(string? dbPath) =>
        dbPath == null ? new PerfDbContext() : new PerfDbContext(dbPath);

    /// <summary>Deletes a single run and its results. Returns false if it was already gone.</summary>
    public static async Task<bool> DeleteRunAsync(int runId, string? dbPath = null)
    {
        await using var db = CreateContext(dbPath);
        await DatabaseInitializer.InitializeAsync(db);

        var run = await db.TestRuns
            .Include(r => r.BrowserResults)
            .ThenInclude(b => b.Metrics)
            .FirstOrDefaultAsync(r => r.Id == runId);

        if (run == null)
            return false;

        db.TestRuns.Remove(run);
        await db.SaveChangesAsync();
        await ReclaimSpaceAsync(db);
        return true;
    }

    /// <summary>Deletes every stored run. Returns how many were removed.</summary>
    public static async Task<int> ClearAsync(string? dbPath = null)
    {
        await using var db = CreateContext(dbPath);
        await DatabaseInitializer.InitializeAsync(db);

        var runs = await db.TestRuns
            .Include(r => r.BrowserResults)
            .ThenInclude(b => b.Metrics)
            .ToListAsync();

        if (runs.Count == 0)
            return 0;

        db.TestRuns.RemoveRange(runs);
        await db.SaveChangesAsync();
        await ReclaimSpaceAsync(db);
        return runs.Count;
    }

    /// <summary>
    /// Deletes runs older than the retention period. Returns how many were removed;
    /// 0 or less days means "keep everything".
    /// </summary>
    public static async Task<int> ApplyRetentionAsync(int retentionDays, string? dbPath = null)
    {
        if (retentionDays <= 0)
            return 0;

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        await using var db = CreateContext(dbPath);
        await DatabaseInitializer.InitializeAsync(db);

        var expired = await db.TestRuns
            .Include(r => r.BrowserResults)
            .ThenInclude(b => b.Metrics)
            .Where(r => r.Timestamp < cutoff)
            .ToListAsync();

        if (expired.Count == 0)
            return 0;

        db.TestRuns.RemoveRange(expired);
        await db.SaveChangesAsync();
        await ReclaimSpaceAsync(db);
        return expired.Count;
    }

    /// <summary>
    /// VACUUM after deleting: SQLite keeps freed pages in the file and in the write-ahead log,
    /// so a deleted URL would still be readable in the raw file without this.
    /// </summary>
    private static async Task ReclaimSpaceAsync(PerfDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("VACUUM;");
    }
}
