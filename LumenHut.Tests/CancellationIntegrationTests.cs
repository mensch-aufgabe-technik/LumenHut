using LumenHut.Models;
using LumenHut.Services;

namespace LumenHut.Tests;

/// <summary>
/// Cancelling a run. Playwright has no CancellationToken, so this verifies the mechanism the
/// service uses instead: closing the context underneath a navigation in flight, and reporting
/// that as cancellation rather than as a measurement error.
/// </summary>
[Trait("Category", "Integration")]
public class CancellationIntegrationTests
{
    [Fact]
    public async Task ARunCancelledMidFlightReportsCancellation()
    {
        await using var service = new PlaywrightPerfService();
        using var cancellation = new CancellationTokenSource();

        var reported = new List<BrowserResult>();
        var progress = new SynchronousProgress(reported.Add);

        cancellation.CancelAfter(TimeSpan.FromMilliseconds(400));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.RunTestsAsync(
                "https://example.com",
                new[] { "Chromium", "Firefox", "WebKit" },
                proxy: null,
                progress: progress,
                cancellationToken: cancellation.Token));

        // Whatever had finished before the cancellation stays available to the caller, so a
        // cancelled run does not have to throw away what it already measured.
        Assert.True(reported.Count < 3, $"Expected an incomplete run, got {reported.Count} results.");
    }

    [Fact]
    public async Task AnAlreadyCancelledRunStartsNoBrowser()
    {
        await using var service = new PlaywrightPerfService();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var reported = new List<BrowserResult>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.RunTestsAsync(
                "https://example.com",
                new[] { "Chromium" },
                proxy: null,
                progress: new SynchronousProgress(reported.Add),
                cancellationToken: cancellation.Token));

        Assert.Empty(reported);
    }

    private sealed class SynchronousProgress(Action<BrowserResult> onResult) : IProgress<BrowserResult>
    {
        public void Report(BrowserResult value) => onResult(value);
    }
}
