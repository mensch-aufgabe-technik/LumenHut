# Technische Details

Ergänzung zur [README](../README.md) für alle, die den Code lesen, ändern oder eine eigene
Programmdatei bauen wollen. Bedienung, Messwerte und Datenschutz stehen dort und werden hier
nicht wiederholt.

- Avalonia 12 auf .NET 10, Desktop-Anwendung
- `LumenHut/LumenHut.csproj` ist das Hauptprojekt, `LumenHut.Tests/LumenHut.Tests.csproj` die Tests

## Für die Verteilung bauen

Eigenständige Einzeldateien, die keine installierte .NET-Laufzeit brauchen. Die Befehle aus dem
Projektverzeichnis `LumenHut/` heraus aufrufen:

```bash
dotnet publish -c Release -r win-x64   -p:PublishSingleFile=true -o ../dist/win-x64
dotnet publish -c Release -r osx-arm64 -p:PublishSingleFile=true -o ../dist/osx-arm64
dotnet publish -c Release -r osx-x64   -p:PublishSingleFile=true -o ../dist/osx-x64
```

Ergebnis: `dist/win-x64/LumenHut.exe` (eine Datei, rund 110 MB) beziehungsweise
`dist/osx-*/LumenHut`.

- `PublishSingleFile=true` aktiviert eine bedingte `PropertyGroup` in `LumenHut.csproj`, die
  `SelfContained`, `IncludeAllContentForSelfExtract`, `EnableCompressionInSingleFile` und
  `DebugType=none` setzt. Diese Eigenschaften stehen absichtlich im csproj und nicht in der
  Befehlszeile, damit Dokumentation und Build nicht auseinanderlaufen.
- `IncludeAllContentForSelfExtract` ist zwingend: Der Playwright-Treiber (`.playwright/` mit
  node) ist ein Content-Ordner, der sonst wegfällt oder lose neben der Programmdatei liegt.
- **Trimming ist bewusst aus** (`PublishTrimmed=false`). Avalonia, EF Core und Playwright
  arbeiten mit Reflection; getrimmte Builds brechen zur Laufzeit. `-p:PublishTrimmed=true` nicht
  ohne vollständigen Handtest ergänzen.
