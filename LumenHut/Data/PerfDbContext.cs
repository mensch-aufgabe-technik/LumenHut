using Microsoft.EntityFrameworkCore;
using LumenHut.Models;
using LumenHut.Services;
using System;
using System.IO;

namespace LumenHut.Data;

public class PerfDbContext : DbContext
{
    public DbSet<TestRun> TestRuns { get; set; }
    public DbSet<BrowserResult> BrowserResults { get; set; }
    public DbSet<PerformanceMetric> PerformanceMetrics { get; set; }

    private readonly string _dbPath;

    public PerfDbContext() : this(GetDefaultDbPath())
    {
    }

    /// <summary>Explicit path variant, used by tests to avoid touching the real user database.</summary>
    public PerfDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    private static string GetDefaultDbPath()
    {
        // Shared with the settings file, and restricted to the current user there: the database
        // holds measured URLs, which are the most sensitive thing this application stores.
        return Path.Combine(SettingsService.EnsureDataDirectory(), "perfdata.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestRun>()
            .HasMany(t => t.BrowserResults)
            .WithOne(b => b.TestRun)
            .HasForeignKey(b => b.TestRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BrowserResult>()
            .HasMany(b => b.Metrics)
            .WithOne(m => m.BrowserResult)
            .HasForeignKey(m => m.BrowserResultId)
            .OnDelete(DeleteBehavior.Cascade);

        // The history always sorts by time and takes the newest entries.
        modelBuilder.Entity<TestRun>()
            .HasIndex(t => t.Timestamp);
    }
}
