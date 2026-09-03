using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace LumenHut.Services;

public enum AppLanguage
{
    German,
    English
}

/// <summary>
/// All user-facing UI text in German and English. Bound from XAML via the view models'
/// <c>S</c> property; switching <see cref="Language"/> raises PropertyChanged with an empty
/// name, which Avalonia treats as "all properties changed" and re-evaluates every binding.
/// </summary>
/// <remarks>
/// Only non-persisted text lives here. Diagnostic strings that are written to SQLite
/// (BrowserResult.SkipReason, PerformanceMetric.Note) stay English on purpose: they are stored
/// verbatim, so localizing them would give a history whose language depends on when it was recorded.
/// </remarks>
public sealed class Strings : INotifyPropertyChanged
{
    public static Strings Instance { get; } = new();

    private AppLanguage _language = DetectSystemLanguage();

    private Strings() { }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    /// <summary>German for German-language systems, English everywhere else.</summary>
    private static AppLanguage DetectSystemLanguage() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.German
            : AppLanguage.English;

    private string T(string de, string en) => _language == AppLanguage.German ? de : en;

    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// Culture for numbers and dates on screen. Follows the selected UI language rather than the
    /// system culture: a German interface showing "0.123" for CLS reads as a different number.
    /// </summary>
    public CultureInfo Culture => _language == AppLanguage.German ? GermanCulture : EnglishCulture;

    // Shell / Navigation
    public string WindowTitle => T("LumenHut – Web-Performance messen", "LumenHut – measure web performance");
    public string BrandEyebrow => T("Mensch · Aufgabe · Technik", "Mensch · Aufgabe · Technik");
    public string BrandSubtitle => T("Web-Performance über Chromium, Firefox und WebKit messen",
        "Measure web performance across Chromium, Firefox and WebKit");
    public string NavMeasure => T("Messung", "Measurement");
    public string NavHistory => T("Verlauf", "History");
    public string NavSettings => T("Einstellungen", "Settings");
    public string NavAbout => T("Über LumenHut", "About LumenHut");
    public string NavAboutShort => T("Über", "About");
    public string NavAboutTooltip => T("Über diese App", "About this app");

    // Messung
    public string MeasureTitle => T("Messung", "Measurement");
    public string UrlLabel => T("Ziel-URL", "Target URL");
    public string UrlPlaceholder => T("z. B. https://example.com", "e.g. https://example.com");
    public string RunMeasurement => T("Messung starten", "Run measurement");
    public string CancelMeasurement => T("Abbrechen", "Cancel");
    public string EnginesLabel => T("Browser-Engines", "Browser engines");
    public string RepeatLabel => T("Durchläufe", "Runs");
    public string RepeatSingle => T("1 Durchlauf (schnell)", "1 run (fast)");
    public string RepeatManyFormat => T("{0} Durchläufe (Median)", "{0} runs (median)");
    public string RepeatHint => T(
        "Ein einzelner Lauf schwankt deutlich. Mehrere Durchläufe melden den Median und die "
        + "Spanne — und dauern entsprechend länger.",
        "A single run varies noticeably. Several runs report the median and the range — and take "
        + "correspondingly longer.");
    public string WebKitHint => T("WebKit ist unter Windows unter Umständen nicht verfügbar.",
                                  "WebKit may be unavailable on Windows.");
    public string ResultsTitle => T("Ergebnisse", "Results");
    public string NoResults => T("Noch keine Messwerte. URL eingeben und Messung starten.",
                                 "No measurements yet. Enter a URL and start a measurement.");
    public string SkippedBadge => T("ÜBERSPRUNGEN", "SKIPPED");
    public string ChartTitle => T("LCP-Vergleich", "LCP comparison");
    public string ChartPlotTitle => T("LCP (ms) je Browser-Engine", "LCP (ms) by browser engine");
    public string ChartNoData => T("Keine LCP-Daten", "No LCP data");
    public string ChartMissingFormat => T("Ohne LCP-Wert: {0}", "Without an LCP value: {0}");
    /// <summary>Shown in place of a number that was not measured. Stays "N/A" in both languages.</summary>
    public string MetricNotMeasured => "N/A";
    public string ChartAxisLcp => T("LCP (ms)", "LCP (ms)");
    public string ExportJson => T("JSON exportieren", "Export JSON");
    public string ExportCsv => T("CSV exportieren", "Export CSV");
    public string CopyToClipboard => T("In Zwischenablage", "Copy to clipboard");

