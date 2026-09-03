# LumenHut

*[This page in English](README.en.md)*

Web-Performance über mehrere Browser-Engines messen, als Desktop-Anwendung. LumenHut lädt eine
URL in Chromium, Firefox und WebKit (über Playwright), liest die Werte aus den Performance-APIs
der Browser selbst (LCP, FCP, CLS, TTFB, INP, Ladezeit, DOMContentLoaded) und legt jeden Lauf in
einer lokalen SQLite-Datenbank ab. Was eine Engine nicht messen kann, erscheint als N/A — nie
erfunden, nie als 0.

![Die Messseite: Ziel-URL, Auswahl der Engines, drei Durchläufe, und je Engine eine Ergebniskarte mit Bewertungspunkt an jedem Messwert](docs/messung-de.webp)

Weiter unten auf derselben Seite das LCP-Vergleichsdiagramm und der Hinweis, wie die Zahlen zu
lesen sind:

![Die Ergebniskarten mit Median und Spanne je Messwert, darunter das LCP-Vergleichsdiagramm je Browser-Engine](docs/ergebnisse-de.webp)

## Was es ist und was nicht

Es ist **kein Lighthouse**: keine Note, keine Drosselung von CPU oder Netz, keine
Mobilgerät-Emulation. Ein Lauf ist ein einzelner Kaltstart in einem Desktop-Fenster von
1280×720. Die Zahlen sind deshalb **nicht** mit den Felddaten von PageSpeed Insights
vergleichbar, und einzelne Läufe schwanken deutlich — dafür gibt es die Option, drei oder fünf
Durchläufe zu messen und Median plus Spanne zu melden.

Bewertet wird gegen die veröffentlichten Schwellenwerte von web.dev, soweit es welche gibt
(LCP, FCP, CLS, INP, TTFB). Wo es keine gibt (Ladezeit, DOMContentLoaded), bleibt der Wert
unbewertet, statt eine erfundene Grenze zu bekommen.

Welche Metriken eine Engine liefert, ist **nicht fest verdrahtet**: LumenHut fragt zur Laufzeit
die Engine selbst (`PerformanceObserver.supportedEntryTypes`) und meldet, was nicht kommt, als
N/A mit Begründung. Die Matrix verschiebt sich damit mit den Browser-Versionen — mit Firefox 151
und WebKit 26.5 liefern alle drei Engines LCP, CLS kommt weiterhin nur von Chromium. INP stammt
aus einem automatisierten Tastendruck statt aus einer echten Interaktion und sagt das immer dazu.

## Die drei Seiten

- **Messung** — Ziel-URL, Auswahl der Engines, Anzahl der Durchläufe (1, 3 oder 5), die Messwerte
  je Engine mit Bewertungspunkt und Erklärung pro Wert, ein LCP-Vergleichsdiagramm sowie Export
  als JSON, Markdown oder CSV und in die Zwischenablage. Eine laufende Messung lässt sich
  abbrechen; Ergebnisse erscheinen je Engine, sobald sie fertig sind.
- **Verlauf** — die letzten 20 Läufe aus der Datenbank. Ein Lauf lässt sich öffnen, zwei
  ausgewählte Läufe Wert für Wert vergleichen (älterer gegen neueren, eine negative Differenz
  heißt also: die Seite ist schneller geworden), ein Lauf mit einer Notiz versehen, einzelne
  Läufe oder der gesamte Verlauf löschen.
- **Einstellungen** — Proxy, Sprache der Oberfläche, URL-Speicherung, Aufbewahrungsdauer und der
  Ort der lokalen Daten.

Tastatur: `Enter` im URL-Feld startet eine Messung, `Strg/Cmd+R` messen, `Strg/Cmd+E` Markdown
exportieren, `Strg/Cmd+1..3` zwischen den Seiten wechseln.

Die Oberfläche ist deutsch und englisch, zur Laufzeit in den Einstellungen umschaltbar; die
Startsprache folgt der Systemsprache.

## Voraussetzungen

