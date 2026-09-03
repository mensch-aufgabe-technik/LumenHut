using Avalonia.Controls;
using Avalonia.Input;
using LumenHut.ViewModels;
using System.Linq;

namespace LumenHut.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The list owns its selection; the view model only needs to know what is selected, so that
    /// comparing two runs can be a command rather than view logic.
    /// </summary>
    private void History_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not HistoryViewModel vm || sender is not ListBox list)
            return;

        vm.SetSelection(list.SelectedItems?.OfType<TestRunSummary>() ?? Enumerable.Empty<TestRunSummary>());
    }

    /// <summary>Double-clicking a row opens it, like the button does.</summary>
    private void History_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is HistoryViewModel vm && vm.OpenSelectedCommand.CanExecute(null))
            vm.OpenSelectedCommand.Execute(null);
    }
}
