using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LumenHut.Models;
using LumenHut.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LumenHut.ViewModels;

/// <summary>Settings page: HTTP proxy, UI language, URL storage and the local data location.</summary>
public partial class SettingsViewModel : PageViewModelBase
{
    // Empty = engine defaults (Chromium/Firefox/WebKit-on-macOS follow system proxy settings).
    [ObservableProperty]
    private string _proxyServer = string.Empty;

    [ObservableProperty]
    private string _proxyUsername = string.Empty;

    /// <summary>
    /// Session state on purpose: a proxy password in settings.json is a plain-text credential in
    /// a file that backups and sync clients pick up. The OS key store is the place for it, and
    /// until that exists the password is simply not persisted.
    /// </summary>
    [ObservableProperty]
    private string _proxyPassword = string.Empty;

    [ObservableProperty]
    private bool _storeFullUrl;

    [ObservableProperty]
    private LanguageOption _selectedLanguage;

    [ObservableProperty]
    private IReadOnlyList<RetentionOption> _retentionOptions = BuildRetentionOptions();

    [ObservableProperty]
    private RetentionOption _selectedRetention;

    public IReadOnlyList<LanguageOption> Languages => Strings.Options;

    public string DataDirectory => SettingsService.GetDataDirectory();

    public SettingsViewModel()
    {
        // Keeps the XAML previewer away from the real settings file.
        var settings = Design.IsDesignMode ? new AppSettings() : SettingsService.Load();
        _proxyServer = settings.ProxyServer;
        _proxyUsername = settings.ProxyUsername;
        _storeFullUrl = settings.StoreFullUrl;

        // The persisted language is already applied by MainWindowViewModel; setting it here
        // would notify this instance mid-construction.
        _selectedLanguage = Strings.Options.First(o => o.Value == Strings.Instance.Language);

        // The options are built once the language is settled, so their labels match the UI.
        _retentionOptions = BuildRetentionOptions();
        _selectedRetention = _retentionOptions.FirstOrDefault(o => o.Days == settings.RetentionDays)
                            ?? _retentionOptions[0];
    }

    private static IReadOnlyList<RetentionOption> BuildRetentionOptions()
    {
        var strings = Strings.Instance;
        return new[]
        {
            new RetentionOption(0, strings.RetentionUnlimited),
            new RetentionOption(30, string.Format(strings.RetentionDaysFormat, 30)),
            new RetentionOption(90, string.Format(strings.RetentionDaysFormat, 90)),
            new RetentionOption(365, string.Format(strings.RetentionDaysFormat, 365))
        };
    }

    /// <summary>Days after which stored runs are deleted at startup; 0 keeps everything.</summary>
    public int RetentionDays => SelectedRetention.Days;

    protected override void OnLanguageChanged()
    {
        var days = SelectedRetention.Days;
        RetentionOptions = BuildRetentionOptions();
        SelectedRetention = RetentionOptions.First(o => o.Days == days);
    }

    /// <summary>
    /// The proxy as currently entered, so a change takes effect on the next run without an app
    /// restart. Throws FormatException for an unparseable address; the caller shows a message.
    /// </summary>
    public ProxyConfig? BuildProxy() =>
        ProxyConfig.Parse(ProxyServer, ProxyUsername, ProxyPassword);

    /// <summary>Whether a measured URL is stored with its query string.</summary>
    public bool ShouldStoreFullUrl() => StoreFullUrl;

    /// <summary>Language switches apply and persist immediately — the whole UI re-renders at once.</summary>
    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        Strings.Instance.Language = value.Value;
        Persist();
    }

    /// <summary>Persisted right away: the setting decides what a run writes to disk, and a
    /// forgotten Save would be the wrong kind of surprise.</summary>
    partial void OnStoreFullUrlChanged(bool value) => Persist();

    /// <summary>Takes effect at the next start, so nothing is deleted while the page is open.</summary>
    partial void OnSelectedRetentionChanged(RetentionOption value) => Persist();

    [RelayCommand]
    private void Save()
    {
        if (Persist())
            SetStatus(s => s.SettingsSaved);
    }

    private bool Persist()
    {
        try
        {
            SettingsService.Save(new AppSettings
            {
                ProxyServer = ProxyServer.Trim(),
                ProxyUsername = ProxyUsername.Trim(),
                Language = Strings.Instance.Language,
                StoreFullUrl = StoreFullUrl,
                RetentionDays = RetentionDays
            });
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus(s => string.Format(s.SettingsSaveErrorFormat, ex.Message));
            return false;
        }
    }
}

/// <summary>One entry of the retention drop-down; the label follows the UI language.</summary>
public sealed record RetentionOption(int Days, string Display)
{
    public override string ToString() => Display;
}
