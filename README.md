# DXPeditions Tracker

A Windows desktop tool for chasing DXCC. For a chosen month, it pulls every announced DXpedition and special-event activation from five sources, cross-references the callsigns against your own ADIF log, and tells you which ones you actually still need — then hands you a ready-to-paste GridTracker 2 regex and a Ham Radio Deluxe DX Cluster Alarms checklist so you don't have to build either by hand.

Built by Jeremy S. Gaynor, K5JSG.

## Features

- **Five sources in one pass**: ng3k.com, dx-world.net, qsl.net/va3rj, ham365.net, and 425dxn.org. If one source is slow or unreachable, the others still come through — it's noted on the status line rather than failing the whole fetch.
- **"Needed" is computed from your own log**, not from what a DXpedition claims it will operate: an entity counts as needed if you have zero QSOs for it, zero *confirmed* QSOs (LoTW or a paper card only — no eQSL/QRZ/ClubLog/HRDLog), or you're missing a confirmed QSO on any of the 11 standard HF+6m bands or on CW/SSB/Digital.
- **Entities you've never worked before are highlighted** in the results grid.
- **Robust DXCC resolution**: falls back from an exact name match, to IOTA reference lookup, to matching the callsign's own prefix against the DXCC entity table — so an odd source-specific spelling (a sub-national descriptor, an abbreviation, a garbled title) doesn't get silently mislabeled as "never worked."
- **Workable-bands filter**: uncheck any band your station can't work and the GridTracker regex leaves out entities whose only unmet need is on a band you can't use, without touching the rest of the needed-analysis.
- **Fast on repeat runs**: dx-world.net's per-article fetch (normally the slow part) is skipped whenever a cheaper source already has the same callsign's dates, and whatever's still fetched is cached locally between runs. IOTA reference data is cached too and only re-checked about once a month.
- **One-click output**: a GridTracker 2 "Regex Limiter" string and an HRD DX Cluster Alarms checklist, each with its own Copy to Clipboard button.

## Installation

Download the latest installer from the [Releases](../../releases) page and run it. The app is self-contained — no separate .NET runtime install is required.

## Building from source

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download) (Windows, with the Windows Desktop workload) and, optionally, [Inno Setup](https://jrsoftware.org/isdl.php) if you want to build the installer.

```powershell
dotnet build DXPeditions.slnx
dotnet test DXPeditions.slnx
```

To produce a self-contained release build and installer:

```powershell
.\build.ps1 -Version <version>
```

This publishes a self-contained, single-file executable to `publish\` and, if Inno Setup is installed, builds the installer into `dist\`.

## Releasing

Bump `Version`/`FileVersion`/`AssemblyVersion` in `src\DXPeditions.App\DXPeditions.App.csproj`, commit, then push a version tag:

```powershell
git tag V1.1.0
git push origin master V1.1.0
```

Pushing a `V*.*.*` tag runs `.github\workflows\release.yml` on GitHub Actions, which builds the exe and installer itself (via `build.ps1`, same as building locally) and publishes the release.

## License

GNU General Public License v3.0 — see [License.txt](License.txt).
