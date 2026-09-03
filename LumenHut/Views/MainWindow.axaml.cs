using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LumenHut.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void About_OnClick(object? sender, RoutedEventArgs e)
    {
        foreach (var owned in OwnedWindows)
        {
            if (owned is AboutWindow existing)
            {
                existing.Activate();
                return;
            }
        }

        await new AboutWindow().ShowDialog(this);
    }
}