    // Einordnung der Messwerte
    public string RatingLegend => T(
        "Farbe = Schwellenwert von web.dev: grün gut, gelb verbesserungswürdig, rot schlecht. "
        + "Ohne Punkt gibt es keinen veröffentlichten Schwellenwert.",
        "Colour = threshold from web.dev: green good, amber needs improvement, red poor. "
        + "No dot means there is no published threshold.");
    public string HowToReadTitle => T("So sind diese Zahlen zu lesen", "How to read these numbers");
    public string HowToReadBody => T(
        "Ein Labormesswert aus einem einzigen Kaltlauf, ohne Drosselung von CPU oder Netz, "
        + "Desktop-Fenster 1280×720, leerer Cache. Das ist nicht mit PageSpeed-Insights-Werten "
        + "aus Felddaten vergleichbar, und einzelne Läufe schwanken deutlich. "
        + "Welche Metriken eine Engine liefert, hängt von ihrer Version ab und wird bei jedem "
        + "Lauf geprüft — derzeit meldet CLS nur Chromium. Nicht Messbares erscheint als "
        + "„nicht messbar“, nicht als 0.",
        "A laboratory value from a single cold run, without CPU or network throttling, in a "
        + "1280×720 desktop window with an empty cache. It is not comparable to PageSpeed "
        + "Insights field data, and single runs vary noticeably. Which metrics an engine reports "
        + "depends on its version and is detected on every run — currently only Chromium reports "
        + "CLS. What cannot be measured appears as \"not measured\", not as 0.");
    public string EngineSupportHint => T(
        "Was eine Engine liefert, hängt von ihrer Version ab und wird bei jedem Lauf geprüft. "
        + "Derzeit meldet CLS nur Chromium; Nicht-Unterstütztes erscheint als N/A mit Begründung.",
        "What an engine provides depends on its version and is detected on every run. "
        + "Currently only Chromium reports CLS; what it cannot measure appears as N/A with a reason.");
    public string ExportMarkdown => T("Markdown exportieren", "Export Markdown");

    // Statusmeldungen Messung
    public string StatusReady => T("Bereit. URL eingeben und Messung starten.",
                                   "Ready. Enter a URL and start a measurement.");
    public string StatusUrlRequired => T("Bitte eine gültige URL angeben.", "Please provide a valid URL.");
    public string StatusEngineRequired => T("Bitte mindestens eine Browser-Engine auswählen.",
                                            "Please select at least one browser engine.");
    /// <summary>Deliberately does not repeat the entered value: it may contain a password.</summary>
    public string StatusProxyInvalid => T(
        "Proxy-Adresse ungültig. Beispiel: http://proxy.local:3128",
        "Invalid proxy address. Example: http://proxy.local:3128");
    public string StatusRunningFormat => T("Messung für {0} läuft ({1})…", "Measuring {0} ({1})…");
    public string StatusCompleted => T("Messung abgeschlossen und gespeichert.",
                                       "Measurement completed and saved.");
    public string StatusEngineDoneFormat => T("{0} von {1} fertig – {2}", "{0} of {1} done – {2}");
    public string StatusPassEngineFormat => T("Durchlauf {0}/{1} – {2} von {3} fertig – {4}",
                                              "Run {0}/{1} – {2} of {3} done – {4}");
    public string StatusCancelled => T("Messung abgebrochen.", "Measurement cancelled.");
    public string StatusCancelledPartialFormat => T(
        "Messung abgebrochen. {0} von {1} Engines wurden gespeichert.",
        "Measurement cancelled. {0} of {1} engines were saved.");
    public string StatusErrorFormat => T("Fehler: {0}", "Error: {0}");
    public string StatusLoadedRunFormat => T("Messung #{0} aus dem Verlauf geladen.",
                                             "Loaded run #{0} from history.");