- Beim ersten Start entpackt sich das Bundle samt Playwright-Treiber in das Temp-Verzeichnis des
  Benutzers (`%TEMP%\.net\LumenHut\` unter Windows); weitere Starts verwenden es wieder.
- Die Programmdateien sind **nicht signiert**: Windows SmartScreen und macOS Gatekeeper warnen
  beim ersten Start. Es werden keine Installationspakete, `.app`-Bundles oder `.dmg` erzeugt.
- Playwright bettet **keine** Browser ein. Chromium, Firefox und WebKit landen beim ersten Lauf
  im Playwright-Cache des Benutzers (`~/Library/Caches/ms-playwright`,
  `%LocalAppData%\ms-playwright`). Der erste Start braucht deshalb Netz und einige Minuten.

## Getroffene Entscheidungen

Diese Punkte sind bewusst so und nicht anders gelöst. Wer sie umbaut, sollte den Grund kennen.

- **Kein Repository-Pattern, aber auch kein EF in den ViewModels.** Die Persistenz liegt in
  statischen Diensten, die `DbContext` direkt verwenden: `Services/RunStore.cs` (speichern,
  laden, letzte Läufe, Notizen) und `Services/HistoryMaintenance.cs` (löschen, leeren,
  Aufbewahrung samt `VACUUM`). Beide nehmen einen optionalen `dbPath` — genau das macht sie gegen
  eine temporäre Datenbank testbar, und Produktivpfad und Tests führen denselben Code aus.
- **Das Schema wird migriert, nicht `EnsureCreated`.** `Data/Migrations/` plus
  `Data/DatabaseInitializer.cs`, der eine Datenbank aus der Zeit vor den Migrationen mit der
  ersten Migration stempelt, bevor er migriert. Jede Aufrufstelle initialisiert über den
  `DatabaseInitializer`. `EnsureCreated` darf nicht zurückkommen, sonst zerbricht die nächste
  Schemaänderung installierte Kopien.
- **Ein Lauf speichert seine eigenen Bedingungen** (Engine-Version je Ergebnis, Betriebssystem,
  Programmversion, Fenstergröße, ob ein Proxy verwendet wurde — nie die Proxy-Adresse). Ohne
  diese Angaben sind zwei Läufe im Abstand von Wochen nicht vergleichbar.
- **Die Einstellungen liegen in `settings.json` neben der Datenbank**, absichtlich nicht in der
  Datenbank: So bleibt die Datei von Hand editierbar, und eine kaputte Einstellungsdatei nimmt
  nicht die Messwerte mit. **Das Proxy-Passwort wird nicht persistiert**; es ist Sitzungszustand
  im `SettingsViewModel`.
- **Nicht unterstützte Messwerte sind N/A, nie 0.** Ob eine Engine eine Metrik liefert, wird zur
  Laufzeit über `PerformanceObserver.supportedEntryTypes` erkannt, nicht anhand einer fest
  hinterlegten Tabelle — deshalb wandert die Matrix mit den Browser-Versionen. `MetricView`
  trägt den rohen `double?`, damit Diagramm, Export und Anzeige die Messung lesen und nicht
  einen formatierten String zurückparsen.
- **Mehrere Durchläufe** führt `Services/MeasurementAggregator.cs` zusammen (Median plus Spanne
  in der Notiz); ein einzelner Durchlauf geht unverändert durch.
  `Services/RunComparison.cs` vergleicht zwei gespeicherte Läufe.
- **Gemessene URLs reduziert `Services/UrlPrivacy.cs`** vor dem Speichern (Zugangsdaten immer,
  Query-String sofern nicht ausdrücklich anders gewählt). Fehlertexte der Engines werden in
  `PlaywrightPerfService.Summarize` gekürzt und von Proxy-Zugangsdaten befreit, bevor sie
  gespeichert oder exportiert werden.
- **`Services/AppLog.cs`** schreibt `lumenhut.log` neben die Datenbank (1 MB, eine Rotation).
  Was protokolliert werden darf, ist eingeschränkt: keine Proxy-Zugangsdaten, keine
  Query-Strings, und das SQL-Logging von EF Core bleibt aus, weil dessen Parameter die URLs
  tragen würden. `LUMENHUT_LOG_DIR` lenkt das Protokoll um; das Testprojekt setzt es auf ein
  temporäres Verzeichnis.
- **Eine Messung ist abbrechbar.** Playwright kennt keinen `CancellationToken`, deshalb schließt
  der Abbruch den Browser-Kontext unter der laufenden Navigation, und die entstehende Exception
  wird in eine `OperationCanceledException` übersetzt.

## Oberfläche

- Die Hülle ist ein tiefblauer `Border.hero-strip` (`#294d73`) über die volle Breite, der
  Wortmarke, Eyebrow-Zeile und Navigations-Pills trägt, darüber eine Seitenfläche auf `#f2f0ec`,
  deren Inhalt in weißen `Border.card`-Feldern gruppiert ist. Die Seiten haben keinen eigenen
  Titel: Das aktive Navigations-Pill benennt die Seite, Überschriften stehen in den Karten.
  Es gibt keine Seitenleiste.
- **Farbrollen:** Grün ist Struktur (Umrisse, Überschriften, Flächen), Blau `#5d84b6` ist
  Aktion. Der einfache `Button` ist ein grüner Pill-Umriss in Unbounded, die Hauptaktion einer
  Seite trägt `Classes="accent"` (gefüllt blau), `Button.small` ist die kompakte Variante,
  `Button.nav` das Pill im Kopfstreifen. `SystemAccentColor` ist entsprechend blau.
- **Bewertungspunkte** an den Messwerten folgen den veröffentlichten Schwellenwerten von web.dev
  (`Services/CoreWebVitals.cs`). Metriken ohne veröffentlichten Schwellenwert bleiben unbewertet,
  statt eine erfundene Grenze zu bekommen. Die drei Bewertungsfarben (`BrandRatingGoodBrush`,
  `…WarnBrush`, `…PoorBrush`) sind semantisch und von den Markenrollen getrennt.
- Die Markenpalette, die Stilklassen (`Border.card`, `Border.hero-strip`, `Button.accent`,
  `Button.nav`, `Button.small`, `Button.link`, `TextBlock.eyebrow`, `.section-title`,
  `.field-label`, `.hint`) und die Schriften liegen in `App.axaml`. Views verweisen auf diese
  Ressourcen statt auf fest eingetragene Farben.
- Die Schriften Mulish (Text) und Unbounded (`.display`, Wortmarke, Schaltflächen,
  Überschriften) sind unter `Assets/Fonts/` eingebettet, je eine variable TTF pro Familie, unter
  SIL OFL 1.1. Die Lizenztexte liegen neben den TTFs und müssen dort bleiben.
- `RequestedThemeVariant="Light"` ist fest: Die Palette ist ein Markenschema, kein Systemthema.
- `MainWindowViewModel` ist nur die Hülle (Navigation und Besitz der Seiten). Die Seiten sind
  `MeasureViewModel`, `HistoryViewModel` und `SettingsViewModel`; der `ViewLocator` löst sie zu
  Views auf.

## Tests

- `dotnet test --filter "Category!=Integration"` ist die schnelle Suite: kein Netz, keine
  Browser, derzeit 108 Tests. Alles, was einen Browser oder das Netz braucht, trägt
  `[Trait("Category", "Integration")]`.
- Die schnelle Suite zusätzlich unter `LC_ALL=en_US.UTF-8` laufen lassen. Ein Test, der
  unbemerkt von der Sprache des Rechners abhing, war auf einem deutschen System grün und
  überall sonst rot. Die CI führt beides aus.
- Die CI (`.github/workflows/ci.yml`) baut unter Linux, Windows und macOS mit `-warnaserror`,
  prüft den dokumentierten Single-File-Publish für alle drei RIDs und führt die
  Integrationstests nächtlich aus.

## Lokalisierung

- Die Oberfläche ist deutsch und englisch, zur Laufzeit umschaltbar; die Vorgabe folgt der
  Systemsprache. `Services/Localization.cs` hält jeden Text für Benutzer als `T(de, en)`
  Eigenschaft am `Strings`-Singleton; Views binden über die `S`-Eigenschaft der ViewModels.
- Ein Sprachwechsel löst `PropertyChanged` mit leerem Namen aus, was Avalonia als „alle
  Eigenschaften geändert“ behandelt. Was nicht direkt an `Strings` gebunden ist (Statustext der
  Seite, Navigationsbeschriftungen, Titel des ScottPlot-Diagramms, das macOS-Programmmenü),
  abonniert dieses Ereignis und zeichnet neu.
- Statusmeldungen liegen als `Func<Strings, string>` vor, nicht als fertige Zeichenkette, damit
  auch die Meldung auf dem Schirm die Sprache wechselt.
- **Bewusst nicht lokalisiert** ist alles, was nach SQLite geschrieben wird
  (`BrowserResult.SkipReason`, `PerformanceMetric.Note`). Es wird wortgetreu gespeichert; eine
  Übersetzung würde die Sprache eines Laufs davon abhängig machen, wann er aufgenommen wurde.
