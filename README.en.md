# LumenHut

*[Diese Seite auf Deutsch](README.md)*

Cross-browser web performance measurement as a desktop app. LumenHut loads a URL in Chromium, Firefox and WebKit (via Playwright), reads the metrics the browsers' own Performance APIs expose (LCP, FCP, CLS, TTFB, INP, LoadTime, DOMContentLoaded) and stores every run in a local SQLite database. Metrics an engine cannot measure are reported as N/A — never invented, never 0.

![The measurement page: target URL, engine selection, and one result card per engine with a rating dot on every metric](docs/messung-en.webp)

Further down the same page, the LCP comparison chart and the note on how to read the numbers:

![The result cards, the threshold legend, and the LCP comparison chart per browser engine](docs/ergebnisse-en.webp)

It is not Lighthouse: there is no score, no CPU or network throttling, and no mobile emulation. A run is a single cold load in a 1280x720 desktop window, so the numbers are not comparable to PageSpeed Insights field data, and single runs vary noticeably. Values are rated against the published web.dev thresholds where those exist (LCP, FCP, CLS, INP, TTFB) and left unrated where they do not (LoadTime, DOMContentLoaded).

Which metrics an engine reports is not hard-coded: LumenHut asks the engine at run time (`PerformanceObserver.supportedEntryTypes`) and reports what it does not get as N/A with a reason. That matrix therefore moves with the browser versions — as of Firefox 151 and WebKit 26.5, all three engines report LCP, while CLS still comes from Chromium alone. INP comes from an automated key press rather than a real interaction and always says so.

A header strip carries the navigation across three pages:

- **Measurement** — target URL, engine selection, number of passes (1, 3 or 5: several passes report the median and the range), the metrics per engine with a rating dot and a per-metric tooltip, an LCP comparison chart, and export as JSON, Markdown or CSV plus copy-to-clipboard. A running measurement can be cancelled; results appear per engine as they finish.
- **History** — the last 20 runs from the database; opening one shows it in the measurement page, two selected runs can be compared metric by metric (older against newer, so a negative difference means the page got faster), a note can be attached to a run, and single runs or the whole history can be deleted.
- **Settings** — proxy, interface language, URL storage, retention period and the local data location.

Keyboard: `Enter` in the URL field starts a measurement, `Ctrl/Cmd+R` runs, `Ctrl/Cmd+E` exports Markdown, `Ctrl/Cmd+1..3` switches pages.

The interface is available in German and English, switchable at runtime under Settings; the initial language follows the system UI language.

## Requirements

- .NET 10 SDK
- Network access on first run (Playwright downloads the browser engines to the user cache, several hundred MB)

## Run

```bash
git clone https://github.com/mensch-aufgabe-technik/LumenHut.git
cd LumenHut
dotnet run --project LumenHut
```

## Test

```bash
dotnet test --filter "Category!=Integration"   # fast: no network, no browsers
dotnet test                                    # everything, including real browser runs
```

The functional tests launch real browsers and load a public web page, so they need installed Playwright browsers and network access.

## Proxy

The optional proxy settings — address (e.g. `http://proxy.local:3128`, `socks5://host:1080`), user name and password — apply to both the measured page loads and the first-run browser download. The values take effect on the next run; press **Save** to keep address and user name for the next app start.

**The password is never written to disk.** It is kept for the running session only, so it has to be entered again after a restart; the alternative would be a plain-text credential in `settings.json` that every backup and sync client picks up. A settings file from an earlier version that still contains `user:pass@host` is split on the next start: the user name is kept, the password is removed.

Left empty, the browsers use their defaults: Chromium, Firefox and WebKit (macOS) follow the system proxy settings, but the browser download does not — behind a corporate proxy, set the address before the first run.

## Build for distribution

See [docs/TECHNIK.md](docs/TECHNIK.md) (German) for single-file publish commands per platform. No signed installers are provided: Windows SmartScreen and macOS Gatekeeper will warn about a self-built executable on first run.

## Data and privacy

Results are stored in `LumenHut/perfdata.db` and settings in `LumenHut/settings.json` inside the user's local application data folder (macOS: `~/Library/Application Support/LumenHut/`, Windows: `%LocalAppData%\LumenHut\`). The folder is restricted to the current user (mode 0700 on macOS and Linux; on Windows the LocalAppData ACL already does this).

**Stored per run:** the measured URL, the timestamp, the metrics, the engines' error messages, and the conditions of the run (engine version, operating system, viewport, and whether a proxy was used — never the proxy address). No telemetry, and nothing is sent anywhere; the data leaves the device only through an explicit export.

**Log:** `lumenhut.log` in the same folder, at most 1 MB with a single rotation (`lumenhut.log.1`). It records application start, runs, skipped engines, installer exit codes and schema upgrades — deliberately without proxy credentials (masked at the source) and without query strings (URLs are reduced before they are logged). Delete the files to remove it; EF Core's own SQL logging stays off, because its parameters would contain the URLs.

**The URL is reduced before it is stored:** credentials (`https://user:pass@host/`) are always removed, and the query string is dropped unless "Store the full URL" is enabled under Settings. Query strings routinely carry session tokens, password reset tokens, invitation tokens and mail addresses, and the database is not encrypted.

**Retention:** the history page deletes a selected run or the whole history, and Settings offers a retention period (default: keep everything, so nothing disappears unnoticed) that is applied at startup. Deleting runs `VACUUM` afterwards, because SQLite otherwise keeps the freed pages — including the URL — readable in the raw file.

**Outbound connections:** on the first run the Playwright browser engines are downloaded from Microsoft CDNs (`cdn.playwright.dev`, `playwright.download.prss.microsoft.com`). Every measurement loads the target page in up to three engines, including all third-party requests that page makes — the target site and its embedded third parties see the measuring machine's IP address. Each run uses a fresh, non-persistent browser context, so cookies and storage of the measured page are not kept.

**Recommended rule for organizations:** measure logged-out URLs or test accounts only. Measuring a logged-in view loads real data into the browser and puts its URL and error messages into the local database.

Error messages from the browser engines are shortened to their first line and stripped of proxy credentials before they are stored or exported. They can still contain internal host names, ports and paths — worth a look before passing an export on.

Security findings please **not** as a public issue, but as described in [SECURITY.md](SECURITY.md).

## Dependencies

| Package | Purpose | License |
|---|---|---|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | Cross-platform UI | MIT |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MVVM base types | MIT |
| [Microsoft.Playwright](https://github.com/microsoft/playwright-dotnet) | Drives the browser engines | MIT |
| [EntityFrameworkCore.Sqlite](https://github.com/dotnet/efcore) | Local database and migrations | MIT |
| [ScottPlot](https://github.com/ScottPlot/ScottPlot) | LCP comparison chart | MIT |
| [SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw) | SQLite native bundle | Apache-2.0 |

The full list, including the transitive components in a distributed build and the licenses of the browser engines, is in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Status and support

This tool was built inside [Managentis GmbH](https://managentis.com) and is published here as source because it can be useful to others.

**There is no support, no roadmap and no warranty.** Issues and pull requests are read, but neither a reply nor any work on them is promised. Anyone putting this into production should expect to maintain it themselves — that is what the MIT license is for.

## License

[MIT](LICENSE) · © 2026 Managentis GmbH

The bundled brand fonts Mulish and Unbounded are licensed under the SIL Open Font License 1.1; the license texts ship next to the font files in `LumenHut/Assets/Fonts/`.

Built in the **Mensch Aufgabe Technik** unit of Managentis GmbH —
[managentis.com/menschaufgabetechnik](https://managentis.com/menschaufgabetechnik/)
