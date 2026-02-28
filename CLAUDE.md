# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
cd RamWatchdog
dotnet build          # Build (expect 0 warnings, 0 errors — treat warnings as errors)
dotnet run            # Run the app (appears in system tray)
```

**Publish as single EXE:**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**If build warns about locked files:** the app is still running. Kill it first, then rebuild:
```bash
taskkill /IM RamWatchdog.exe /F
dotnet build
```

No external NuGet dependencies — uses only .NET 9 built-in libraries.

## Architecture

Six-file SRP design where each file has one responsibility:

- **Program.cs** — Entry point + single-instance mutex guard
- **Config.cs** — JSON persistence to `%APPDATA%\RamWatchdog\config.json` (ignored processes, mute state, threshold GB/%, display floor)
- **MemoryMonitor.cs** — Polling engine: enumerates processes every 2s via `System.Threading.Timer`, groups by name, sums `WorkingSet64`, publishes `OnSnapshot` event. Exposes GB convenience properties (`ThresholdGB`, `TotalPhysicalRamGB`, `DisplayFloorGB`, `DisplayFloorMB`) to avoid repeated byte-to-GB divisions.
- **MainForm.cs** — Core UI: system tray icon, dark-themed ListView (owner-drawn), alert system, button bar, report saving, Win32 dark title bar interop. Color/font constants exposed as `internal static` for use by Dialogs.
- **Dialogs.cs** — Static dialog builders: Settings (threshold/floor config), Manage Ignored, and Help. Called from MainForm button handlers. Contains `StyleDialogButton()` and `StyleTextBox()` helpers.
- **DarkControls.cs** — Dark-themed WinForms control classes: `DarkHeaderNativeWindow` (header gap painting), `DarkListView` (double-buffered), `DarkMenuRenderer`, `DarkColorTable`.

**Data flow:** `MemoryMonitor` fires `OnSnapshot` event → `MainForm` subscribes, marshals to UI thread via `BeginInvoke`, updates ListView and checks alert thresholds.

**Win32 P/Invoke usage:**
- `GlobalMemoryStatusEx` in MemoryMonitor for total RAM detection
- `DwmSetWindowAttribute` in MainForm for dark title bar
- `DarkHeaderNativeWindow` subclasses the native ListView header control via `NativeWindow` to intercept `WM_ERASEBKGND`/`WM_PAINT` (eliminates white gap artifacts)
- `DarkListView` sends `LVM_SETEXTENDEDLISTVIEWSTYLE` for double-buffering to prevent hover flicker

## Key Conventions

- **Version numbers** in each file header comment (e.g., `v1.8.1`) — increment on modification. Also update `<Version>` in `.csproj`.
- **Dark mode palette** defined as static `Color` fields at the top of `MainForm` — all UI controls reference these
- **Cached GDI objects**: Static `Font`, `SolidBrush`, `Pen`, and `StringFormat` fields are used in drawing hot paths (`DrawItem`, `DrawColumnHeader`) to avoid per-call allocations and GDI leaks. Variable-color brushes (per-row colors) still use `using var` since colors differ per item.
- **Button colors**: Each main button has a tinted bg/hover color pair reflecting its purpose (amber=mute, purple=ignore, blue=manage, teal=save, red=shutdown, etc.). Dialog buttons use neutral `StyleDialogButton()`.
- **Threshold logic**: min(16 GB, 30% of total RAM) as automatic default; user can override via GB or % in Settings
- **Display floor**: Configurable in Settings (default 0.5 GB / 500 MB) — only processes at or above this appear in the list
- **Alert cooldown**: 5 minutes per process before re-alerting
- **Auto-start**: Registry key at `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- **Reports saved to**: `Documents\RamWatchDog\Ram_Watchdog_<datetime>.md`
- **Named tray menu fields**: `_silenceMenuItem` and `_autoStartMenuItem` — never use magic indices to access tray menu items

## Gotchas

- `Timer` is ambiguous in WinForms projects — always use `System.Threading.Timer` (not `System.Windows.Forms.Timer`)
- WinForms owner-drawn ListView: `DrawSubItem` is unreliable during hover (only fires for the column under the mouse). All row drawing MUST happen in `DrawItem` to prevent columns vanishing on mouseover. `DrawSubItem` is intentionally a no-op.
- `DrawColumnHeader` only fires for actual columns, not the blank gap to the right — that's why `DarkHeaderNativeWindow` exists
- `Process.GetProcesses()` enumeration can throw if a process exits mid-read — always wrap in try/catch per process
- Close button hides to tray (not exit) — `ExitApp()` is the real shutdown path
- Never allocate `StringFormat` without `using` or caching — it implements `IDisposable` and leaks GDI handles if not disposed
- Use `StyleTextBox()` and `StyleDialogButton()` from `Dialogs.cs` for dialog controls (not `StyleButton()` which is MainForm-only and requires color params)
- `MemoryMonitor.OneGBd` is the canonical bytes-to-GB constant — use it instead of inline `1024L * 1024 * 1024` or `1024.0 * 1024.0 * 1024.0`
