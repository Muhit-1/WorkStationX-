# WorkStationX

A Windows desktop productivity app for developers and designers, combining a
**workspace launcher**, a **task timer with a Time Bank ledger**, and a set of
**screen tools** into one application.

> **Status:** v0.1 — foundation. The shell, data layer, DI, logging and
> single-instance handling are in place. Features land release by release
> (see the roadmap below).

## Why

Context switching is expensive. Opening the eight apps and twelve tabs that make up
"design work" takes minutes every time, and most time-tracking tools tell you how long
things took without ever telling you how wrong your estimate was. WorkStationX does both:
one click restores a working context, and the Time Bank keeps a signed ledger of your
estimation accuracy.

## Architecture

- **.NET 8 / WPF** with **MVVM** (`CommunityToolkit.Mvvm` source generators)
- **DI throughout** via `Microsoft.Extensions.Hosting`
- **SQLite + EF Core**, database at `%APPDATA%\WorkStationX\app.db`, migrated on startup
- **Serilog** rolling file logs at `%APPDATA%\WorkStationX\logs`
- **Per-Monitor DPI Aware v2**, required for correct pixel measurement on mixed-DPI setups
- No business logic in code-behind; navigation and dialogs go through services

```
WorkStationX/
├── Models/            entities
├── ViewModels/        one per page + ShellViewModel
├── Views/             XAML, selected by DataTemplate on view-model type
├── Services/          navigation, launcher, timer, Time Bank, reminders, capture
├── Infrastructure/    P/Invoke and DPI helpers, AppPaths
└── Data/              AppDbContext + Migrations
```

## Build

Requires the .NET SDK and Windows 10/11.

```bash
dotnet build
```

```bash
dotnet test
```

```bash
dotnet run --project WorkStationX
```

### Retargeting to .NET 10

The target framework is centralised in `Directory.Build.props`. Once the .NET 10 SDK is
installed, change `WsxTargetFramework` to `net10.0-windows10.0.19041.0`. The Windows SDK
suffix is needed for WinRT interop (toast notifications). Nothing else changes.

## Roadmap

| Release | Scope | Status |
|---|---|---|
| v0.1 | Shell, DI, EF Core, logging, single instance, tray | ✅ |
| v0.2 | Workspace Launcher + Chrome profile auto-discovery | |
| v0.3 | Tasks, countdown timer, Time Bank ledger | |
| v0.4 | History & estimation-accuracy reporting | |
| v0.5 | Reminders, toasts, run at startup | |
| v0.6 | Window pinner + colour picker | |
| v0.7 | Screen ruler | |
| v0.8 | Screenshot + annotation | |
| v0.9 | Settings, global hotkeys, export/backup | |
| v1.0 | Tests, installer, auto-update | |

## Design notes

**Time Bank is a signed ledger, not a running total.** Finishing early credits it;
overrunning debits it. A number that only ever increases is a scoreboard — a signed
balance is a measurable statement about how good your estimates are.

**Timer durability.** `Stopwatch` drives the UI tick (it does not drift), but persistence
uses a stored UTC start timestamp plus accumulated pause duration. `Stopwatch` does not
survive a process restart and may not advance through sleep.

**Screenshots use GDI+, not the Windows Graphics Capture API.** WGC is a video-capture
API; for still images it requires WinRT plumbing and draws a yellow capture border.

## Licence

Not yet chosen.
