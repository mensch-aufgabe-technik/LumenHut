using LumenHut.Models;
using LumenHut.ViewModels;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LumenHut.Services;

/// <summary>
/// Puts two runs side by side. Answers the question a history exists for: did the page get faster
/// than last time? Every metric here is better when smaller, so the difference carries its own
/// meaning and no metric-specific direction is needed.
/// </summary>
public static class RunComparison
{
    public static List<ComparisonRow> Build(TestRun older, TestRun newer, CultureInfo culture)
    {
        var rows = new List<ComparisonRow>();

        foreach (var olderResult in older.BrowserResults)
        {
            var newerResult = newer.BrowserResults.FirstOrDefault(b => b.Browser == olderResult.Browser);
            if (newerResult == null)
                continue;

            foreach (var olderMetric in olderResult.Metrics)
            {
                var newerMetric = newerResult.Metrics.FirstOrDefault(m => m.Name == olderMetric.Name);
                if (newerMetric == null)
                    continue;

                rows.Add(BuildRow(olderResult.Browser, olderMetric, newerMetric, culture));
            }
        }

        return rows;
    }

    private static ComparisonRow BuildRow(string browser, PerformanceMetric older,
        PerformanceMetric newer, CultureInfo culture)
    {
        double? change = older.Value.HasValue && newer.Value.HasValue
            ? newer.Value.Value - older.Value.Value
            : null;

        return new ComparisonRow
        {
            Browser = browser,
            Metric = older.Name,
            Older = Format(older, culture),
            Newer = Format(newer, culture),
            Change = change,
            Delta = change.HasValue ? FormatDelta(change.Value, older, culture) : "–"
        };
    }

    private static string Format(PerformanceMetric metric, CultureInfo culture)
    {
        if (!metric.Value.HasValue)
            return Strings.Instance.MetricNotMeasured;

        return string.IsNullOrEmpty(metric.Unit)
            ? metric.Value.Value.ToString("0.###", culture)
            : $"{System.Math.Round(metric.Value.Value).ToString("N0", culture)} {metric.Unit}";
    }

    /// <summary>Absolute change with a sign, plus the relative change where it is meaningful.</summary>
    private static string FormatDelta(double change, PerformanceMetric older, CultureInfo culture)
    {
        var unitless = string.IsNullOrEmpty(older.Unit);
        var absolute = unitless
            ? change.ToString("+0.###;-0.###;0", culture)
            : $"{System.Math.Round(change).ToString("+#,##0;-#,##0;0", culture)} {older.Unit}";

        // A percentage off a zero baseline says nothing.
        if (older.Value is null or 0)
            return absolute;

        var percent = change / older.Value.Value * 100;
        return $"{absolute} ({percent.ToString("+0.#;-0.#;0", culture)} %)";
    }
}
