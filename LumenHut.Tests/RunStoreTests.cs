using LumenHut.Data;
using LumenHut.Models;
using LumenHut.Services;
using Microsoft.EntityFrameworkCore;

namespace LumenHut.Tests;

/// <summary>
/// Storing and reading a run through the same code the application uses, against a temporary
/// database. This test previously existed as a copy of the persistence logic inside the
/// functional test, which meant the copy was verified and the real path was not.
/// </summary>
public class RunStoreTests : IAsyncLifetime
{
    private string _dbPath = string.Empty;

    public Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"lumenhut-store-{Guid.NewGuid():N}.db");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        TestEnvironment.DeleteDatabase(_dbPath);
        return Task.CompletedTask;
    }

    private static List<BrowserResult> SampleResults() =>
    [
        new BrowserResult
        {
            Browser = "Chromium",
            EngineVersion = "149.0.1",
            Metrics =
            {
                new PerformanceMetric { Name = "LCP", Value = 1234, Unit = "ms" },
                new PerformanceMetric { Name = "CLS", Value = null, Unit = "", Note = "N/A (not supported by this engine)" }
            }
        },
        new BrowserResult { Browser = "WebKit", Skipped = true, SkipReason = "engine not available" }
    ];

    [Fact]
    public async Task SavingCreatesTheSchemaAndKeepsEveryDetail()
    {
        var measuredAt = new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc);
        var context = new RunContext("macOS 15", "LumenHut 1.0.0", "1280x720", ProxyUsed: true);

        var id = await RunStore.SaveAsync("https://example.com/p", measuredAt, context, SampleResults(), _dbPath);

        var run = await RunStore.LoadAsync(id, _dbPath);

        Assert.NotNull(run);
        Assert.Equal("https://example.com/p", run.Url);
        Assert.Equal(measuredAt, run.Timestamp);
        Assert.Equal("macOS 15", run.OsDescription);
        Assert.Equal("LumenHut 1.0.0", run.ToolVersion);
        Assert.Equal("1280x720", run.Viewport);
        Assert.True(run.ProxyUsed);

        var chromium = run.BrowserResults.Single(b => b.Browser == "Chromium");
        Assert.Equal("149.0.1", chromium.EngineVersion);
        Assert.Equal(1234, chromium.Metrics.Single(m => m.Name == "LCP").Value);

        // A metric that was not measured stays null with its reason, rather than becoming 0.
        var cls = chromium.Metrics.Single(m => m.Name == "CLS");
        Assert.Null(cls.Value);
        Assert.Contains("N/A", cls.Note);

        var webkit = run.BrowserResults.Single(b => b.Browser == "WebKit");
        Assert.True(webkit.Skipped);
        Assert.Empty(webkit.Metrics);
    }

    [Fact]
    public async Task LoadingAMissingRunReturnsNull() =>
        Assert.Null(await RunStore.LoadAsync(4711, _dbPath));

    [Fact]
    public async Task RecentRunsComeBackNewestFirstAndCapped()
    {
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++)
        {
            await RunStore.SaveAsync($"https://example.com/{i}", baseTime.AddHours(i),
                new RunContext(null, null, null, false), SampleResults(), _dbPath);
        }

        var recent = await RunStore.LoadRecentAsync(3, _dbPath);

        Assert.Equal(3, recent.Count);
        Assert.Equal("https://example.com/4", recent[0].Url);
        Assert.Equal("https://example.com/2", recent[2].Url);
    }

    [Fact]
    public async Task ANoteCanBeAttachedAndCleared()
    {
        var id = await RunStore.SaveAsync("https://example.com", DateTime.UtcNow,
            new RunContext(null, null, null, false), SampleResults(), _dbPath);

        Assert.True(await RunStore.UpdateNotesAsync(id, "  after the image rework  ", _dbPath));
        Assert.Equal("after the image rework", (await RunStore.LoadAsync(id, _dbPath))!.Notes);

        Assert.True(await RunStore.UpdateNotesAsync(id, "   ", _dbPath));
        Assert.Null((await RunStore.LoadAsync(id, _dbPath))!.Notes);
    }

    [Fact]
    public async Task ANoteOnAMissingRunReportsThat() =>
        Assert.False(await RunStore.UpdateNotesAsync(4711, "note", _dbPath));
}

