# Third-party notices

LumenHut is distributed as a self-contained executable and therefore ships the following
components. MIT and Apache-2.0 both require the copyright notice to travel with binary
distributions, which is what this file is for. Licenses were read from the packages' own nuspec
metadata, not from memory.

## NuGet packages (direct references)

| Package | Version | License |
|---|---|---|
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter | 12.0.5 | MIT |
| CommunityToolkit.Mvvm | 8.4.1 | MIT |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.9 | MIT |
| Microsoft.Playwright | 1.61.0 | MIT |
| ScottPlot.Avalonia, ScottPlot | 5.1.59 | MIT |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.3 | Apache-2.0 |

`Microsoft.EntityFrameworkCore.Design` (10.0.9, MIT) and `AvaloniaUI.DiagnosticsSupport`
(2.2.3) are development-time only and are not part of a Release build.

## Transitive components in the bundle

| Component | Version | License |
|---|---|---|
| SkiaSharp and its native assets | 3.119.x | MIT |
| HarfBuzzSharp and its native assets | 8.3.1.x | MIT |
| SQLite (via SQLitePCLRaw's `e_sqlite3`) | bundled | Public domain |

## Browser engines

Chromium, Firefox and WebKit are **not** part of the executable. Playwright downloads them into
the user's cache on first run (`~/Library/Caches/ms-playwright`, `%LocalAppData%\ms-playwright`),
where they remain under their own licenses (BSD-3-Clause for Chromium, MPL-2.0 for Firefox,
LGPL/BSD for WebKit). Distributing LumenHut therefore does not distribute them.

## Fonts

Mulish and Unbounded are embedded under the SIL Open Font License 1.1. The full license texts
sit next to the font files in `LumenHut/Assets/Fonts/` and must stay there.

## LumenHut itself

LumenHut is licensed under the MIT license, © 2026 Managentis GmbH — see `LICENSE`.

To regenerate the package part of this list:

```bash
dotnet list package --include-transitive
```
