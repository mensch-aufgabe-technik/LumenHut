using LumenHut.Models;
using LumenHut.Services;
using LumenHut.ViewModels;

namespace LumenHut.Tests;

/// <summary>
/// How a measurement reaches the screen. The point of these tests is that a metric which was
/// not measured never turns into a number — neither as "0" in the chart nor as a parsed string.
/// </summary>
[Collection(UiLanguageCollection.Name)]
public class MetricPresentationTests
{
    [Fact]
    public void AMetricWithoutAValueKeepsANullRawValue()
    {
        var view = BrowserResultView.From(new BrowserResult
        {
            Browser = "WebKit",
            Metrics =
            {
                new PerformanceMetric { Name = "LCP", Value = null, Unit = "ms", Note = "N/A (not supported by this engine)" }
            }
        });

        var lcp = Assert.Single(view.Metrics);
        Assert.Null(lcp.Raw);
        Assert.Equal("N/A", lcp.Value);
        Assert.True(lcp.HasNote);
    }

    [Fact]
    public void AMeasuredValueSurvivesAsANumber()
    {
        var view = BrowserResultView.From(new BrowserResult
        {
            Browser = "Chromium",
            Metrics = { new PerformanceMetric { Name = "LCP", Value = 1234.6, Unit = "ms" } }
        });

        Assert.Equal(1234.6, Assert.Single(view.Metrics).Raw);
    }

    [Theory]
    [InlineData(AppLanguage.German, 12345.0, "ms", "12.345")]
    [InlineData(AppLanguage.English, 12345.0, "ms", "12,345")]
    [InlineData(AppLanguage.German, 0.1234, "", "0,123")]
    [InlineData(AppLanguage.English, 0.1234, "", "0.123")]
    public void NumbersFollowTheSelectedLanguage(AppLanguage language, double raw, string unit, string expected)
    {
        var previous = Strings.Instance.Language;
        try
        {
            Strings.Instance.Language = language;
            var metric = new MetricView { Name = "X", Raw = raw, Unit = unit };

            Assert.Equal(expected, metric.Value);
        }
        finally
        {
            Strings.Instance.Language = previous;
        }
    }

    [Fact]
    public void SwitchingLanguageReformatsAValueAlreadyOnScreen()
    {
        var previous = Strings.Instance.Language;
        try
        {
            Strings.Instance.Language = AppLanguage.English;
            var metric = new MetricView { Name = "CLS", Raw = 0.25, Unit = "" };
            Assert.Equal("0.25", metric.Value);

            var changed = new List<string?>();
            metric.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            Strings.Instance.Language = AppLanguage.German;
            metric.RefreshFormatting();

            Assert.Contains(nameof(MetricView.Value), changed);
            Assert.Equal("0,25", metric.Value);
        }
        finally
        {
            Strings.Instance.Language = previous;
        }
    }

    [Fact]
    public void AHistoryEntryShowsItsTimestampInTheSelectedLanguage()
    {
        var previous = Strings.Instance.Language;
        try
        {
            var summary = new TestRunSummary { Timestamp = new DateTime(2026, 3, 9, 14, 5, 0, DateTimeKind.Local) };

            Strings.Instance.Language = AppLanguage.German;
            Assert.Equal("09.03.2026 14:05", summary.TimestampDisplay);

            Strings.Instance.Language = AppLanguage.English;
            summary.RefreshFormatting();
            // Not an exact match: ICU puts a narrow no-break space before AM/PM, which says
            // nothing about whether the format follows the language.
            Assert.StartsWith("3/9/2026", summary.TimestampDisplay);
            Assert.EndsWith("PM", summary.TimestampDisplay);
        }
        finally
        {
            Strings.Instance.Language = previous;
        }
    }
}

/// <summary>
/// Tests that switch the global UI language must not run next to each other.
/// </summary>
[CollectionDefinition(Name)]
public class UiLanguageCollection
{
    public const string Name = "UiLanguage";
}
