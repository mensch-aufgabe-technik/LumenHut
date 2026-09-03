using LumenHut.Services;

namespace LumenHut.Tests;

/// <summary>
/// Rating a value against the published thresholds — including the metrics that have none,
/// where the absence of a rating is the point.
/// </summary>
public class CoreWebVitalsTests
{
    [Theory]
    [InlineData("LCP", 2500, MetricRating.Good)]
    [InlineData("LCP", 2501, MetricRating.NeedsImprovement)]
    [InlineData("LCP", 4000, MetricRating.NeedsImprovement)]
    [InlineData("LCP", 4001, MetricRating.Poor)]
    [InlineData("CLS", 0.1, MetricRating.Good)]
    [InlineData("CLS", 0.2, MetricRating.NeedsImprovement)]
    [InlineData("CLS", 0.3, MetricRating.Poor)]
    [InlineData("INP", 200, MetricRating.Good)]
    [InlineData("INP", 501, MetricRating.Poor)]
    [InlineData("FCP", 1800, MetricRating.Good)]
    [InlineData("FCP", 3001, MetricRating.Poor)]
    [InlineData("TTFB", 800, MetricRating.Good)]
    [InlineData("TTFB", 1801, MetricRating.Poor)]
    public void RatesAgainstThePublishedBoundaries(string metric, double value, MetricRating expected) =>
        Assert.Equal(expected, CoreWebVitals.Rate(metric, value));

    [Theory]
    [InlineData("LoadTime", 12000)]
    [InlineData("DOMContentLoaded", 9000)]
    [InlineData("SomethingElse", 1)]
    public void InventsNoRatingWhereThereIsNoPublishedThreshold(string metric, double value) =>
        Assert.Equal(MetricRating.None, CoreWebVitals.Rate(metric, value));

    [Theory]
    [InlineData("LCP")]
    [InlineData("CLS")]
    [InlineData("TTFB")]
    public void AMetricThatWasNotMeasuredIsNotRated(string metric) =>
        Assert.Equal(MetricRating.None, CoreWebVitals.Rate(metric, null));
}
