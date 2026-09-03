using Avalonia;
using Avalonia.Controls;
using LumenHut.Services;
using LumenHut.ViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace LumenHut.Views;

public partial class MeasureView : UserControl
{
    // Markenfarben je Engine, damit das Diagramm zum Rest der App passt.
    private static readonly ScottPlot.Color ChromiumColor = ScottPlot.Color.FromHex("#233e30");
    private static readonly ScottPlot.Color FirefoxColor = ScottPlot.Color.FromHex("#5d84b6");
    private static readonly ScottPlot.Color WebKitColor = ScottPlot.Color.FromHex("#6b8f7e");
    private static readonly ScottPlot.Color FallbackColor = ScottPlot.Color.FromHex("#9db5a6");
    private static readonly ScottPlot.Color SurfaceColor = ScottPlot.Color.FromHex("#ffffff");
    private static readonly ScottPlot.Color LineColor = ScottPlot.Color.FromHex("#e8e6e2");
    private static readonly ScottPlot.Color MutedColor = ScottPlot.Color.FromHex("#5c6b62");

    private MeasureViewModel? _vm;
    private bool _subscribed;
    private bool _attached;

    public MeasureView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // The shell's ContentControl rebuilds this view on every navigation, so subscriptions are
    // tied to the visual tree — otherwise each visit would leave another live listener behind.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Subscribe();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        Unsubscribe();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();
        _vm = DataContext as MeasureViewModel;
        if (_attached)
            Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        _vm ??= DataContext as MeasureViewModel;
        if (_vm == null) return;

        _vm.CurrentResults.CollectionChanged += OnResultsChanged;
        // Der Diagrammtitel steckt im Plot, nicht im XAML — bei Sprachwechsel neu zeichnen.
        Strings.Instance.PropertyChanged += OnStringsChanged;
        _subscribed = true;
        UpdateLcpChart(_vm);
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;

        if (_vm != null)
            _vm.CurrentResults.CollectionChanged -= OnResultsChanged;
        Strings.Instance.PropertyChanged -= OnStringsChanged;
        _subscribed = false;
    }

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_vm != null)
            UpdateLcpChart(_vm);
    }

    private void OnStringsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm != null)
            UpdateLcpChart(_vm);
    }

    private void UpdateLcpChart(MeasureViewModel vm)
    {
        var chart = this.FindControl<ScottPlot.Avalonia.AvaPlot>("LcpChart");
        if (chart == null) return;

        var plot = chart.Plot;
        plot.Clear();

        plot.FigureBackground.Color = SurfaceColor;
        plot.DataBackground.Color = SurfaceColor;
        plot.Axes.Color(MutedColor);
        plot.Grid.MajorLineColor = LineColor;

        // Only engines that actually reported an LCP value get a bar. Drawing a 0 for
        // "not measured" would present the engine without LCP support as the fastest one.
        var measured = vm.CurrentResults
            .Where(r => !r.Skipped)
            .Select(r => (r.Browser, Lcp: r.Metrics.FirstOrDefault(m => m.Name == "LCP")?.Raw))
            .Where(entry => entry.Lcp.HasValue)
            .ToList();

        if (measured.Count == 0)
        {
            plot.Title(vm.S.ChartNoData);
            chart.Refresh();
            return;
        }

        var labels = measured.Select(entry => entry.Browser).ToArray();
        var values = measured.Select(entry => entry.Lcp!.Value).ToArray();

        var bars = plot.Add.Bars(values);
        for (int i = 0; i < bars.Bars.Count; i++)
        {
            bars.Bars[i].FillColor = ColorFor(labels[i]);
            bars.Bars[i].LineColor = ColorFor(labels[i]);
        }

        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
            Enumerable.Range(0, labels.Length).Select(idx => (double)idx).ToArray(),
            labels);

        plot.Title(vm.S.ChartPlotTitle);
        plot.Axes.Left.Label.Text = vm.S.ChartAxisLcp;
        plot.Axes.AutoScale();
        chart.Refresh();
    }

    private static ScottPlot.Color ColorFor(string browser) => browser switch
    {
        "Chromium" => ChromiumColor,
        "Firefox" => FirefoxColor,
        "WebKit" => WebKitColor,
        _ => FallbackColor
    };
}
