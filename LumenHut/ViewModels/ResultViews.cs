using CommunityToolkit.Mvvm.ComponentModel;
using LumenHut.Models;
using LumenHut.Services;
using System;
using System.Collections.ObjectModel;

namespace LumenHut.ViewModels;

/// <summary>Display-friendly wrapper for <see cref="BrowserResult"/> (MVVM projection).</summary>
public partial class BrowserResultView : ObservableObject
{
    [ObservableProperty]
    private string _browser = string.Empty;

    /// <summary>Engine version behind the numbers; shown next to the browser name.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEngineVersion))]
    private string? _engineVersion;

    public bool HasEngineVersion => !string.IsNullOrWhiteSpace(EngineVersion);

    [ObservableProperty]
    private bool _skipped;

    [ObservableProperty]
    private string? _skipReason;

    public ObservableCollection<MetricView> Metrics { get; } = new();

    public static BrowserResultView From(BrowserResult br)
    {
        var view = new BrowserResultView
        {
            Browser = br.Browser,
            EngineVersion = br.EngineVersion,
            Skipped = br.Skipped,
            SkipReason = br.SkipReason
        };

        foreach (var m in br.Metrics)
        {
            view.Metrics.Add(new MetricView
            {
                Name = m.Name,
                Raw = m.Value,
                Unit = m.Unit,
                Note = m.Note
            });
        }

        return view;
    }

    /// <summary>Re-renders the formatted numbers after a language switch.</summary>
    public void RefreshFormatting()
    {
        foreach (var metric in Metrics)
            metric.RefreshFormatting();
    }
}

public partial class MetricView : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// The measured value, or null when the metric was not measured. Kept as a number so that
    /// the chart and the JSON export use the measurement instead of parsing back a display string.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Value))]
    [NotifyPropertyChangedFor(nameof(Rating))]
    [NotifyPropertyChangedFor(nameof(IsGood))]
    [NotifyPropertyChangedFor(nameof(NeedsImprovement))]
    [NotifyPropertyChangedFor(nameof(IsPoor))]
    [NotifyPropertyChangedFor(nameof(HasRating))]
    private double? _raw;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    private string? _note;

    /// <summary>
    /// The value as shown on screen, formatted in the selected UI language: CLS keeps three
    /// decimals, timings are whole milliseconds with a thousands separator. A metric that was
    /// not measured reads "N/A" — never 0.
    /// </summary>
    public string Value
    {
        get
        {
            if (!Raw.HasValue)
                return Strings.Instance.MetricNotMeasured;

            var culture = Strings.Instance.Culture;
            // CLS is the only unitless metric and the only one below 1.
            return string.IsNullOrEmpty(Unit)
                ? Raw.Value.ToString("0.###", culture)
                : Math.Round(Raw.Value).ToString("N0", culture);
        }
    }

    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    /// <summary>Rating against the published threshold, or None where there is no threshold.</summary>
    public MetricRating Rating => CoreWebVitals.Rate(Name, Raw);

    public bool IsGood => Rating == MetricRating.Good;
    public bool NeedsImprovement => Rating == MetricRating.NeedsImprovement;
    public bool IsPoor => Rating == MetricRating.Poor;
    public bool HasRating => Rating != MetricRating.None;

    /// <summary>One sentence explaining the metric, shown as a tooltip.</summary>
    public string Description => Strings.Instance.MetricDescription(Name);

    public void RefreshFormatting()
    {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(Description));
    }
}

public partial class TestRunSummary : ObservableObject
{
    public int Id { get; set; }

    [ObservableProperty] private string _url = string.Empty;

    /// <summary>Local time; the database stores UTC.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimestampDisplay))]
    private DateTime _timestamp;

    [ObservableProperty] private string _browsersTested = string.Empty;

    /// <summary>Free-text note the user attached to this run.</summary>
    [ObservableProperty] private string? _notes;

    /// <summary>Date and time in the selected UI language's format.</summary>
    public string TimestampDisplay => Timestamp.ToString("g", Strings.Instance.Culture);

    public void RefreshFormatting() => OnPropertyChanged(nameof(TimestampDisplay));
}

/// <summary>
/// One metric of one engine in two runs. Every metric this application measures is better when
/// it is smaller, so a negative difference is an improvement.
/// </summary>
public sealed class ComparisonRow
{
    public required string Browser { get; init; }
    public required string Metric { get; init; }
    public required string Older { get; init; }
    public required string Newer { get; init; }
    public required string Delta { get; init; }

    /// <summary>Null when one of the two runs has no value for this metric.</summary>
    public double? Change { get; init; }

    public bool IsBetter => Change < 0;
    public bool IsWorse => Change > 0;
}