    // Browser-Download (Erststart)
    public string StatusDownloadingFormat => T(
        "Erststart: Browser-Engines werden heruntergeladen ({0}). Das kann einige Minuten dauern…",
        "First run: downloading browser engines ({0}). This can take a few minutes…");
    public string StatusDownloadFailedFormat => T(
        "Browser-Installation fehlgeschlagen (Exit-Code {0}). Netzwerkzugang prüfen und erneut versuchen.",
        "Browser installation failed (exit code {0}). Check network access and retry.");

    // Verlauf
    public string HistoryTitle => T("Verlauf", "History");
    public string HistorySubtitle => T("Die letzten 20 Messungen aus der lokalen Datenbank",
                                       "The last 20 measurements from the local database");
    public string HistoryRefresh => T("Aktualisieren", "Refresh");
    public string HistoryLoad => T("Messung öffnen", "Open measurement");
    public string HistoryEmpty => T("Noch keine gespeicherten Messungen.", "No stored measurements yet.");
    public string HistoryLoadErrorFormat => T("Verlauf konnte nicht geladen werden: {0}",
                                              "Could not load history: {0}");
    public string HistoryDelete => T("Messung löschen", "Delete measurement");
    public string HistoryClear => T("Verlauf leeren", "Clear history");
    public string HistoryClearConfirm => T("Wirklich alles löschen?", "Really delete everything?");
    public string HistoryDeletedFormat => T("Messung #{0} gelöscht.", "Deleted measurement #{0}.");
    public string HistoryClearedFormat => T("{0} Messungen gelöscht.", "Deleted {0} measurements.");
    public string HistoryDeleteErrorFormat => T("Löschen fehlgeschlagen: {0}", "Deleting failed: {0}");
    public string HistoryCompare => T("Vergleichen", "Compare");
    public string HistoryCompareHint => T(
        "Zwei Messungen auswählen (Cmd/Strg-Klick) und vergleichen — beantwortet „ist die Seite besser geworden?“.",
        "Select two measurements (Cmd/Ctrl-click) and compare — this answers \"did the page get faster?\".");
    public string HistoryCompareNeedTwo => T("Bitte genau zwei Messungen auswählen.",
                                             "Please select exactly two measurements.");
    public string HistoryCompareTitle => T("Vergleich", "Comparison");
    public string HistoryCompareHeaderFormat => T("älter: {0}  →  neuer: {1}",
                                                  "older: {0}  →  newer: {1}");
    public string HistoryCompareDifferentUrls => T(
        "Achtung: die beiden Messungen betreffen unterschiedliche URLs.",
        "Careful: the two measurements are for different URLs.");
    public string HistoryCompareOlder => T("älter", "older");
    public string HistoryCompareNewer => T("neuer", "newer");
    public string HistoryCompareDelta => T("Differenz", "Difference");
    public string HistoryNoteLabel => T("Notiz zur ausgewählten Messung", "Note on the selected measurement");
    public string HistoryNotePlaceholder => T("z. B. nach dem Bilder-Umbau", "e.g. after the image rework");
    public string HistoryNoteSave => T("Notiz speichern", "Save note");
    public string HistoryNoteSaved => T("Notiz gespeichert.", "Note saved.");
    public string HistoryRetentionAppliedFormat => T(
        "{0} Messungen wegen der Aufbewahrungsfrist gelöscht.",
        "Deleted {0} measurements to honour the retention period.");