/// <summary>
/// The risky part of introducing migrations: databases created by the previous version with
/// EnsureCreated carry no migration history, and Migrate would try to create tables that are
/// already there. This reproduces such a database with the exact schema of the initial migration
/// and checks that upgrading keeps the data and adds the new columns.
/// </summary>
public class DatabaseInitializerTests : IAsyncLifetime
{
    private string _dbPath = string.Empty;

    public Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"lumenhut-legacy-{Guid.NewGuid():N}.db");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        TestEnvironment.DeleteDatabase(_dbPath);
        return Task.CompletedTask;
    }

    /// <summary>The schema the previous version produced: no run context, no migration history.</summary>
    private async Task CreateLegacyDatabaseAsync()
    {
        await using var db = new PerfDbContext(_dbPath);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "TestRuns" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_TestRuns" PRIMARY KEY AUTOINCREMENT,
                "Url" TEXT NOT NULL,
                "Timestamp" TEXT NOT NULL,
                "Notes" TEXT NULL)
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "BrowserResults" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BrowserResults" PRIMARY KEY AUTOINCREMENT,
                "TestRunId" INTEGER NOT NULL,
                "Browser" TEXT NOT NULL,
                "Skipped" INTEGER NOT NULL,
                "SkipReason" TEXT NULL,
                CONSTRAINT "FK_BrowserResults_TestRuns_TestRunId" FOREIGN KEY ("TestRunId")
                    REFERENCES "TestRuns" ("Id") ON DELETE CASCADE)
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "PerformanceMetrics" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PerformanceMetrics" PRIMARY KEY AUTOINCREMENT,
                "BrowserResultId" INTEGER NOT NULL,
                "Name" TEXT NOT NULL,
                "Value" REAL NULL,
                "Unit" TEXT NOT NULL,
                "Note" TEXT NULL,
                CONSTRAINT "FK_PerformanceMetrics_BrowserResults_BrowserResultId" FOREIGN KEY ("BrowserResultId")
                    REFERENCES "BrowserResults" ("Id") ON DELETE CASCADE)
            """);

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"TestRuns\" (\"Url\", \"Timestamp\", \"Notes\") VALUES ('https://legacy.example', '2026-01-02 03:04:05', 'kept')");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"BrowserResults\" (\"TestRunId\", \"Browser\", \"Skipped\") VALUES (1, 'Chromium', 0)");
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"PerformanceMetrics\" (\"BrowserResultId\", \"Name\", \"Value\", \"Unit\") VALUES (1, 'LCP', 999, 'ms')");
    }

    [Fact]
    public async Task UpgradingALegacyDatabaseKeepsItsData()
    {
        await CreateLegacyDatabaseAsync();

        await using (var db = new PerfDbContext(_dbPath))
            await DatabaseInitializer.InitializeAsync(db);

        var run = await RunStore.LoadAsync(1, _dbPath);

        Assert.NotNull(run);
        Assert.Equal("https://legacy.example", run.Url);
        Assert.Equal("kept", run.Notes);
        Assert.Equal(999, run.BrowserResults.Single().Metrics.Single().Value);

        // The columns the upgrade adds are present and simply empty for the old run.
        Assert.Null(run.OsDescription);
        Assert.False(run.ProxyUsed);
    }

    [Fact]
    public async Task ALegacyDatabaseEndsUpFullyMigrated()
    {
        await CreateLegacyDatabaseAsync();

        await using var db = new PerfDbContext(_dbPath);
        await DatabaseInitializer.InitializeAsync(db);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(db.Database.GetMigrations().Count(), (await db.Database.GetAppliedMigrationsAsync()).Count());
    }

    [Fact]
    public async Task AFreshDatabaseIsCreatedByTheMigrations()
    {
        await using var db = new PerfDbContext(_dbPath);
        await DatabaseInitializer.InitializeAsync(db);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(0, await db.TestRuns.CountAsync());
    }

    [Fact]
    public async Task InitializingTwiceIsHarmless()
    {
        await using var db = new PerfDbContext(_dbPath);
        await DatabaseInitializer.InitializeAsync(db);
        await DatabaseInitializer.InitializeAsync(db);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}
