# RAM Watchdog

Lightweight system tray memory monitor for Windows. Polls all running processes every 2 seconds and alerts you when any single process (or group of same-named processes) uses too much RAM. Designed to catch memory leaks before they crash your system.

## Features

- **Real-time monitoring** — polls `Process.WorkingSet64` every 2 seconds, groups by process name
- **Configurable alerts** — threshold by fixed GB or percentage of total RAM, with 5-minute cooldown per process
- **Custom toast notifications** — dark-themed popup at bottom-right of screen; auto-dismisses after 10 seconds, click to open main window. Re-alerts every 5 minutes until silenced or ignored. Can be disabled in Settings.
- **Tray icon flash** — tray icon turns red when any process exceeds the threshold, returns to green when clear
- **Ignore list** — permanently skip known-heavy processes from triggering alerts
- **Display floor** — only show processes above a configurable minimum (default 500 MB)
- **Markdown reports** — export the current process list as a formatted `.md` file
- **Auto-start** — optional "Start with Windows" via registry (`HKCU\Run`)
- **Dark mode UI** — full dark theme including title bar, ListView, dialogs, and context menus
- **System tray** — lives in the notification area; close button hides to tray, double-click to restore
- **Check for updates** — link in Settings opens the [GitHub releases page](https://github.com/AvenisLabs/Ram-Watchdog/releases)

## Screenshot

The main window shows processes sorted by memory usage with color coding:
- **Green** — top memory consumer
- **Red** — process exceeding the alert threshold
- **Gray** — ignored process

## Requirements

- Windows 10/11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (for building from source)
- No external NuGet dependencies

## Build & Run

```bash
cd RamWatchdog
dotnet build
dotnet run
```

## Publish as Single EXE

```bash
cd RamWatchdog
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The output will be in `bin/Release/net9.0-windows/win-x64/publish/RamWatchdog.exe` — a single self-contained executable that requires no .NET runtime installed.

## Configuration

Settings are persisted to `%APPDATA%\RamWatchdog\config.json` and include:

| Setting | Default | Description |
|---------|---------|-------------|
| Alert threshold | min(16 GB, 30% of RAM) | Trigger point for alerts — configurable as fixed GB or % |
| Display floor | 0.5 GB (500 MB) | Minimum memory to show a process in the list |
| Ignored processes | (none) | Processes that never trigger alerts |
| Alerts silenced | false | Suppress all toast notifications (quick toggle) |
| Notifications enabled | true | Master toggle for toast notifications (in Settings) |

Reports are saved to `Documents\RamWatchDog\Ram_Watchdog_<datetime>.md`.

## Architecture

Eight-file SRP design:

| File | Responsibility |
|------|---------------|
| `Program.cs` | Entry point + single-instance mutex guard |
| `Config.cs` | JSON persistence to `%APPDATA%` |
| `MemoryMonitor.cs` | Polling engine, process enumeration, threshold logic |
| `MainForm.cs` | System tray, ListView, alerts, report saving, tray icon flash |
| `Dialogs.cs` | Settings, Help, and Manage Ignored dialogs |
| `DarkControls.cs` | Dark-themed ListView, header, and menu controls |
| `ToastNotification.cs` | Custom dark toast popup for RAM alerts |
| `AlertIcon.cs` | Red alert icon generator for tray icon flash |

## License

This project is provided as-is for personal use.