    // Einstellungen
    public string SettingsNetworkSection => T("Netzwerk", "Network");
    public string ProxyLabel => T("HTTP-Proxy", "HTTP proxy");
    public string ProxyPlaceholder => T("optional, z. B. http://proxy.local:3128",
                                        "optional, e.g. http://proxy.local:3128");
    public string ProxyHint => T(
        "Leer lassen für die Systemeinstellung. Gilt für Messungen und für den Browser-Download.",
        "Leave empty to use the system setting. Applies to measurements and to the browser download.");
    public string ProxyUserLabel => T("Benutzername (optional)", "User name (optional)");
    public string ProxyPasswordLabel => T("Passwort (optional)", "Password (optional)");
    public string ProxyPasswordHint => T(
        "Wird nur für diese Sitzung behalten und nicht in settings.json geschrieben.",
        "Kept for this session only and never written to settings.json.");
    public string SettingsLanguageSection => T("Sprache", "Language");
    public string LanguageLabel => T("Sprache der Oberfläche", "Interface language");
    public string LanguageHint => T("Wirkt sofort und wird gespeichert.",
                                    "Applies immediately and is saved.");
    public string SettingsStorageSection => T("Datenablage", "Data storage");
    public string StorageHint => T("Messungen und Einstellungen liegen lokal unter:",
                                   "Measurements and settings are stored locally under:");
    public string PrivacyStoredHint => T(
        "Gespeichert werden: gemessene URL, Zeitpunkt, Messwerte und die Fehlermeldungen der Engines. "
        + "Die Daten verlassen das Gerät nur, wenn du sie exportierst.",
        "Stored per run: the measured URL, the time, the metrics and the engines' error messages. "
        + "The data leaves this device only when you export it.");
    public string PrivacyRetentionHint => T(
        "Messungen bleiben unbegrenzt gespeichert. Zum Löschen die Datei perfdata.db im Ordner oben entfernen.",
        "Runs are kept indefinitely. To delete them, remove perfdata.db in the folder above.");
    public string PrivacyLogHint => T(
        "Protokoll: lumenhut.log im Ordner oben, maximal 1 MB mit einer Rotation. Es enthält "
        + "Läufe, Fehler und Engine-Versionen — keine Passwörter und keine Query-Strings.",
        "Log: lumenhut.log in the folder above, at most 1 MB with one rotation. It records runs, "
        + "errors and engine versions — no passwords and no query strings.");
    public string PrivacyDownloadHint => T(
        "Beim ersten Lauf lädt die App die Browser-Engines von Microsoft-CDNs (cdn.playwright.dev). "
        + "Jede Messung ruft die Zielseite samt ihrer Drittanbieter auf.",
        "On the first run the application downloads the browser engines from Microsoft CDNs "
        + "(cdn.playwright.dev). Every measurement loads the target page including its third parties.");
    public string RetentionLabel => T("Aufbewahrung", "Retention");
    public string RetentionUnlimited => T("unbegrenzt", "keep everything");
    public string RetentionDaysFormat => T("{0} Tage", "{0} days");
    public string RetentionHint => T(
        "Ältere Messungen werden beim Programmstart gelöscht. Voreinstellung ist unbegrenzt, "
        + "damit nichts unbemerkt verschwindet.",
        "Older measurements are deleted at startup. The default keeps everything, so nothing "
        + "disappears unnoticed.");
    public string StoreFullUrlLabel => T("Vollständige URL speichern",
                                         "Store the full URL");
    public string StoreFullUrlHint => T(
        "Aus: nur Schema, Host und Pfad werden gespeichert. Ein Query-String kann Sitzungs-Token "
        + "oder Mailadressen enthalten. Zugangsdaten in der URL werden immer entfernt.",
        "Off: only scheme, host and path are stored. A query string can carry session tokens or "
        + "mail addresses. Credentials in the URL are always removed.");
    public string Save => T("Speichern", "Save");
    public string SettingsSaved => T("Einstellungen gespeichert.", "Settings saved.");
    public string SettingsSaveErrorFormat => T("Einstellungen konnten nicht gespeichert werden: {0}",
                                               "Could not save settings: {0}");

