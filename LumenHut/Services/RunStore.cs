using LumenHut.Data;
using LumenHut.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LumenHut.Services;

/// <summary>Conditions a run was measured under, stored alongside the numbers.</summary>
public sealed record RunContext(string? OsDescription, string? ToolVersion, string? Viewport, bool ProxyUsed)
{
    /// <summary>Everything known about this machine and build; the proxy is a yes or no, never the address.</summary>
    public static RunContext Current(bool proxyUsed) => new(
        System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        AppInfo.NameAndVersion,
        AppInfo.Viewport,
        proxyUsed);
}

/// <summary>
/// Reading and writing runs. DbContext used directly as the unit of work, no repository
/// abstraction — the point of this class is that the production path and the tests use the same
/// code, which they did not while every caller wrote its own persistence block.
/// </summary>
public static class RunStore
{
    private static PerfDbContext CreateContext(string? dbPath) =>
        dbPath == null ? new PerfDbContext() : new PerfDbContext(dbPath);

    /// <summary>Stores one run and returns its id.</summary>
    public static async Task<int> SaveAsync(string url, DateTime measuredAtUtc, RunContext context,
        IEnumerable<BrowserResult> results, string? dbPath = null)
    {
        await using var db = CreateContext(dbPath);
        await DatabaseInitializer.InitializeAsync(db);

        var run = new TestRun
        {
            Url = url,
            Timestamp = measuredAtUtc,
            OsDescription = context.OsDescription,
            ToolVersion = context.ToolVersion,
            Viewport = context.Viewport,
            ProxyUsed = context.ProxyUsed
        };

        foreach (var result in results)
        {
            // Copied rather than attached: the instances belong to the measurement service.
            var stored = new BrowserResult
            {
                Browser = result.Browser,
                EngineVersion = result.EngineVersion,
                Skipped = result.Skipped,
                SkipReason = result.SkipReason
            };

            foreach (var metric in result.Metrics)
            {
                stored.Metrics.Add(new PerformanceMetric
                {
                    Name = metric.Name,
                    Value = metric.Value,
                    Unit = metric.Unit,
                    Note = metric.Note
                });
            }

            run.BrowserResults.Add(stored);
        }

        db.TestRuns.Add(run);
        await db.SaveChangesAsync();
        return run.Id;
    }

    /// <summary>One run with its results and metrics, or null if it is gone.</summary>
    public static async Task<TestRun?> LoadAsync(int runId, string? dbPath = null)
    {
        await using var db = CreateContext(dbPath);
        await DatabaseInitializer.InitializeAsync(db);

        return await db.TestRuns
            .AsNoTracking()
            .Include(r => r.BrowserResults)
            .ThenInclude(b => b.Metrics)
            .FirstOrDefaultAsync(r => r.Id == runId);
    }

    /// <summary>The newest runs, results included but metrics left out — the history only needs names.</summary>
    public static async Task<List<TestRun>> LoadRecentAsync(int max, string? dbPath = null)
    {
        await using var db = CreateContext(dbPath);
        await DatabaseInitializer.InitializeAsync(db);

        return await db.TestRuns
            .AsNoTracking()
            .Include(r => r.BrowserResults)
            .OrderByDescending(r => r.Timestamp)
            .Take(max)
            .ToListAsync();
    }

    /// <summary>Attaches or replaces the free-text note of a run. False if the run is gone.</summary>
    public static async Task<bool> UpdateNotesAsync(int runId, string? notes, string? dbPath = null)
    {
        await using var db = CreateContext(dbPath);
        await DatabaseInitializer.InitializeAsync(db);

        var run = await db.TestRuns.FirstOrDefaultAsync(r => r.Id == runId);
        if (run == null)
            return false;

        run.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await db.SaveChangesAsync();
        return true;
    }
}
