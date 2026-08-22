# WorkStationX

A Windows desktop app that puts three things on one panel: a **workspace launcher**,
a **task timer that measures how wrong your estimates were**, and a set of **screen tools**.

Opening the eight apps and twelve tabs that make up "design work" costs minutes every time
you switch context. Most time trackers tell you how long something took, but never that you
guessed 30 minutes and it took 50. WorkStationX does both — one click restores a working
context, and the Time Bank keeps a running score of your estimating.

> **Status:** early. The shell, data layer and theming are built. Features land release
> by release — see the roadmap.

## Features

| Feature | What it does |
|---|---|
| **Workspaces** | One click opens every app and website for a context |
| **Profiles** | Websites open in the right Chrome profile, not whichever was last used |
| **Timer** | Counts down from your estimate, survives a restart or a sleep |
| **Extend** | Add more time when a task runs long, recorded as an overrun |
| **Time Bank** | Signed ledger — finishing early credits, running over debits |
| **Accuracy** | Shows how optimistic your estimates have been this week |
| **History** | Daily and weekly log of what you actually worked on |
| **Reminders** | Repeating hourly, daily or weekly toast notifications |
| **Pin** | Force any window to stay on top |
| **Colour** | Pick any pixel on screen, copies the hex |
| **Ruler** | Measure pixel distances on any monitor |
| **Capture** | Screenshot a region, window or screen, then annotate it |
| **Hotkeys** | Trigger any tool without leaving what you're doing |
| **Themes** | Swap the whole colour scheme, dark green by default |
| **Tray** | Keeps running in the background so reminders still fire |

## Built with

| Choice | Why |
|---|---|
| **C# / .NET 10** | Long-term support to 2028, deep Windows integration |
| **WPF** | Full control over a custom-drawn interface |
| **MVVM** | No business logic in the UI layer |
| **SQLite + EF Core** | Local file database, no server to install |
| **CommunityToolkit.Mvvm** | Removes MVVM boilerplate |
| **Microsoft.Extensions.Hosting** | Dependency injection and background services |
| **Serilog** | File logs, so a bug report is actually debuggable |
| **H.NotifyIcon** | System tray icon |
| **Archivo** | Embedded typeface, so nothing needs installing |
| **Win32 / P-Invoke** | Window pinning, pixel reading, global hotkeys |
| **GDI+** | Screen capture without the Windows capture border |

## Run it

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Windows 10 or 11.

```bash
git clone https://github.com/Muhit-1/WorkStationX-.git
```

```bash
dotnet run --project WorkStationX
```

Build and test:

```bash
dotnet build
```

```bash
dotnet test
```

Or open `WorkStationX.sln` in Visual Studio and press F5.

Your data lives in `%APPDATA%\WorkStationX` — database, settings and logs.

## Roadmap

| Release | Scope | Status |
|---|---|---|
| v0.1 | Shell, DI, database, logging, tray | Done |
| v0.2 | Workspace launcher | Done |
| v0.3 | Tasks, timer, Time Bank | Done |
| v0.4 | History and accuracy reporting | Done |
| v0.5 | Reminders | Done |
| v0.6 | Window pinner, colour picker | Done |
| v0.7 | Screen ruler | Done |
| v0.8 | Screenshot and annotation | Done |
| v0.9 | Settings, hotkeys, backup | Done |
| v1.0 | Installer | Done |

## Making an installer

One command builds both the self-contained app and the setup file:

```bash
powershell -ExecutionPolicy Bypass -File build-installer.ps1
```

Step 1 publishes to `publish\` and always works. Step 2 needs
[Inno Setup](https://jrsoftware.org/isdl.php); without it the script stops after step 1
and tells you so.

The result is `dist\WorkStationX-Setup-1.0.0.exe`, about 60-70 MB. It is
**self-contained**, so the machine you install it on does not need .NET.

Because the installer is not code-signed, Windows SmartScreen shows a blue
"Windows protected your PC" box on first run. Click **More info** then **Run anyway**.
A signing certificate costs roughly $200 a year and is the only way to remove it.

## Shortcuts

These work anywhere in Windows, even with WorkStationX in the tray. Change them in
Settings.

| Shortcut | Does |
|---|---|
| Ctrl + Shift + 2 | Capture a region |
| Ctrl + Shift + 3 | Capture a window |
| Ctrl + Shift + 4 | Pick a colour |
| Ctrl + Shift + 5 | Show or hide the ruler |
| Ctrl + Shift + W | Bring WorkStationX to the front |

## Licence

Not yet chosen.
