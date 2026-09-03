namespace LumenHut.Services;

public enum MetricRating
{
    /// <summary>No published threshold for this metric — no rating is invented for it.</summary>
    None,
    Good,
    NeedsImprovement,
    Poor
}

/// <summary>
/// Published rating thresholds for the metrics that have them.
/// Source: web.dev/articles/vitals and the per-metric articles (LCP, CLS, INP, FCP, TTFB),
/// Google's "good / needs improvement / poor" boundaries at the 75th percentile.
/// </summary>
/// <remarks>
/// LoadTime and DOMContentLoaded deliberately have no thresholds: there are no published ones,
/// and a made-up boundary would be exactly the kind of invented number this application avoids.
/// A single laboratory run is also not a field percentile — the rating is an orientation, not a
/// verdict, which is what the legend in the measurement page says.
/// </remarks>
public static class CoreWebVitals
{
    public static MetricRating Rate(string metricName, double? value)
    {
        if (!value.HasValue)
            return MetricRating.None;

        var (good, poor) = metricName switch
        {
            "LCP" => (2500d, 4000d),
            "CLS" => (0.1d, 0.25d),
            "INP" => (200d, 500d),
            "FCP" => (1800d, 3000d),
            "TTFB" => (800d, 1800d),
            _ => (0d, 0d)
        };

        if (poor == 0d)
            return MetricRating.None;

        if (value.Value <= good)
            return MetricRating.Good;

        return value.Value <= poor ? MetricRating.NeedsImprovement : MetricRating.Poor;
    }
}
