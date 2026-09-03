using LumenHut.Models;
using LumenHut.Services;

namespace LumenHut.Tests;

/// <summary>
/// Combining repeated passes. The interesting cases are the uneven ones: a metric that only some
/// passes could measure, and an engine that was skipped in some of them.
/// </summary>
public class MeasurementAggregatorTests
{
    private static List<BrowserResult> Pass(params double?[] lcpPerBrowser)
    {
        var browsers = new[] { "Chromium", "Firefox" };
        return lcpPerBrowser.Select((lcp, i) => new BrowserResult
        {
            Browser = browsers[i],
            EngineVersion = "149.0",
            Metrics =
            {
                new PerformanceMetric
                {
                    Name = "LCP",
                    Value = lcp,
                    Unit = "ms",
                    Note = lcp.HasValue ? null : "N/A (not supported by this engine)"
                }
            }
        }).ToList();
    }

    [Fact]
    public void ASinglePassIsReturnedUntouched()
    {
        var pass = Pass(1200);

        var combined = MeasurementAggregator.Combine(new[] { pass });

        Assert.Same(pass, combined);
        Assert.Null(combined[0].Metrics[0].Note);
    }

    [Fact]
    public void ThreePassesYieldTheMedianAndTheRange()
    {
        var combined = MeasurementAggregator.Combine(new[] { Pass(1000), Pass(3000), Pass(2000) });

        var lcp = combined.Single(r => r.Browser == "Chromium").Metrics.Single();
        Assert.Equal(2000, lcp.Value);
        Assert.Contains("Median of 3 runs", lcp.Note);
        Assert.Contains("1000-3000", lcp.Note);
    }

    [Fact]
    public void AnEvenNumberOfPassesAveragesTheTwoMiddleValues()
    {
        var combined = MeasurementAggregator.Combine(new[] { Pass(1000), Pass(2000) });

        Assert.Equal(1500, combined.Single(r => r.Browser == "Chromium").Metrics.Single().Value);
    }

    [Fact]
    public void AMetricOnlySomePassesCouldMeasureSaysSo()
    {
        var combined = MeasurementAggregator.Combine(new[] { Pass(1000), Pass((double?)null), Pass(1400) });

        var lcp = combined.Single(r => r.Browser == "Chromium").Metrics.Single();
        Assert.Equal(1200, lcp.Value);
        Assert.Contains("2 measured runs out of 3", lcp.Note);
    }

    [Fact]
    public void AReasonFromAPassThatFailedDoesNotTravelToAMeasuredMedian()
    {
        // The first pass measures nothing, later passes do. Carrying the first pass's reason
        // would put "N/A (not supported by this engine)" next to a number.
        var combined = MeasurementAggregator.Combine(
            new[] { Pass((double?)null), Pass(1000), Pass(1400) });

        var lcp = combined.Single(r => r.Browser == "Chromium").Metrics.Single();
        Assert.Equal(1200, lcp.Value);
        Assert.DoesNotContain("N/A", lcp.Note);
        Assert.Contains("2 measured runs out of 3", lcp.Note);
    }

    [Fact]
    public void ASingleMeasuredPassIsCountedInTheSingular()
    {
        var combined = MeasurementAggregator.Combine(
            new[] { Pass((double?)null), Pass((double?)null), Pass(1000) });

        var lcp = combined.Single(r => r.Browser == "Chromium").Metrics.Single();
        Assert.Equal("Median of 1 measured run out of 3", lcp.Note);
    }

    [Fact]
    public void AMetricNoPassCouldMeasureKeepsItsOriginalReason()
    {
        var combined = MeasurementAggregator.Combine(
            new[] { Pass((double?)null), Pass((double?)null) });

        var lcp = combined.Single(r => r.Browser == "Chromium").Metrics.Single();
        Assert.Null(lcp.Value);
        Assert.Equal("N/A (not supported by this engine)", lcp.Note);
    }

    [Fact]
    public void IdenticalValuesReportNoRange()
    {
        var combined = MeasurementAggregator.Combine(new[] { Pass(1500), Pass(1500) });

        var note = combined.Single(r => r.Browser == "Chromium").Metrics.Single().Note;
        Assert.Equal("Median of 2 runs", note);
    }

    [Fact]
    public void AnEngineSkippedEverywhereKeepsItsReason()
    {
        var skipped = new List<BrowserResult>
        {
            new() { Browser = "WebKit", Skipped = true, SkipReason = "engine not available" }
        };

        var combined = MeasurementAggregator.Combine(new[] { skipped, skipped });

        Assert.True(combined.Single().Skipped);
        Assert.Equal("engine not available", combined.Single().SkipReason);
    }

    [Fact]
    public void AnEngineThatFailedOnceStillReportsItsSuccessfulPasses()
    {
        var failed = new List<BrowserResult>
        {
            new() { Browser = "Chromium", Skipped = true, SkipReason = "Navigation failed: HTTP 503" }
        };
        var succeeded = Pass(1800).Where(r => r.Browser == "Chromium").ToList();

        var combined = MeasurementAggregator.Combine(new[] { succeeded, failed, succeeded });

        var chromium = combined.Single();
        Assert.False(chromium.Skipped);
        Assert.Equal(1800, chromium.Metrics.Single().Value);
        Assert.Contains("2 measured runs out of 3", chromium.Metrics.Single().Note);
    }

    [Fact]
    public void NoPassesYieldNoResults() =>
        Assert.Empty(MeasurementAggregator.Combine(new List<List<BrowserResult>>()));

    [Fact]
    public void TheEngineVersionSurvivesTheMerge()
    {
        var combined = MeasurementAggregator.Combine(new[] { Pass(1000), Pass(1100) });

        Assert.Equal("149.0", combined.First().EngineVersion);
    }
}
