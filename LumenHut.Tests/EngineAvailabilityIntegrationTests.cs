using Microsoft.Playwright;
using LumenHut.Data;
using LumenHut.Services;
using Xunit;

namespace LumenHut.Tests;

/// <summary>
/// Integration tests validating cross-platform browser engine availability.
/// If an engine is unavailable on the OS, the application must skip the test node
/// and proactively inform the user. These tests exercise the launch behavior.
/// </summary>
[Trait("Category", "Integration")]
public class EngineAvailabilityIntegrationTests : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
    }

    public Task DisposeAsync()
    {
        _playwright?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Chromium_IsAvailable_OnCurrentPlatform()
    {
        Assert.NotNull(_playwright);
        var browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        Assert.NotNull(browser);
        await browser.CloseAsync();
    }

    [Fact]
    public async Task Firefox_IsAvailable_OnCurrentPlatform()
    {
        Assert.NotNull(_playwright);
        var browser = await _playwright.Firefox.LaunchAsync(new() { Headless = true });
        Assert.NotNull(browser);
        await browser.CloseAsync();
    }

    [Fact]
    public async Task WebKit_Availability_DependsOnPlatform()
    {
        Assert.NotNull(_playwright);

        // WebKit may be unavailable on some platforms; the service must skip gracefully.
        try
        {
            var browser = await _playwright.Webkit.LaunchAsync(new() { Headless = true });
            Assert.NotNull(browser);
            await browser.CloseAsync();
        }
        catch (PlaywrightException ex)
        {
            Assert.True(
                ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("executable", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("WebKit", StringComparison.OrdinalIgnoreCase),
                $"Unexpected WebKit failure message: {ex.Message}");
        }
    }
}

/// <summary>
/// End-to-end functional validation of the service + direct DbContext persistence.
/// Only real metrics, N/A for unsupported, DbContext as UoW. Uses a temp database
/// so the real user data in LocalApplicationData is never touched.
/// </summary>
[Trait("Category", "Integration")]
public class PerfMeasurementFunctionalTests : IAsyncLifetime
{
    private PlaywrightPerfService? _service;
    private string _dbPath = string.Empty;

    public Task InitializeAsync()
    {
        _service = new PlaywrightPerfService();
        _dbPath = Path.Combine(Path.GetTempPath(), $"lumenhut-test-{Guid.NewGuid():N}.db");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_service != null) await _service.DisposeAsync();
        TestEnvironment.DeleteDatabase(_dbPath);
    }

    [Fact]
    public async Task RunTest_CollectsRealMetrics_OrMarksNA_AndPersistsViaDbContextDirectly()
    {
        Assert.NotNull(_service);

        // Use a stable, fast loading public page
        var url = "https://example.com";
        var browsers = new[] { "Chromium", "Firefox", "WebKit" };

        var results = await _service.RunTestsAsync(url, browsers);

        Assert.NotEmpty(results);
        Assert.Equal(3, results.Count);

        // A skipped engine carries no metrics by design, only a reason — the application
        // supports exactly that case (WebKit on Windows, failed navigation).
        Assert.All(results, r =>
        {
            if (r.Skipped)
                Assert.False(string.IsNullOrWhiteSpace(r.SkipReason));
            else
                Assert.Contains(r.Metrics, m => m.Name is "LoadTime" or "TTFB" or "LCP" or "FCP");
        });

        // Diagnostics
        foreach (var r in results)
        {
            Console.WriteLine($"[DIAG] Browser={r.Browser} Skipped={r.Skipped} Reason={r.SkipReason}");
            foreach (var m in r.Metrics)
                Console.WriteLine($"   {m.Name} = {m.Value?.ToString() ?? "null"} {m.Unit} note={m.Note ?? "-"}");
        }

        // Persist through the same code the application uses.
        var runId = await RunStore.SaveAsync(
            url,
            DateTime.UtcNow,
            RunContext.Current(proxyUsed: false),
            results,
            _dbPath);

        var persisted = await RunStore.LoadAsync(runId, _dbPath);

        Assert.NotNull(persisted);
        Assert.Equal(url, persisted.Url);
        Assert.NotEmpty(persisted.BrowserResults);

        // Validate anti-hallucination: no metric invented beyond known list
        var allowed = new HashSet<string> { "LCP", "FCP", "CLS", "LoadTime", "TTFB", "INP", "DOMContentLoaded" };
        foreach (var metric in persisted.BrowserResults.SelectMany(b => b.Metrics))
        {
            Assert.Contains(metric.Name, allowed);
            if (!metric.Value.HasValue)
            {
                Assert.NotNull(metric.Note);
                Assert.Contains("N/A", metric.Note);
            }
        }
    }
}
