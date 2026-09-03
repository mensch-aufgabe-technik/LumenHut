using LumenHut.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LumenHut.Services;

/// <summary>
/// Combines several passes over the same URL into one result per engine, using the median of each
/// metric.
/// </summary>
/// <remarks>
/// A single cold load varies enough that two runs of the same page can differ by a third. The
/// median of three or five passes is the cheapest honest answer to that. The spread is kept in
/// the note rather than averaged away, so a wildly unstable page still looks unstable.
/// </remarks>
public static class MeasurementAggregator
{
    /// <summary>
    /// Merges the passes in order. A single pass is returned unchanged — no note, no aggregation,
    /// so the default behaviour of the application does not change.
    /// </summary>
    public static List<BrowserResult> Combine(IReadOnlyList<List<BrowserResult>> passes)
    {
        if (passes.Count == 0)
            return new List<BrowserResult>();

        if (passes.Count == 1)
            return passes[0];

        var merged = new List<BrowserResult>();

        // Engine order follows the first pass; later passes only contribute values.
        foreach (var browser in passes[0].Select(r => r.Browser))
        {
            var attempts = passes
                .Select(pass => pass.FirstOrDefault(r => r.Browser == browser))
                .Where(r => r != null)
                .Select(r => r!)
                .ToList();

            var usable = attempts.Where(r => !r.Skipped).ToList();

            if (usable.Count == 0)
            {
                // Skipped everywhere: keep the first reason rather than inventing a summary.
                merged.Add(attempts[0]);
                continue;
            }

            merged.Add(new BrowserResult
            {
                Browser = browser,
                EngineVersion = usable.Select(r => r.EngineVersion).FirstOrDefault(v => v != null),
                Skipped = false,
                Metrics = CombineMetrics(usable, passes.Count)
            });
        }

        return merged;
    }

    private static List<PerformanceMetric> CombineMetrics(List<BrowserResult> attempts, int passCount)
    {
        var combined = new List<PerformanceMetric>();

        foreach (var name in attempts[0].Metrics.Select(m => m.Name))
        {
            var samples = attempts
                .SelectMany(a => a.Metrics.Where(m => m.Name == name))
                .ToList();

            var values = samples
                .Where(m => m.Value.HasValue)
                .Select(m => m.Value!.Value)
                .OrderBy(v => v)
                .ToList();

            var template = samples[0];

            // The note has to describe the value that is shown. samples[0] is the first pass, and
            // if that pass could not measure the metric its "not measured" reason would end up
            // next to a median that later passes did produce.
            var noteSource = samples.FirstOrDefault(m => m.Value.HasValue) ?? template;

            if (values.Count == 0)
            {
                // Not measurable in any pass: the original reason is more useful than a summary.
                combined.Add(new PerformanceMetric
                {
                    Name = name,
                    Value = null,
                    Unit = template.Unit,
                    Note = template.Note
                });
                continue;
            }

            combined.Add(new PerformanceMetric
            {
                Name = name,
                Value = Median(values),
                Unit = template.Unit,
                Note = Combine(noteSource.Note, Spread(values, passCount))
            });
        }

        return combined;
    }

    private static double Median(List<double> ordered) =>
        ordered.Count % 2 == 1
            ? ordered[ordered.Count / 2]
            : (ordered[ordered.Count / 2 - 1] + ordered[ordered.Count / 2]) / 2;

    /// <summary>English on purpose: the note is persisted verbatim, like every other note.</summary>
    private static string Spread(List<double> ordered, int passCount)
    {
        var min = ordered[0].ToString("0.###", CultureInfo.InvariantCulture);
        var max = ordered[^1].ToString("0.###", CultureInfo.InvariantCulture);

        var counted = ordered.Count == passCount
            ? $"Median of {passCount} runs"
            : $"Median of {ordered.Count} measured run{(ordered.Count == 1 ? "" : "s")} out of {passCount}";

        return ordered[0] == ordered[^1] ? counted : $"{counted} (range {min}-{max})";
    }

    private static string Combine(string? existing, string added) =>
        string.IsNullOrWhiteSpace(existing) ? added : $"{existing} | {added}";
}
