using System.Globalization;
using LumenHut.Models;
using LumenHut.Services;

namespace LumenHut.Tests;

/// <summary>
/// Comparing two runs. Every metric is better when smaller, so a negative difference must always
/// read as an improvement — and a metric only one of the runs measured must not produce one.
/// </summary>
public class RunComparisonTests
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private static TestRun Run(string url, params (string Metric, double? Value, string Unit)[] metrics)
    {
        var result = new BrowserResult { Browser = "Chromium" };
        foreach (var (metric, value, unit) in metrics)
            result.Metrics.Add(new PerformanceMetric { Name = metric, Value = value, Unit = unit });

        return new TestRun { Url = url, BrowserResults = { result } };
    }

    [Fact]
    public void AFasterRunReadsAsAnImprovement()
    {
        var older = Run("https://example.com", ("LCP", 2000, "ms"));
        var newer = Run("https://example.com", ("LCP", 1500, "ms"));

        var row = Assert.Single(RunComparison.Build(older, newer, German));

        Assert.Equal("LCP", row.Metric);
        Assert.Equal(-500, row.Change);
        Assert.True(row.IsBetter);
        Assert.False(row.IsWorse);
        Assert.Contains("-500 ms", row.Delta);
        Assert.Contains("-25", row.Delta);
    }

    [Fact]
    public void ASlowerRunReadsAsARegression()
    {
        var row = Assert.Single(RunComparison.Build(
            Run("https://example.com", ("TTFB", 400, "ms")),
            Run("https://example.com", ("TTFB", 600, "ms")),
            German));

        Assert.True(row.IsWorse);
        Assert.Contains("+200 ms", row.Delta);
    }

    [Fact]
    public void NumbersFollowTheGivenCulture()
    {
        var german = Assert.Single(RunComparison.Build(
            Run("https://example.com", ("LoadTime", 4000, "ms")),
            Run("https://example.com", ("LoadTime", 5500, "ms")),
            German));

        Assert.Equal("4.000 ms", german.Older);
        Assert.Contains("+1.500 ms", german.Delta);

        var english = Assert.Single(RunComparison.Build(
            Run("https://example.com", ("LoadTime", 4000, "ms")),
            Run("https://example.com", ("LoadTime", 5500, "ms")),
            CultureInfo.GetCultureInfo("en-US")));

        Assert.Equal("4,000 ms", english.Older);
    }

    [Fact]
    public void AMissingValueProducesNoDifference()
    {
        var row = Assert.Single(RunComparison.Build(
            Run("https://example.com", ("CLS", null, "")),
            Run("https://example.com", ("CLS", 0.2, "")),
            German));

        Assert.Null(row.Change);
        Assert.Equal("–", row.Delta);
        Assert.False(row.IsBetter);
        Assert.False(row.IsWorse);
        Assert.Equal("N/A", row.Older);
        Assert.Equal("0,2", row.Newer);
    }

    [Fact]
    public void AZeroBaselineReportsNoPercentage()
    {
        var row = Assert.Single(RunComparison.Build(
            Run("https://example.com", ("CLS", 0, "")),
            Run("https://example.com", ("CLS", 0.15, "")),
            German));

        Assert.DoesNotContain("%", row.Delta);
    }

    [Fact]
    public void OnlyMetricsPresentInBothRunsAreCompared()
    {
        var older = Run("https://example.com", ("LCP", 2000, "ms"), ("INP", 90, "ms"));
        var newer = Run("https://example.com", ("LCP", 1800, "ms"));

        var rows = RunComparison.Build(older, newer, German);

        Assert.Equal("LCP", Assert.Single(rows).Metric);
    }

    [Fact]
    public void AnEngineMissingFromOneRunIsSkipped()
    {
        var older = Run("https://example.com", ("LCP", 2000, "ms"));
        older.BrowserResults.Add(new BrowserResult
        {
            Browser = "WebKit",
            Metrics = { new PerformanceMetric { Name = "LCP", Value = 3000, Unit = "ms" } }
        });

        var rows = RunComparison.Build(older, Run("https://example.com", ("LCP", 1900, "ms")), German);

        Assert.Equal("Chromium", Assert.Single(rows).Browser);
    }
}
