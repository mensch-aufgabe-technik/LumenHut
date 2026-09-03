using LumenHut.Data;
using LumenHut.Models;
using LumenHut.Services;
using Microsoft.EntityFrameworkCore;

namespace LumenHut.Tests;

/// <summary>
/// Deleting stored runs — the one operation in the application that cannot be undone, and the
/// one a data subject request depends on. Runs against a temporary database; the real user data
/// is never touched.
/// </summary>
public class HistoryMaintenanceTests : IAsyncLifetime
{
    private string _dbPath = string.Empty;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"lumenhut-maintenance-{Guid.NewGuid():N}.db");
        await using var db = new PerfDbContext(_dbPath);
        await DatabaseInitializer.InitializeAsync(db);
    }

    public Task DisposeAsync()
    {
        TestEnvironment.DeleteDatabase(_dbPath);
        return Task.CompletedTask;
    }

    private async Task<int> SeedRunAsync(string url, DateTime timestampUtc)
    {
        await using var db = new PerfDbContext(_dbPath);
        var run = new TestRun
        {
            Url = url,
            Timestamp = timestampUtc,
            BrowserResults =
            {
                new BrowserResult
                {
                    Browser = "Chromium",
                    Metrics = { new PerformanceMetric { Name = "LCP", Value = 1200, Unit = "ms" } }
                }
            }
        };

        db.TestRuns.Add(run);
        await db.SaveChangesAsync();
        return run.Id;
    }

    private async Task<(int Runs, int Results, int Metrics)> CountAsync()
    {
        await using var db = new PerfDbContext(_dbPath);
        return (await db.TestRuns.CountAsync(),
                await db.BrowserResults.CountAsync(),
                await db.PerformanceMetrics.CountAsync());
    }

    [Fact]
    public async Task DeletingARunRemovesItsResultsAndMetricsToo()
    {
        var keep = await SeedRunAsync("https://keep.example", DateTime.UtcNow);
        var drop = await SeedRunAsync("https://drop.example", DateTime.UtcNow);

        Assert.True(await HistoryMaintenance.DeleteRunAsync(drop, _dbPath));

        var (runs, results, metrics) = await CountAsync();
        Assert.Equal(1, runs);
        Assert.Equal(1, results);
        Assert.Equal(1, metrics);

        await using var db = new PerfDbContext(_dbPath);
        Assert.Equal(keep, (await db.TestRuns.SingleAsync()).Id);
    }

    [Fact]
    public async Task DeletingAMissingRunReportsThat() =>
        Assert.False(await HistoryMaintenance.DeleteRunAsync(4711, _dbPath));

    [Fact]
    public async Task ClearingRemovesEverything()
    {
        await SeedRunAsync("https://a.example", DateTime.UtcNow);
        await SeedRunAsync("https://b.example", DateTime.UtcNow);

        Assert.Equal(2, await HistoryMaintenance.ClearAsync(_dbPath));
        Assert.Equal((0, 0, 0), await CountAsync());
    }

    [Fact]
    public async Task ClearingAnEmptyHistoryDoesNothing() =>
        Assert.Equal(0, await HistoryMaintenance.ClearAsync(_dbPath));

    [Fact]
    public async Task RetentionRemovesOnlyRunsPastTheCutoff()
    {
        await SeedRunAsync("https://old.example", DateTime.UtcNow.AddDays(-100));
        await SeedRunAsync("https://recent.example", DateTime.UtcNow.AddDays(-10));

        Assert.Equal(1, await HistoryMaintenance.ApplyRetentionAsync(90, _dbPath));

        await using var db = new PerfDbContext(_dbPath);
        Assert.Equal("https://recent.example", (await db.TestRuns.SingleAsync()).Url);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RetentionOfZeroOrLessKeepsEverything(int days)
    {
        await SeedRunAsync("https://ancient.example", DateTime.UtcNow.AddYears(-5));

        Assert.Equal(0, await HistoryMaintenance.ApplyRetentionAsync(days, _dbPath));
        Assert.Equal(1, (await CountAsync()).Runs);
    }
}
