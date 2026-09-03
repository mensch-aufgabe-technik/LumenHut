using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LumenHut.Services;
using Microsoft.Extensions.Logging;
using LumenHut.ViewModels;
using LumenHut.Views;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace LumenHut;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            InstallAppMenu();

            var viewModel = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            // Shut down the Playwright driver process with the app. Task.Run keeps the wait off
            // the UI synchronization context — awaiting it there would deadlock as soon as a
            // continuation needs the UI thread — and the timeout keeps a hung browser from
            // holding the application open.
            desktop.Exit += (_, _) =>
            {
                Task.Run(() => viewModel.DisposeAsync().AsTask()).Wait(TimeSpan.FromSeconds(5));
                AppLog.For<App>().LogInformation("Shutting down");
                AppLog.Shutdown();
            };

            AppLog.For<App>().LogInformation("Started {Version} on {Os}",
                AppInfo.NameAndVersion, System.Runtime.InteropServices.RuntimeInformation.OSDescription);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Replaces the default "About Avalonia" entry in the macOS app menu. Built in code rather
    /// than XAML because its label follows the selected UI language.
    /// </summary>
    private void InstallAppMenu()
    {
        var about = new NativeMenuItem(Strings.Instance.NavAbout + "…");
        about.Click += AboutMenu_OnClick;

        Strings.Instance.PropertyChanged += (_, _) => about.Header = Strings.Instance.NavAbout + "…";

        NativeMenu.SetMenu(this, new NativeMenu { Items = { about } });
    }

    private void AboutMenu_OnClick(object? sender, System.EventArgs e)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            var owner = GetMainWindow();
            if (owner is null) return;

            foreach (var w in owner.OwnedWindows)
            {
                if (w is AboutWindow existing)
                {
                    existing.Activate();
                    return;
                }
            }

            await new AboutWindow().ShowDialog(owner);
        });
    }

    private Window? GetMainWindow() =>
        ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
