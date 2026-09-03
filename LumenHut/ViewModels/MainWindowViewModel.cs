using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumenHut.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace LumenHut.ViewModels;

/// <summary>
/// Shell view model: owns the three pages and drives the navigation in the hero strip.
/// The pages live for the app's lifetime so that a measurement keeps running while the
/// user looks at the history or the settings.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IAsyncDisposable
{
    private const string MeasureKey = "measure";
    private const string HistoryKey = "history";
    private const string SettingsKey = "settings";

    public MeasureViewModel Measure { get; }
    public HistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }

    public IReadOnlyList<NavigationItem> Navigation { get; }

    [ObservableProperty]
    private ViewModelBase _currentPage;

    public MainWindowViewModel()
    {
        // The persisted language is applied before the first page exists, not from inside a
        // page's constructor — see SettingsService.ApplyPersistedLanguage.
        if (!Design.IsDesignMode)
            SettingsService.ApplyPersistedLanguage();

        Settings = new SettingsViewModel();
        Measure = new MeasureViewModel(Settings.BuildProxy, Settings.ShouldStoreFullUrl);
        History = new HistoryViewModel();

        Measure.ResultsSaved += async (_, _) => await History.LoadHistoryAsync();
        History.RunOpened += async (_, runId) =>
        {
            Navigate(MeasureKey);
            await Measure.LoadRunAsync(runId);
        };

        Navigation = new[]
        {
            new NavigationItem(MeasureKey, s => s.NavMeasure),
            new NavigationItem(HistoryKey, s => s.NavHistory),
            new NavigationItem(SettingsKey, s => s.NavSettings)
        };

        _currentPage = Measure;
        Navigate(MeasureKey);

        if (!Design.IsDesignMode)
            _ = ApplyRetentionAsync();
    }

    /// <summary>
    /// Enforces the retention period once per start. Runs after the pages exist so the history
    /// can be refreshed if anything was removed.
    /// </summary>
    private async Task ApplyRetentionAsync()
    {
        try
        {
            var removed = await HistoryMaintenance.ApplyRetentionAsync(Settings.RetentionDays);
            if (removed == 0)
                return;

            await History.LoadHistoryAsync();
            History.ReportRetention(removed);
        }
        catch (Exception)
        {
            // Housekeeping: a failure here must not keep the application from starting. The
            // next start tries again.
        }
    }

    [RelayCommand]
    private void Navigate(string key)
    {
        CurrentPage = key switch
        {
            HistoryKey => History,
            SettingsKey => Settings,
            _ => Measure
        };

        foreach (var item in Navigation)
            item.IsActive = item.Key == key;
    }

    public async ValueTask DisposeAsync()
    {
        History.Dispose();
        Settings.Dispose();
        await Measure.DisposeAsync();
    }
}

/// <summary>One navigation pill. Its label re-renders when the UI language changes.</summary>
public sealed partial class NavigationItem : ObservableObject
{
    private readonly Func<Strings, string> _title;

    [ObservableProperty]
    private bool _isActive;

    public NavigationItem(string key, Func<Strings, string> title)
    {
        Key = key;
        _title = title;
        Strings.Instance.PropertyChanged += OnStringsChanged;
    }

    public string Key { get; }

    public string Title => _title(Strings.Instance);

    private void OnStringsChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(nameof(Title));
}
