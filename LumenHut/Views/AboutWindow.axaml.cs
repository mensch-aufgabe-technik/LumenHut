using Avalonia.Controls;
using Avalonia.Interactivity;
using LumenHut.Services;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace LumenHut.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = Strings.Instance;

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "1.0.0";

        // Strip the "+<commit sha>" suffix the SDK appends to InformationalVersion.
        var plus = version.IndexOf('+');
        if (plus >= 0) version = version[..plus];

        VersionText.Text = string.Format(Strings.Instance.AboutVersionFormat, version);
    }

    private void Close_OnClick(object? sender, RoutedEventArgs e) => Close();

    private void Website_OnClick(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://managentis.com");

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        // No browser or no shell handler: not worth interrupting the About dialog for.
        catch (Exception ex) when (ex is Win32Exception or PlatformNotSupportedException or InvalidOperationException)
        {
        }
    }
}