    // Export
    public string ExportNoResults => T("Keine Ergebnisse zum Exportieren.", "No results to export.");
    public string ExportCancelled => T("Export abgebrochen.", "Export cancelled.");
    public string ExportNoWindow => T("Export nicht möglich: kein Fenster für den Speicherdialog.",
                                      "Export failed: no window available for the save dialog.");
    public string ExportDialogTitleFormat => T("{0} exportieren", "Export {0}");
    public string ExportDoneFormat => T("{0} nach {1} exportiert.", "Exported {0} to {1}.");
    public string ExportFailedFormat => T("Export fehlgeschlagen: {0}", "Export failed: {0}");
    public string ExportReportHeading => T("Browserübergreifender Performance-Bericht",
                                           "Cross-browser performance report");
    public string ExportReportUrl => T("URL", "URL");
    public string ExportReportTime => T("Zeitpunkt", "Time");
    public string ExportReportSkipped => T("Übersprungen", "Skipped");
    public string ExportReportTool => T("Werkzeug", "Tool");
    public string ExportReportViewport => T("Fenster", "Viewport");
    public string ExportCopied => T("Bericht in die Zwischenablage kopiert.",
                                    "Report copied to the clipboard.");
    public string ExportClipboardUnavailable => T("Zwischenablage nicht verfügbar.",
                                                  "Clipboard not available.");
    public string ExportTableMetric => T("Messwert", "Metric");
    public string ExportTableValue => T("Wert", "Value");
    public string ExportTableUnit => T("Einheit", "Unit");
    public string ExportTableNote => T("Hinweis", "Note");

    // Über-Fenster
    public string AboutTitle => T("Über LumenHut", "About LumenHut");
    public string AboutSubtitle => T("Web-Performance über Chromium, Firefox und WebKit",
                                     "Web performance across Chromium, Firefox and WebKit");
    public string AboutVersionFormat => T("Version {0}", "Version {0}");
    public string Close => T("Schließen", "Close");
    public string AboutWebsiteTooltip => "https://managentis.com";

    /// <summary>
    /// One sentence per metric, shown as a tooltip. Thresholds from web.dev; metrics without a
    /// published threshold say so instead of implying one.
    /// </summary>
    public string MetricDescription(string metricName) => metricName switch
    {
        "LCP" => T("Largest Contentful Paint: wann der größte Inhalt sichtbar ist. Gut bis 2,5 s, schlecht ab 4 s.",
                   "Largest Contentful Paint: when the largest content element becomes visible. Good up to 2.5 s, poor from 4 s."),
        "FCP" => T("First Contentful Paint: wann überhaupt zuerst etwas gezeichnet wird. Gut bis 1,8 s, schlecht ab 3 s.",
                   "First Contentful Paint: when anything is first painted. Good up to 1.8 s, poor from 3 s."),
        "CLS" => T("Cumulative Layout Shift: wie stark das Layout nachträglich springt. Gut bis 0,1, schlecht ab 0,25.",
                   "Cumulative Layout Shift: how much the layout moves after loading. Good up to 0.1, poor from 0.25."),
        "INP" => T("Interaction to Next Paint: Reaktionszeit auf eine Eingabe. Gut bis 200 ms, schlecht ab 500 ms. "
                   + "Hier synthetisch erzeugt, deshalb nur ein Anhaltspunkt.",
                   "Interaction to Next Paint: how long the page takes to respond to input. Good up to 200 ms, "
                   + "poor from 500 ms. Produced synthetically here, so treat it as an indication only."),
        "TTFB" => T("Time to First Byte: Wartezeit bis zum ersten Byte der Antwort. Gut bis 0,8 s, schlecht ab 1,8 s.",
                    "Time to First Byte: wait until the first byte of the response. Good up to 0.8 s, poor from 1.8 s."),
        "LoadTime" => T("Ladezeit bis zum load-Ereignis. Kein veröffentlichter Schwellenwert, deshalb ohne Bewertung.",
                        "Time until the load event. No published threshold, so it carries no rating."),
        "DOMContentLoaded" => T("Zeit bis das DOM geladen ist. Kein veröffentlichter Schwellenwert, deshalb ohne Bewertung.",
                                "Time until the DOM is loaded. No published threshold, so it carries no rating."),
        _ => string.Empty
    };

    /// <summary>Options for the language ComboBox; labels stay in their own language on purpose.</summary>
    public static IReadOnlyList<LanguageOption> Options { get; } = new[]
    {
        new LanguageOption(AppLanguage.German, "Deutsch"),
        new LanguageOption(AppLanguage.English, "English")
    };
}

public sealed record LanguageOption(AppLanguage Value, string Display)
{
    public override string ToString() => Display;
}
