# KiwiTraffic

A small always-on Windows desktop widget that shows how much of the current
billing-cycle traffic your BandwagonHost (KiwiVM) VPS has used — without
opening the control panel.

> **Status: usable, but unfinished.** It reads real data from the KiwiVM API:
> set up your VEID and API key and it shows the current cycle's usage. What is
> still missing is the widget chrome — tray icon, always-on-top, remembered
> position (M3) and automatic refresh (M4) — so for now you refresh by hand.
>
> One caveat before trusting a number: the official API documentation sits
> behind a panel login, so the data contract here was reconstructed from public
> sources and has not been checked against a live account yet.
>
> The one detail still unverified is `monthly_data_multiplier`, a
> per-datacenter quota coefficient. It only affects the percentage when that
> coefficient is **not 1** — for example a VPS migrated to a CN2 GT location,
> where the quota shrinks to a third. In an ordinary location it is 1 and the
> percentage is simply used ÷ quota.
> Details: [`docs/api-contract.md`](docs/api-contract.md).
> Current state: [PROGRESS.md](PROGRESS.md).

## Features (v1 scope)

- Large readout of the used percentage for the current cycle, plus used /
  quota / remaining traffic and the next reset time.
- Always-on-top floating widget and a system tray icon; drag to move, position
  remembered in a DPI-aware way.
- Automatic and manual refresh, with the time of the last successful update
  shown relative to now.
- Threshold alerts at 80 % / 90 % / 95 %, de-duplicated per account and cycle.
- Explicit states for offline / rate limited / invalid key / stale data —
  an error is never rendered as 0 % usage.
- API key entered locally and encrypted with Windows DPAPI (CurrentUser scope);
  never written to disk in plaintext, never logged.

## Not in v1

VPS power / reinstall / snapshot / migrate operations, multi-VPS aggregation,
account sync, mobile app, web dashboard, real-time network speed, CPU / memory /
SSH monitoring, history charts, usage forecasting, auto-updater, installers,
commercial code signing.

## Requirements

- Windows 10 or 11 (x64)
- [.NET SDK 10.0.4xx](https://dotnet.microsoft.com/download/dotnet/10.0) to build
  (pinned in `global.json`)
- PowerShell 7 (`pwsh`) to run the build script
- Node.js is **optional** — `package.json` only wraps the build commands

No .NET runtime is needed on the target machine: the published artifact is a
self-contained single-file EXE.

## Build

```powershell
# Quick build for daily verification -> dist/fast/KiwiTraffic.exe
npm run build:exe

# Full quality gates + release build -> dist/release/KiwiTraffic.exe
npm run build:release

# Tests only
dotnet test
```

Without Node.js, call the script directly:

```powershell
pwsh -File scripts/build.ps1 -Configuration fast
pwsh -File scripts/build.ps1 -Configuration release
```

See [BUILD.md](BUILD.md) for details.

## Usage

1. Run `KiwiTraffic.exe`. It has no installer — put it wherever you like.
2. On first launch it asks for a VPS alias (optional), the VEID and the API
   key. Both are found in your KiwiVM control panel under *API*.
3. The window shows the current cycle. Use *Refresh* to fetch again, or
   *Settings…* to change the configuration.

   Not there yet: the window does not stay on top, does not remember its
   position, has no tray icon, and does not refresh by itself. Closing it quits
   the application. Those arrive with M3 and M4 — see
   [PROGRESS.md](PROGRESS.md).

## Data & privacy

Everything runs locally. There is no telemetry, no cloud sync and no network
traffic other than the queries to the KiwiVM API.

Data lives in `%LOCALAPPDATA%\KiwiTraffic\`:

| File | Contents |
| --- | --- |
| `settings.json` | alias, VEID, proxy mode, refresh interval, window position, alert settings |
| `credentials.dat` | API key (and proxy password, if any), encrypted with DPAPI |
| `cache.json` | last valid normalized snapshot |
| `notifications.json` | confirmed cycle and already-fired thresholds |
| `logs/` | size-capped, redacted diagnostic log with rotation |

To remove all local data, quit the widget, delete that directory, and disable
*Start with Windows* in the settings first (so no stale startup entry remains).

The API key is encrypted with DPAPI `CurrentUser` scope: it cannot be decrypted
after moving the EXE to another machine or switching Windows user. The widget
will ask for the key again in that case.

## License

No license has been chosen yet.
