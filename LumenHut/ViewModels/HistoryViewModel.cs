using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumenHut.Data;
using LumenHut.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace LumenHut.ViewModels;

/// <summary>History page: the most recent runs from SQLite, openable in the measurement page.</summary>
public partial class HistoryViewModel : PageViewModelBase
{
    private const int MaxEntries = 20;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveNoteCommand))]
    private TestRunSummary? _selectedHistoryItem;

    /// <summary>
    /// Second click confirms clearing the history. A two-step button instead of a modal dialog:
    /// the application has no dialog infrastructure, and this is reversible enough to warn once.
    /// </summary>
    [ObservableProperty]
    private bool _clearArmed;

    public ObservableCollection<TestRunSummary> History { get; } = new();

    /// <summary>What the list has selected; pushed in by the view, which owns the selection.</summary>
    public ObservableCollection<TestRunSummary> SelectedRuns { get; } = new();

    public ObservableCollection<ComparisonRow> Comparison { get; } = new();

    [ObservableProperty]
    private string? _comparisonHeader;

    [ObservableProperty]
    private bool _comparisonUrlsDiffer;

    public bool HasComparison => Comparison.Count > 0;

    public bool HasHistory => History.Count > 0;

    public HistoryViewModel()
    {
        History.CollectionChanged += OnHistoryChanged;

        // The XAML previewer instantiates this view model; without the guard it would create and
        // read the real user database from the designer process.
        if (!Design.IsDesignMode)
            _ = LoadHistoryAsync();
    }

    /// <summary>Raised with the run id when the user opens an entry.</summary>
    public event EventHandler<int>? RunOpened;

    private void OnHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(HasHistory));

    protected override void OnLanguageChanged()
    {
        foreach (var entry in History)
            entry.RefreshFormatting();

        OnPropertyChanged(nameof(ClearLabel));

        // The comparison holds formatted numbers, so it has to be rebuilt in the new language.
        if (HasComparison)
            _ = CompareAsync();
    }

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        List<TestRunSummary> entries;
        try
        {
            entries = await ReadHistoryAsync();
        }
        catch (Exception ex)
        {
            SetStatus(s => string.Format(s.HistoryLoadErrorFormat, ex.Message));
            return;
        }

        // Replaced in one step after the read: clearing before the await let two overlapping
        // loads (constructor and the ResultsSaved handler) leave twice the entries behind.
        History.Clear();
        foreach (var entry in entries)
            History.Add(entry);
    }

    private static async Task<List<TestRunSummary>> ReadHistoryAsync()
    {
        var runs = await RunStore.LoadRecentAsync(MaxEntries);

        return runs.Select(run => new TestRunSummary
        {
            Id = run.Id,
            Url = run.Url,
            // The column holds UTC and SQLite returns it as Unspecified; without the explicit
            // kind every entry would be shown one or two hours in the past.
            Timestamp = DateTime.SpecifyKind(run.Timestamp, DateTimeKind.Utc).ToLocalTime(),
            Notes = run.Notes,
            BrowsersTested = string.Join(", ",
                run.BrowserResults.Select(b => b.Browser + (b.Skipped ? " ⚠" : "")))
        }).ToList();
    }

    [RelayCommand(CanExecute = nameof(CanOpenSelected))]
    private void OpenSelected()
    {
        if (SelectedHistoryItem == null) return;
        RunOpened?.Invoke(this, SelectedHistoryItem.Id);
    }

    private bool CanOpenSelected() => SelectedHistoryItem != null;

    /// <summary>Label of the clear button: names the consequence once it is armed.</summary>
    public string ClearLabel => ClearArmed ? S.HistoryClearConfirm : S.HistoryClear;

    partial void OnClearArmedChanged(bool value) => OnPropertyChanged(nameof(ClearLabel));

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        var item = SelectedHistoryItem;
        if (item == null) return;

        try
        {
            var deleted = await HistoryMaintenance.DeleteRunAsync(item.Id);
            if (deleted)
            {
                var id = item.Id;
                SetStatus(s => string.Format(s.HistoryDeletedFormat, id));
            }

            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            SetStatus(s => string.Format(s.HistoryDeleteErrorFormat, ex.Message));
        }
    }

    private bool CanDeleteSelected() => SelectedHistoryItem != null;

    /// <summary>Called by the view when the list selection changes.</summary>
    public void SetSelection(IEnumerable<TestRunSummary> selected)
    {
        SelectedRuns.Clear();
        foreach (var item in selected)
            SelectedRuns.Add(item);

        CompareCommand.NotifyCanExecuteChanged();
    }

    private bool CanCompare() => SelectedRuns.Count == 2;

    [RelayCommand(CanExecute = nameof(CanCompare))]
    private async Task CompareAsync()
    {
        var picked = SelectedRuns.ToList();
        if (picked.Count != 2)
        {
            SetStatus(s => s.HistoryCompareNeedTwo);
            return;
        }

        try
        {
            // Older run first, so a negative difference always means "got faster".
            var ordered = picked.OrderBy(p => p.Timestamp).ToList();
            var older = await RunStore.LoadAsync(ordered[0].Id);
            var newer = await RunStore.LoadAsync(ordered[1].Id);

            if (older == null || newer == null)
            {
                await LoadHistoryAsync();
                return;
            }

            Comparison.Clear();
            foreach (var row in RunComparison.Build(older, newer, S.Culture))
                Comparison.Add(row);

            ComparisonUrlsDiffer = !string.Equals(older.Url, newer.Url, StringComparison.Ordinal);
            ComparisonHeader = string.Format(S.HistoryCompareHeaderFormat,
                ordered[0].TimestampDisplay, ordered[1].TimestampDisplay);

            OnPropertyChanged(nameof(HasComparison));
        }
        catch (Exception ex)
        {
            SetStatus(s => string.Format(s.HistoryLoadErrorFormat, ex.Message));
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task SaveNoteAsync()
    {
        var item = SelectedHistoryItem;
        if (item == null) return;

        try
        {
            await RunStore.UpdateNotesAsync(item.Id, item.Notes);
            SetStatus(s => s.HistoryNoteSaved);
        }
        catch (Exception ex)
        {
            SetStatus(s => string.Format(s.HistoryDeleteErrorFormat, ex.Message));
        }
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (!ClearArmed)
        {
            ClearArmed = true;
            return;
        }

        ClearArmed = false;

        try
        {
            var removed = await HistoryMaintenance.ClearAsync();
            SetStatus(s => string.Format(s.HistoryClearedFormat, removed));
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            SetStatus(s => string.Format(s.HistoryDeleteErrorFormat, ex.Message));
        }
    }

    /// <summary>Reports what the retention pass removed at startup.</summary>
    public void ReportRetention(int removed) =>
        SetStatus(s => string.Format(s.HistoryRetentionAppliedFormat, removed));


    public override void Dispose()
    {
        History.CollectionChanged -= OnHistoryChanged;
        base.Dispose();
    }
}
