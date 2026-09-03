using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumenHut.Data;
using LumenHut.Models;
using LumenHut.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LumenHut.ViewModels;

/// <summary>Measurement page: target URL, engine selection, current results and export.</summary>
public partial class MeasureViewModel : PageViewModelBase, IAsyncDisposable
{
    private static readonly ILogger Log = AppLog.For<MeasureViewModel>();

    private readonly PlaywrightPerfService _perfService = new();
    private readonly Func<ProxyConfig?> _proxyProvider;
    private readonly Func<bool> _storeFullUrlProvider;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunTestCommand))]
    private string _url = "https://example.com";

    [ObservableProperty]
    private bool _useChromium = true;

    [ObservableProperty]
    private bool _useFirefox = true;

    [ObservableProperty]
    private bool _useWebKit = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunTestCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelTestCommand))]
    private bool _isRunning;

    /// <summary>Cancels the run in progress; null while nothing is running.</summary>
    private CancellationTokenSource? _cancellation;

    [ObservableProperty]
    private IReadOnlyList<RepeatOption> _repeatOptions = BuildRepeatOptions();

    [ObservableProperty]
    private RepeatOption _selectedRepeat = BuildRepeatOptions()[0];

    private static IReadOnlyList<RepeatOption> BuildRepeatOptions()
    {
        var strings = Strings.Instance;
        return new[]
        {
            new RepeatOption(1, strings.RepeatSingle),
            new RepeatOption(3, string.Format(strings.RepeatManyFormat, 3)),
            new RepeatOption(5, string.Format(strings.RepeatManyFormat, 5))
        };
    }

    public ObservableCollection<BrowserResultView> CurrentResults { get; } = new();

    public bool HasResults => CurrentResults.Count > 0;

    /// <summary>UTC timestamp of the run on screen, so an export reports when it was measured
    /// rather than when it was exported.</summary>
    private DateTime? _measuredAtUtc;

    /// <summary>Whether the last run went through a proxy; stored with the run, never the address.</summary>
    private bool _proxyUsedInLastRun;

    /// <summary>
    /// Names the engines that ran but produced no LCP value, shown below the chart. Without this
    /// they would simply be absent from the comparison with no explanation.
    /// </summary>
    public string? MissingLcpNote
    {
        get
        {
            var missing = CurrentResults
                .Where(r => !r.Skipped)
                .Where(r => r.Metrics.FirstOrDefault(m => m.Name == "LCP")?.Raw is null)
                .Select(r => r.Browser)
                .ToList();

            return missing.Count == 0 ? null : string.Format(S.ChartMissingFormat, string.Join(", ", missing));
        }
    }

    public bool HasMissingLcp => MissingLcpNote != null;

    /// <param name="proxyProvider">Reads the proxy currently entered in the settings page,
    /// so a change takes effect on the next run without an app restart.</param>
    /// <param name="storeFullUrlProvider">Whether the measured URL is persisted with its query
    /// string; off by default because query strings routinely carry tokens.</param>
    public MeasureViewModel(Func<ProxyConfig?> proxyProvider, Func<bool> storeFullUrlProvider)
    {
        _proxyProvider = proxyProvider;
        _storeFullUrlProvider = storeFullUrlProvider;
        CurrentResults.CollectionChanged += OnResultsChanged;
        SetStatus(s => s.StatusReady);
    }

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(MissingLcpNote));
        OnPropertyChanged(nameof(HasMissingLcp));
    }

    protected override void OnLanguageChanged()
    {
        foreach (var result in CurrentResults)
            result.RefreshFormatting();

        var repeats = SelectedRepeat.Count;
        RepeatOptions = BuildRepeatOptions();
        SelectedRepeat = RepeatOptions.First(o => o.Count == repeats);

        OnPropertyChanged(nameof(MissingLcpNote));
        OnPropertyChanged(nameof(HasMissingLcp));
    }

    partial void OnUrlChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            SetStatus(s => s.StatusUrlRequired);
    }

    [RelayCommand(CanExecute = nameof(CanRunTest))]
    private async Task RunTestAsync()
    {
        var selected = GetSelectedBrowsers();
        if (selected.Count == 0)
        {
            SetStatus(s => s.StatusEngineRequired);
            return;
        }

        ProxyConfig? proxy;
        try
        {
            proxy = _proxyProvider();
        }
        catch (FormatException)
        {
            // The exception message is not shown: the entered value can contain a password.
            SetStatus(s => s.StatusProxyInvalid);
            return;
        }

        _proxyUsedInLastRun = proxy != null;

        IsRunning = true;
        CurrentResults.Clear();

        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;

        var total = selected.Count;
        var repeats = SelectedRepeat.Count;

        // Completed passes, plus the one in flight: a cancelled run keeps what it already has.
        var passes = new List<List<BrowserResult>>();
        var currentPass = new List<BrowserResult>();

        try
        {
            var installError = await _perfService.EnsureBrowsersInstalledAsync(
                selected, SetStatusRaw, proxy, token);
            if (installError != null)
            {
                SetStatusRaw(installError);
                return;
            }

            var target = Url;
            var engines = string.Join(", ", selected);
            SetStatus(s => string.Format(s.StatusRunningFormat, target, engines));

            // Reduced URL on purpose: a log line must not carry a session token.
            Log.LogInformation("Run started for {Url} on {Engines}, {Repeats} pass(es), proxy {ProxyUsed}",
                UrlPrivacy.ForStorage(target, keepQuery: false), engines, repeats, _proxyUsedInLastRun);

            for (var pass = 1; pass <= repeats; pass++)
            {
                var passNumber = pass;
                currentPass = new List<BrowserResult>();

                var progress = new EngineProgress(result =>
                {
                    currentPass.Add(result);

                    // Cards are shown live for the first pass; the merged values replace them
                    // once every pass is in.
                    if (passNumber == 1)
                        ShowResult(result);

                    var done = currentPass.Count;
                    var engine = result.Browser;
                    SetStatus(s => repeats == 1
                        ? string.Format(s.StatusEngineDoneFormat, done, total, engine)
                        : string.Format(s.StatusPassEngineFormat, passNumber, repeats, done, total, engine));
                });

                passes.Add(await _perfService.RunTestsAsync(Url, selected, proxy, progress, token));
            }

            var merged = await FinishAsync(passes, repeats);

            SetStatus(s => s.StatusCompleted);
            Log.LogInformation("Run finished with {Count} engine results, {Skipped} skipped",
                merged.Count, merged.Count(r => r.Skipped));
            ResultsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            if (currentPass.Count > 0)
                passes.Add(currentPass);

            if (passes.Count == 0)
            {
                SetStatus(s => s.StatusCancelled);
                return;
            }

            var merged = await FinishAsync(passes, repeats);

            Log.LogInformation("Run cancelled after {Passes} pass(es)", passes.Count);
            var done = merged.Count;
            SetStatus(s => string.Format(s.StatusCancelledPartialFormat, done, total));
            ResultsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Run failed");
            SetStatus(s => string.Format(s.StatusErrorFormat, ex.Message));
        }
        finally
        {
            IsRunning = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    /// <summary>
    /// Merges the passes, puts the result on screen and stores it. With more than one pass the
    /// live cards from the first pass are replaced by the merged values.
    /// </summary>
    private async Task<List<BrowserResult>> FinishAsync(List<List<BrowserResult>> passes, int repeats)
    {
        var merged = MeasurementAggregator.Combine(passes);

        if (repeats > 1)
        {
            CurrentResults.Clear();
            foreach (var result in merged)
                CurrentResults.Add(BrowserResultView.From(result));
        }

        _measuredAtUtc = DateTime.UtcNow;
        await PersistAsync(merged);
        return merged;
    }

    /// <summary>Persists what was measured, with the URL reduced first — see UrlPrivacy.</summary>
    private async Task PersistAsync(List<BrowserResult> results)
    {
        await RunStore.SaveAsync(
            UrlPrivacy.ForStorage(Url, _storeFullUrlProvider()),
            _measuredAtUtc!.Value,
            RunContext.Current(_proxyUsedInLastRun),
            results);
    }

    private void ShowResult(BrowserResult result)
    {
        if (Dispatcher.UIThread.CheckAccess())
            CurrentResults.Add(BrowserResultView.From(result));
        else
            Dispatcher.UIThread.Post(() => CurrentResults.Add(BrowserResultView.From(result)));
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void CancelTest() => _cancellation?.Cancel();

    /// <summary>Reports synchronously, unlike <see cref="Progress{T}"/>, so a cancelled run knows
    /// exactly which engines had finished at that point.</summary>
    private sealed class EngineProgress(Action<BrowserResult> onResult) : IProgress<BrowserResult>
    {
        public void Report(BrowserResult value) => onResult(value);
    }

    /// <summary>Raised after a run was written to SQLite so the history page can refresh.</summary>
    public event EventHandler? ResultsSaved;

    private bool CanRunTest() => !IsRunning && !string.IsNullOrWhiteSpace(Url);

    private List<string> GetSelectedBrowsers()
    {
        var list = new List<string>();
        if (UseChromium) list.Add("Chromium");
        if (UseFirefox) list.Add("Firefox");
        if (UseWebKit) list.Add("WebKit");
        return list;
    }

    /// <summary>Loads a stored run into this page; called when the history page opens a run.</summary>
    public async Task LoadRunAsync(int runId)
    {
        CurrentResults.Clear();

        try
        {
            var run = await RunStore.LoadAsync(runId);

            if (run == null) return;

            Url = run.Url; // reflect in UI
            // SQLite hands back Unspecified; the column holds UTC.
            _measuredAtUtc = DateTime.SpecifyKind(run.Timestamp, DateTimeKind.Utc);

            foreach (var br in run.BrowserResults)
                CurrentResults.Add(BrowserResultView.From(br));

            SetStatus(s => string.Format(s.StatusLoadedRunFormat, runId));
        }
        catch (Exception ex)
        {
            SetStatus(s => string.Format(s.StatusErrorFormat, ex.Message));
        }
    }

    [RelayCommand]
    private async Task ExportJsonAsync()
    {
        if (!HasResults)
        {
            SetStatus(s => s.ExportNoResults);
            return;
        }

        await SaveExportAsync(BuildJson(), SuggestedName("json"), "JSON", "*.json", "application/json");
    }

    [RelayCommand]
    private async Task ExportMarkdownAsync()
    {
        if (!HasResults)
        {
            SetStatus(s => s.ExportNoResults);
            return;
        }

        await SaveExportAsync(BuildMarkdown(), SuggestedName("md"), "Markdown", "*.md", "text/markdown");
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (!HasResults)
        {
            SetStatus(s => s.ExportNoResults);
            return;
        }

        await SaveExportAsync(BuildCsv(), SuggestedName("csv"), "CSV", "*.csv", "text/csv");
    }

    /// <summary>For pasting a run into a chat or a ticket, which is where most reports go.</summary>
    [RelayCommand]
    private async Task CopyToClipboardAsync()
    {
        if (!HasResults)
        {
            SetStatus(s => s.ExportNoResults);
            return;
        }

        var clipboard = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?
            .MainWindow?.Clipboard;

        if (clipboard == null)
        {
            SetStatus(s => s.ExportClipboardUnavailable);
            return;
        }

        try
        {
            await clipboard.SetTextAsync(BuildMarkdown());
            SetStatus(s => s.ExportCopied);
        }
        catch (Exception ex)
        {
            SetStatus(s => string.Format(s.ExportFailedFormat, ex.Message));
        }
    }

    private string BuildJson() => System.Text.Json.JsonSerializer.Serialize(
        new
        {
            Url,
            MeasuredAt = MeasuredAtOffset(),
            ExportedAt = DateTimeOffset.Now,
            Tool = AppInfo.NameAndVersion,
            Viewport = AppInfo.Viewport,
            Results = CurrentResults.Select(r => new
            {
                r.Browser,
                r.Skipped,
                r.SkipReason,
                // Raw numbers, null for "not measured": the export is meant to be processed,
                // not parsed back from a formatted string.
                Metrics = r.Metrics.Select(m => new { m.Name, Value = m.Raw, m.Unit, m.Note })
            })
        },
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    private string BuildMarkdown()
    {
        var md = new StringBuilder()
            .AppendLine($"# {S.ExportReportHeading}")
            .AppendLine()
            .AppendLine($"**{S.ExportReportUrl}:** {Url}")
            .AppendLine($"**{S.ExportReportTime}:** {MeasuredAtOffset():yyyy-MM-dd HH:mm zzz}")
            .AppendLine($"**{S.ExportReportTool}:** {AppInfo.NameAndVersion}")
            .AppendLine($"**{S.ExportReportViewport}:** {AppInfo.Viewport}")
            .AppendLine();

        foreach (var r in CurrentResults)
        {
            md.AppendLine($"## {r.Browser}");
            if (r.Skipped)
            {
                md.AppendLine($"**{S.ExportReportSkipped}:** {r.SkipReason}").AppendLine();
                continue;
            }
            md.AppendLine($"| {S.ExportTableMetric} | {S.ExportTableValue} | {S.ExportTableUnit} | {S.ExportTableNote} |");
            md.AppendLine("|--------|-------|------|------|");
            foreach (var m in r.Metrics)
                md.AppendLine($"| {m.Name} | {m.Value} | {m.Unit} | {m.Note ?? "-"} |");

            md.AppendLine();
        }

        return md.ToString();
    }

    /// <summary>
    /// One row per metric, with the run's context repeated so rows stay meaningful when several
    /// exports are appended. Separator and decimal mark follow the selected UI language, because
    /// the file is opened in that language's spreadsheet.
    /// </summary>
    private string BuildCsv()
    {
        var culture = S.Culture;
        var separator = culture.TextInfo.ListSeparator;
        var measuredAt = MeasuredAtOffset().ToString("yyyy-MM-dd HH:mm:ss zzz", culture);

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(separator, new[]
        {
            S.ExportReportUrl, S.ExportReportTime, "Browser", S.ExportTableMetric,
            S.ExportTableValue, S.ExportTableUnit, S.ExportTableNote
        }.Select(Quote)));

        foreach (var r in CurrentResults)
        {
            if (r.Skipped)
            {
                csv.AppendLine(string.Join(separator, new[]
                {
                    Url, measuredAt, r.Browser, S.ExportReportSkipped, string.Empty, string.Empty,
                    r.SkipReason ?? string.Empty
                }.Select(Quote)));
                continue;
            }

            foreach (var m in r.Metrics)
            {
                csv.AppendLine(string.Join(separator, new[]
                {
                    Url, measuredAt, r.Browser, m.Name,
                    m.Raw?.ToString(culture) ?? string.Empty,
                    m.Unit, m.Note ?? string.Empty
                }.Select(Quote)));
            }
        }

        return csv.ToString();

        string Quote(string field) =>
            field.Contains(separator, StringComparison.Ordinal) || field.Contains('"') || field.Contains('\n')
                ? '"' + field.Replace("\"", "\"\"") + '"'
                : field;
    }

    /// <summary>Host and measurement time in the file name, so a second export does not offer to
    /// overwrite the first.</summary>
    private string SuggestedName(string extension)
    {
        var host = "report";
        if (Uri.TryCreate(PlaywrightPerfService.NormalizeUrl(Url), UriKind.Absolute, out var uri)
            && !string.IsNullOrEmpty(uri.Host))
        {
            host = uri.Host;
        }

        return $"lumenhut_{host}_{MeasuredAtOffset():yyyyMMdd-HHmm}.{extension}";
    }

    private async Task SaveExportAsync(string content, string suggestedName, string typeName, string pattern, string mimeType)
    {
        try
        {
            var storage = GetStorageProvider();
            if (storage == null)
            {
                SetStatus(s => s.ExportNoWindow);
                return;
            }

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = string.Format(S.ExportDialogTitleFormat, typeName),
                SuggestedFileName = suggestedName,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(typeName) { Patterns = new[] { pattern }, MimeTypes = new[] { mimeType } }
                }
            });

            if (file == null)
            {
                SetStatus(s => s.ExportCancelled);
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new System.IO.StreamWriter(stream, Encoding.UTF8);
            await writer.WriteAsync(content);

            var name = file.Name;
            SetStatus(s => string.Format(s.ExportDoneFormat, typeName, name));
        }
        catch (Exception ex)
        {
            SetStatus(s => string.Format(s.ExportFailedFormat, ex.Message));
        }
    }

    /// <summary>Measurement time as local time with offset; falls back to now for a run whose
    /// timestamp is unknown.</summary>
    private DateTimeOffset MeasuredAtOffset() =>
        _measuredAtUtc.HasValue
            ? new DateTimeOffset(_measuredAtUtc.Value, TimeSpan.Zero).ToLocalTime()
            : DateTimeOffset.Now;

    private static IStorageProvider? GetStorageProvider() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?
        .MainWindow?.StorageProvider;

    public override void Dispose()
    {
        CurrentResults.CollectionChanged -= OnResultsChanged;
        base.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation?.Cancel();
        Dispose();
        await _perfService.DisposeAsync();
    }
}

/// <summary>How many passes a measurement makes; the label follows the UI language.</summary>
public sealed record RepeatOption(int Count, string Display)
{
    public override string ToString() => Display;
}
