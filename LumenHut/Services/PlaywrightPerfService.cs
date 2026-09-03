using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LumenHut.Models;

namespace LumenHut.Services;

/// <summary>
/// Playwright wrapper for cross-browser performance measurement.
/// Only extracts metrics explicitly supported via Playwright + browser Performance APIs.
/// Missing or engine-unsupported values are marked as N/A. No invented metrics.
/// </summary>
public class PlaywrightPerfService : IAsyncDisposable
{
    private static readonly ILogger Log = AppLog.For<PlaywrightPerfService>();

    private IPlaywright? _playwright;

    private static readonly string[] SupportedBrowsers = { "Chromium", "Firefox", "WebKit" };

    /// <summary>
    /// Observers must start before navigation to catch late-finalized LCP candidates and all
    /// layout shifts. Support is detected via PerformanceObserver.supportedEntryTypes because
    /// observe() on an unsupported type is a silent no-op per spec, not an exception — an engine
    /// without an entry type must report N/A instead of a fake 0.
    /// </summary>
    private const string InitScript = @"
        window.__lh = { lcp: null, lcpSupported: false, cls: 0, clsSupported: false, inp: null, inpSupported: false };
        try {
            const supported = PerformanceObserver.supportedEntryTypes || [];
            if (supported.includes('largest-contentful-paint')) {
                window.__lh.lcpSupported = true;
                new PerformanceObserver(list => {
                    for (const e of list.getEntries()) window.__lh.lcp = e.startTime;
                }).observe({ type: 'largest-contentful-paint', buffered: true });
            }
            if (supported.includes('layout-shift')) {
                window.__lh.clsSupported = true;
                new PerformanceObserver(list => {
                    for (const e of list.getEntries()) {
                        if (!e.hadRecentInput) window.__lh.cls += (e.value || 0);
                    }
                }).observe({ type: 'layout-shift', buffered: true });
            }
            if (supported.includes('event')) {
                window.__lh.inpSupported = true;
                new PerformanceObserver(list => {
                    for (const e of list.getEntries()) {
                        // INP proxy: worst interaction latency, start until next paint (= entry.duration)
                        if (e.interactionId) window.__lh.inp = Math.max(window.__lh.inp || 0, e.duration || 0);
                    }
                }).observe({ type: 'event', buffered: true, durationThreshold: 16 });
            }
        } catch (e) {}
    ";