- .NET 10 SDK
- Netzwerkzugriff beim ersten Start: Playwright lädt die Browser-Engines in den Benutzer-Cache
  (mehrere hundert MB)

## Starten

```bash
git clone https://github.com/mensch-aufgabe-technik/LumenHut.git
cd LumenHut
dotnet run --project LumenHut
```

## Tests

```bash
dotnet test --filter "Category!=Integration"   # schnell: ohne Netz, ohne Browser
dotnet test                                    # alles, inklusive echter Browser-Läufe
```

Die Integrationstests starten echte Browser und laden eine öffentliche Seite. Sie brauchen also
installierte Playwright-Browser und Netzwerkzugriff.

## Proxy

Die optionalen Proxy-Einstellungen — Adresse (etwa `http://proxy.local:3128`,
`socks5://host:1080`), Benutzername und Passwort — gelten sowohl für die gemessenen Seitenaufrufe
als auch für den Browser-Download beim ersten Start. Die Werte greifen ab dem nächsten Lauf;
**Speichern** behält Adresse und Benutzername für den nächsten Programmstart.

**Das Passwort wird nie auf die Festplatte geschrieben.** Es gilt nur für die laufende Sitzung
und ist nach einem Neustart erneut einzugeben. Die Alternative wäre ein Klartext-Zugangsdatum in
`settings.json`, das jedes Backup- und Sync-Programm mitnimmt. Eine Einstellungsdatei aus einer
früheren Version, die noch `benutzer:passwort@host` enthält, wird beim nächsten Start
aufgeteilt: der Benutzername bleibt, das Passwort wird entfernt.

Bleibt das Feld leer, gelten die Vorgaben der Browser: Chromium, Firefox und WebKit (macOS)
folgen den System-Proxy-Einstellungen — der Browser-Download aber nicht. Hinter einem
Unternehmens-Proxy muss die Adresse deshalb **vor** dem ersten Start eingetragen werden.

## Für die Verteilung bauen

Die Befehle für den Single-File-Publish je Plattform stehen in [docs/TECHNIK.md](docs/TECHNIK.md).
Es gibt keine signierten Installationspakete: Windows SmartScreen und macOS Gatekeeper warnen
beim ersten Start vor einer selbst gebauten Programmdatei.

## Daten und Datenschutz