    private const string ExtractScript = @"() => {
        const nav = performance.getEntriesByType('navigation')[0];
        const fcpEntry = performance.getEntriesByType('paint').find(p => p.name === 'first-contentful-paint');
        const s = window.__lh || {};
        return {
            loadTime: nav && nav.loadEventEnd > 0 ? Math.round(nav.loadEventEnd - nav.startTime) : null,
            domContentLoaded: nav && nav.domContentLoadedEventEnd > 0 ? Math.round(nav.domContentLoadedEventEnd - nav.startTime) : null,
            ttfb: nav && nav.responseStart > 0 ? Math.round(nav.responseStart - nav.startTime) : null,
            fcp: fcpEntry ? Math.round(fcpEntry.startTime) : null,
            lcp: s.lcp != null ? Math.round(s.lcp) : null,
            lcpSupported: !!s.lcpSupported,
            cls: s.clsSupported ? parseFloat(s.cls.toFixed(4)) : null,
            clsSupported: !!s.clsSupported,
            inp: s.inp != null ? Math.round(s.inp) : null,
            inpSupported: !!s.inpSupported
        };
    }";

    private sealed class RawMetrics
    {
        [JsonPropertyName("loadTime")] public double? LoadTime { get; set; }
        [JsonPropertyName("domContentLoaded")] public double? DomContentLoaded { get; set; }
        [JsonPropertyName("ttfb")] public double? Ttfb { get; set; }
        [JsonPropertyName("fcp")] public double? Fcp { get; set; }
        [JsonPropertyName("lcp")] public double? Lcp { get; set; }
        [JsonPropertyName("lcpSupported")] public bool LcpSupported { get; set; }
        [JsonPropertyName("cls")] public double? Cls { get; set; }
        [JsonPropertyName("clsSupported")] public bool ClsSupported { get; set; }
        [JsonPropertyName("inp")] public double? Inp { get; set; }
        [JsonPropertyName("inpSupported")] public bool InpSupported { get; set; }
    }

    public async Task InitializeAsync()
    {
        _playwright ??= await Playwright.CreateAsync();
    }

    /// <summary>
    /// Ensures the requested browser engines are installed, downloading missing ones on first run.
    /// Returns null on success, otherwise a user-facing error message.
    /// </summary>
    /// <summary>
    /// HTTP(S)_PROXY is process-wide state: two installs running at once would overwrite each
    /// other's saved value and leave the wrong proxy behind. The UI prevents that, tests do not.
    /// </summary>
    private static readonly SemaphoreSlim InstallGate = new(1, 1);

    public async Task<string?> EnsureBrowsersInstalledAsync(IEnumerable<string> browsers,
        Action<string>? progress = null, ProxyConfig? proxy = null,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync();

        var missing = browsers
            .Where(b => SupportedBrowsers.Contains(b, StringComparer.OrdinalIgnoreCase))
            .Where(b => !File.Exists(GetBrowserType(b).ExecutablePath))
            .Select(b => b.ToLowerInvariant())
            .ToList();

        if (missing.Count == 0)
            return null;

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Invoke(string.Format(Strings.Instance.StatusDownloadingFormat, string.Join(", ", missing)));

        await InstallGate.WaitAsync(cancellationToken);
        // The Node-based installer only honors HTTP(S)_PROXY env vars, never OS proxy
        // settings; the spawned node process inherits this process' environment. The password
        // is therefore readable for other processes of this user while the download runs.
        var previousHttps = Environment.GetEnvironmentVariable("HTTPS_PROXY");
        var previousHttp = Environment.GetEnvironmentVariable("HTTP_PROXY");
        if (proxy != null)
        {
            Environment.SetEnvironmentVariable("HTTPS_PROXY", proxy.ToEnvironmentValue());
            Environment.SetEnvironmentVariable("HTTP_PROXY", proxy.ToEnvironmentValue());
        }

        try
        {
            // Playwright's installer is synchronous and CPU/network heavy; keep it off the UI
            // thread. It cannot be interrupted once started — cancelling stops the run after the
            // download, not the download itself.
            var exitCode = await Task.Run(
                () => Microsoft.Playwright.Program.Main(missing.Prepend("install").ToArray()),
                cancellationToken);

            Log.LogInformation("Browser install for {Engines} exited with {ExitCode}, proxy {Proxy}",
                string.Join(", ", missing), exitCode, proxy?.ToString() ?? "none");

            return exitCode == 0 ? null : string.Format(Strings.Instance.StatusDownloadFailedFormat, exitCode);
        }
        finally
        {
            if (proxy != null)
            {
                Environment.SetEnvironmentVariable("HTTPS_PROXY", previousHttps);
                Environment.SetEnvironmentVariable("HTTP_PROXY", previousHttp);
            }
            InstallGate.Release();
        }
    }

    /// <summary>
    /// Runs performance test for given URL against requested browsers.
    /// Skips unavailable engines (e.g. WebKit blocked on this platform) and returns skip info.
    /// </summary>
    /// <param name="progress">Reports each engine's result as it finishes, so the UI does not
    /// stand still for the length of the whole run.</param>
    public async Task<List<BrowserResult>> RunTestsAsync(string url, IEnumerable<string> selectedBrowsers,
        ProxyConfig? proxy = null, IProgress<BrowserResult>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync();

        var results = new List<BrowserResult>();
        var normalizedUrl = NormalizeUrl(url);

        foreach (var browserName in selectedBrowsers.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SupportedBrowsers.Contains(browserName, StringComparer.OrdinalIgnoreCase))
                continue;

            var result = new BrowserResult { Browser = browserName };

            IBrowser? browser = null;
            IBrowserContext? context = null;
            IPage? page = null;

            try
            {
                // Without an explicit proxy each engine falls back to its own default
                // (Chromium/Firefox/WebKit-on-macOS use the system proxy settings).
                browser = await GetBrowserType(browserName).LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Proxy = proxy?.ToPlaywrightProxy()
                });

                // Recorded with the result: the same page measured against a different engine
                // version is a different measurement.
                result.EngineVersion = browser.Version;

                // Default engine user agent on purpose: a custom UA trips bot detection and skews results.
                context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
                });

                page = await context.NewPageAsync();
                await page.AddInitScriptAsync(InitScript);

                // Playwright has no CancellationToken. Closing the context is what aborts a
                // navigation or a wait that is already in flight; the pending call then throws
                // and the catch below turns it into a cancellation.
                await using var abort = cancellationToken.Register(() => _ = context!.CloseAsync());

                var response = await page.GotoAsync(normalizedUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = 45000
                });

                if (response == null || !response.Ok)
                {
                    result.Skipped = true;
                    result.SkipReason = $"Navigation failed: HTTP {response?.Status ?? 0}";
                    results.Add(result);
                    progress?.Report(result);
                    continue;
                }

                try
                {
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 15000 });
                }
                catch (TimeoutException)
                {
                    // Long-polling/analytics keep some pages busy forever; metrics are still valid.
                }

                // Settle time so late LCP candidates, images and layout shifts get recorded.
                await page.WaitForTimeoutAsync(3000);

                // A minimal interaction finalizes LCP observation and lets the browser record an
                // 'event' entry with interactionId, so INP is not always unmeasurable in load-only
                // tests. Tab rather than a forced click on body: a click lands wherever the page
                // centre happens to be — a link, a consent banner, a submit button — which both
                // skews the measurement and performs an action on someone else's site.
                try
                {
                    await page.Keyboard.PressAsync("Tab");
                    await page.WaitForTimeoutAsync(250);
                }
                catch (Exception ex) when (ex is TimeoutException or PlaywrightException)
                {
                    // Page never took focus — INP simply stays N/A.
                }

                result.Metrics = await ExtractMetricsAsync(page, proxy);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                // Whatever the browser threw once its context went away, the cause was the user.
                throw new OperationCanceledException(cancellationToken);
            }
            catch (PlaywrightException ex) when (IsBrowserNotAvailable(ex))
            {
                result.Skipped = true;
                result.SkipReason = $"{browserName} engine not available on this platform. {Summarize(ex, proxy)}";
            }
            catch (Exception ex)
            {
                result.Skipped = true;
                result.SkipReason = $"Error during test: {Summarize(ex, proxy)}";
            }
            finally
            {
                // Closing in this order, each on its own: a page that fails to close must not
                // leave the browser process running for the rest of the application's lifetime.
                if (page != null) await CloseQuietlyAsync(() => page!.CloseAsync());
                if (context != null) await CloseQuietlyAsync(() => context!.CloseAsync());
                if (browser != null) await CloseQuietlyAsync(() => browser!.CloseAsync());
            }

            if (result.Skipped)
            {
                Log.LogWarning("{Browser} skipped: {Reason}", result.Browser, result.SkipReason);
            }

            results.Add(result);
            progress?.Report(result);
        }

        return results;
    }

    private static async Task CloseQuietlyAsync(Func<Task> close)
    {
        try
        {
            await close();
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException or ObjectDisposedException)
        {
            // Already gone, or gone in a way we cannot do anything about.
        }
    }

    private IBrowserType GetBrowserType(string browserName)
    {
        if (_playwright == null) throw new InvalidOperationException("Playwright not initialized");

        return browserName.ToLowerInvariant() switch
        {
            "chromium" => _playwright.Chromium,
            "firefox" => _playwright.Firefox,
            "webkit" => _playwright.Webkit,
            _ => throw new NotSupportedException(browserName)
        };
    }

    /// <summary>
    /// A short, storable description of a failure. Playwright appends a multi-line call log to
    /// its messages; stored verbatim that puts internal host names, ports and local paths into
    /// the database and into every export. Proxy credentials are removed as well — they can
    /// appear in messages coming from the browser or the installer.
    /// </summary>
    private static string Summarize(Exception ex, ProxyConfig? proxy)
    {
        var firstLine = ex.Message
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim();

        if (string.IsNullOrEmpty(firstLine))
            firstLine = ex.GetType().Name;

        var callLog = firstLine.IndexOf("Call log", StringComparison.OrdinalIgnoreCase);
        if (callLog > 0)
            firstLine = firstLine[..callLog].TrimEnd();

        if (firstLine.Length > 160)
            firstLine = firstLine[..160].TrimEnd() + "…";

        return proxy?.Redact(firstLine) ?? firstLine;
    }

    private static bool IsBrowserNotAvailable(PlaywrightException ex)
    {
        var msg = ex.Message.ToLowerInvariant();
        return msg.Contains("not found")
            || msg.Contains("executable")
            || msg.Contains("browser not installed")
            || msg.Contains("failed to launch");
    }

    /// <summary>
    /// Completes user input into a URL to navigate to. An explicit http(s) scheme is kept as
    /// typed; anything else gets a scheme prefixed — https, except for local development targets,
    /// which rarely serve TLS and would otherwise fail the handshake.
    /// </summary>
    public static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "about:blank";

        var value = url.Trim();

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return (IsLocalHost(value) ? "http://" : "https://") + value;
    }

    private static bool IsLocalHost(string schemeless)
    {
        var host = schemeless.Split('/', 2)[0];
        // Strip a port, but keep an IPv6 literal in brackets intact.
        if (!host.StartsWith('[') && host.Contains(':'))
            host = host.Split(':', 2)[0];

        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.Ordinal)
            || host.Equals("[::1]", StringComparison.Ordinal)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".test", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<List<PerformanceMetric>> ExtractMetricsAsync(IPage page, ProxyConfig? proxy)
    {
        RawMetrics raw;
        try
        {
            raw = await page.EvaluateAsync<RawMetrics>(ExtractScript) ?? new RawMetrics();
        }
        catch (Exception ex)
        {
            var reason = Summarize(ex, proxy);
            return new[] { "LCP", "FCP", "CLS", "LoadTime", "TTFB", "INP", "DOMContentLoaded" }
                .Select(name => new PerformanceMetric
                {
                    Name = name,
                    Unit = name == "CLS" ? "" : "ms",
                    Note = $"N/A (extraction error: {reason})"
                })
                .ToList();
        }

        const string notSupported = "N/A (not supported by this engine)";

        return new List<PerformanceMetric>
        {
            Metric("LCP", raw.Lcp, "ms", raw.LcpSupported ? "N/A (no LCP candidate recorded)" : notSupported),
            Metric("FCP", raw.Fcp, "ms", "N/A (no paint entry recorded)"),
            // The observation window matters: shifts after it are not in the number.
            Metric("CLS", raw.Cls, "", notSupported,
                measuredNote: "Layout shifts observed until ~3.3 s after load"),
            Metric("LoadTime", raw.LoadTime, "ms", "N/A (navigation timing unavailable)"),
            Metric("TTFB", raw.Ttfb, "ms", "N/A (navigation timing unavailable)"),
            // A value here comes from the automated key press above, not from a person using the
            // page. It is reported, but never without saying what it is.
            Metric("INP", raw.Inp, "ms", raw.InpSupported
                    ? "N/A (no qualifying interaction; automated measurement limited)"
                    : notSupported,
                measuredNote: "Synthetic interaction (automated key press), not real user latency"),
            Metric("DOMContentLoaded", raw.DomContentLoaded, "ms", "N/A (navigation timing unavailable)")
        };

        static PerformanceMetric Metric(string name, double? value, string unit, string naNote,
            string? measuredNote = null) => new()
        {
            Name = name,
            Value = value,
            Unit = unit,
            Note = value.HasValue ? measuredNote : naNote
        };
    }

    public async ValueTask DisposeAsync()
    {
        _playwright?.Dispose();
        _playwright = null;
        await ValueTask.CompletedTask;
    }
}