Die Ergebnisse liegen in `LumenHut/perfdata.db`, die Einstellungen in `LumenHut/settings.json`,
beides im lokalen Anwendungsdatenordner des Benutzers (macOS:
`~/Library/Application Support/LumenHut/`, Windows: `%LocalAppData%\LumenHut\`). Der Ordner ist
auf den aktuellen Benutzer beschränkt (Modus 0700 unter macOS und Linux; unter Windows erledigt
das die ACL von LocalAppData).

**Je Lauf gespeichert werden:** die gemessene URL, der Zeitstempel, die Messwerte, die
Fehlermeldungen der Engines und die Bedingungen des Laufs (Engine-Version, Betriebssystem,
Fenstergröße und ob ein Proxy verwendet wurde — nie die Proxy-Adresse). Keine Telemetrie, es
wird nichts irgendwohin geschickt; die Daten verlassen das Gerät nur durch einen ausdrücklichen
Export.

**Protokoll:** `lumenhut.log` im selben Ordner, höchstens 1 MB mit einer Rotation
(`lumenhut.log.1`). Es hält Programmstart, Läufe, übersprungene Engines, Exit-Codes des
Installers und Schema-Aktualisierungen fest — bewusst ohne Proxy-Zugangsdaten (an der Quelle
maskiert) und ohne Query-Strings (URLs werden vor dem Protokollieren reduziert). Zum Entfernen
die Dateien löschen. Das SQL-Logging von EF Core bleibt aus, weil dessen Parameter die URLs
enthalten würden.

**Die URL wird vor dem Speichern reduziert:** Zugangsdaten (`https://benutzer:passwort@host/`)
immer, der Query-String ebenfalls — es sei denn, „Vollständige URL speichern“ ist in den
Einstellungen aktiv. Query-Strings tragen regelmäßig Sitzungs-Tokens, Passwort-Reset-Tokens,
Einladungs-Tokens und Mailadressen, und die Datenbank ist nicht verschlüsselt.

**Aufbewahrung:** Die Verlaufsseite löscht einen ausgewählten Lauf oder den gesamten Verlauf,
und die Einstellungen bieten eine Aufbewahrungsdauer (Vorgabe: alles behalten, damit nichts
unbemerkt verschwindet), die beim Start angewendet wird. Nach dem Löschen läuft `VACUUM`, weil
SQLite die freigewordenen Seiten — samt URL — sonst in der Rohdatei lesbar liegen lässt.

**Verbindungen nach außen:** Beim ersten Start lädt Playwright die Browser-Engines von
Microsoft-CDNs (`cdn.playwright.dev`, `playwright.download.prss.microsoft.com`). Jede Messung
lädt die Zielseite in bis zu drei Engines, samt aller Drittanbieter-Anfragen, die diese Seite
stellt — die Zielseite und die von ihr eingebundenen Dritten sehen also die IP-Adresse des
messenden Rechners. Jeder Lauf verwendet einen frischen, nicht persistenten Browser-Kontext:
Cookies und Speicher der gemessenen Seite bleiben nicht zurück.

**Empfohlene Regel für Organisationen:** nur abgemeldete URLs oder Testkonten messen. Wer eine
angemeldete Ansicht misst, lädt echte Daten in den Browser und schreibt deren URL und
Fehlermeldungen in die lokale Datenbank.

Fehlermeldungen der Engines werden auf ihre erste Zeile gekürzt und von Proxy-Zugangsdaten
befreit, bevor sie gespeichert oder exportiert werden. Sie können weiterhin interne Hostnamen,
Ports und Pfade enthalten — ein Blick darauf lohnt, bevor ein Export weitergegeben wird.

Sicherheitsrelevante Funde bitte **nicht** als öffentliches Issue, sondern wie in
[SECURITY.md](SECURITY.md) beschrieben melden.

## Abhängigkeiten

| Paket | Zweck | Lizenz |
|---|---|---|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | Plattformübergreifende Oberfläche | MIT |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MVVM-Grundtypen | MIT |
| [Microsoft.Playwright](https://github.com/microsoft/playwright-dotnet) | Steuert die Browser-Engines | MIT |
| [EntityFrameworkCore.Sqlite](https://github.com/dotnet/efcore) | Lokale Datenbank und Migrationen | MIT |
| [ScottPlot](https://github.com/ScottPlot/ScottPlot) | LCP-Vergleichsdiagramm | MIT |
| [SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw) | Native SQLite-Bibliothek | Apache-2.0 |

Die vollständige Liste, samt der transitiven Bestandteile eines verteilten Builds und der
Lizenzen der Browser-Engines, steht in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Status und Support

Dieses Werkzeug ist intern bei der [Managentis GmbH](https://managentis.com) entstanden und wird
hier als Quellcode bereitgestellt, weil es anderen nützlich sein kann.

**Es gibt keinen Support, keine Roadmap und keine Gewährleistung.** Issues und Pull Requests
werden gelesen, aber es wird weder eine Reaktion noch eine Bearbeitung zugesagt. Wer das
Werkzeug produktiv einsetzen will, sollte damit rechnen, es selbst zu pflegen — dafür ist es
MIT-lizenziert.

## Lizenz

[MIT](LICENSE) · © 2026 Managentis GmbH

Die mitgelieferten Hausschriften Mulish und Unbounded stehen unter der SIL Open Font License
1.1; die Lizenztexte liegen neben den Schriftdateien in `LumenHut/Assets/Fonts/`.

Entstanden im Bereich **Mensch Aufgabe Technik** der Managentis GmbH —
[managentis.com/menschaufgabetechnik](https://managentis.com/menschaufgabetechnik/)
