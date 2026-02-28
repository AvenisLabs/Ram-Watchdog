# Summary Log

## 2026-02-28 00:05 — Refactor MainForm.cs: Extract DarkControls.cs and Dialogs.cs

**Task:** Reduce MainForm.cs from 1,148 lines to under 800 by extracting self-contained classes and dialog builders.

**New files created:**

`DarkControls.cs` (v1.0.0, ~170 lines):
- Moved `DarkHeaderNativeWindow` — native ListView header subclass for dark gap painting
- Moved `DarkListView` — double-buffered ListView with header subclassing
- Moved `DarkMenuRenderer` — custom dark context menu renderer
- Moved `DarkColorTable` — color table overrides for dark menus
- All four classes had zero coupling to MainForm instance state

`Dialogs.cs` (v1.0.0, ~250 lines):
- Moved `ShowManageIgnored()` — view/remove entries from ignore list
- Moved `ShowSettings()` — alert threshold (GB/%) and display floor configuration; returns bool so MainForm can refresh
- Moved `ShowHelp()` — help popup with yellow text
- Moved `StyleDialogButton()`, `StyleTextBox()`, `StyleDialog()` helpers
- References MainForm color/font constants via `internal static` fields

**Changes to existing files:**

`MainForm.cs` (v1.8.1 → v1.9.0, 1,148 → ~640 lines):
- Removed all four dark control classes (now in DarkControls.cs)
- Removed three dialog methods and three style helpers (now in Dialogs.cs)
- Changed 10 color/font fields from `private` to `internal` for Dialogs access: BgDark, BgPanel, FgBright, AccentGreen, HelpYellow, TextBoxBg, BorderColor, FontNormal, FontSmall, FontSemibold
- Changed `EnableDarkTitleBar()` from `private` to `internal static`
- Button handlers now delegate: `Dialogs.ShowManageIgnored(this, _config)`, `Dialogs.ShowSettings(this, _config, _monitor)`, `Dialogs.ShowHelp(this, _monitor)`

`RamWatchdog.csproj`: Version 1.8.0 → 1.9.0
`CLAUDE.md`: Updated architecture section (4-file → 6-file), removed stale "consider extracting" gotcha

**Build:** 0 warnings, 0 errors.

## 2026-02-28 00:33 — Custom Toast Popup + Tray Icon Flash (updated 00:36)

**Task:** Replace unreliable Windows balloon tips with a custom toast notification and add tray icon color flash for active alerts.

**New files created:**

`ToastNotification.cs` (v1.0.0, ~110 lines):
- Borderless dark-themed TopMost form positioned at bottom-right of primary screen
- Shows process name, memory usage, and instance count with red accent bar
- Click to open main window and dismiss; auto-dismisses after 5 seconds
- Static `ShowToast()` method; replaces existing toast if one is already visible (no stacking)
- Uses `System.Windows.Forms.Timer` for auto-dismiss (UI-thread safe)

`AlertIcon.cs` (v1.0.0, ~25 lines):
- Static `CreateAlertIcon()` generates a red "R" 16x16 icon in memory
- Mirrors the green app icon style but signals active alert state
- Called once at startup, cached in MainForm

**Changes to existing files:**

`MainForm.cs` (v1.9.1 → v1.10.0):
- Added `_alertIcon` field initialized via `AlertIcon.CreateAlertIcon()` in constructor
- Changed `AlertRed` from `private` to `internal` (needed by ToastNotification and AlertIcon)
- Replaced `FireAlert()` balloon tip + SystemSounds with `ToastNotification.ShowToast()`
- Added tray icon flash logic at end of `UpdateListView()`: icon turns red when any non-ignored process exceeds threshold, returns to green when clear
- Removed `using System.Media` import (no longer needed)

`RamWatchdog.csproj`: Version 1.9.0 → 1.10.0
`CLAUDE.md`: Updated architecture section (6-file → 8-file), added ToastNotification.cs and AlertIcon.cs descriptions

**Update (00:36):** Changed toast auto-dismiss from 5s to 10s. Re-alerting every 5 minutes was already handled by the existing `AlertCooldown` logic in `CheckAlerts` — toast reappears every 5 minutes until the process is silenced or ignored.

**Update (00:38):** Added "Enable toast notifications" setting to disable notifications generally.
- `Config.cs` (v1.8.0 → v1.8.1): Added `NotificationsEnabled` property (default `true`)
- `Dialogs.cs` (v1.0.0 → v1.1.0): Added "Notifications" section with checkbox in Settings dialog; dialog height 340 → 400; Reset All restores to enabled
- `MainForm.cs`: `OnMonitorSnapshot` now checks `_config.NotificationsEnabled` before calling `CheckAlerts`; tray icon flash still works when notifications are disabled

**Update (00:41):** Added "Check for updates on GitHub" link in Settings → Updates section.
- `Dialogs.cs` (v1.1.0 → v1.2.0): Added "Updates" section with LinkLabel pointing to `https://github.com/AvenisLabs/Ram-Watchdog/releases`; opens in default browser; dialog height 400 → 460
- Published single EXE, committed, pushed, and created GitHub release v1.10.0

**Build:** 0 warnings, 0 errors.

## 2026-02-28 00:55 — Security & Code Quality Review

**Task:** Comprehensive security audit and code quality review of all 8 source files.

**Output:** `docs/security_review_2026-02-28_00-55-33.md`

**Findings Summary:**
- **0 Critical, 2 High, 8 Medium, 9 Low, 3 Info** across security, resource leaks, threading, error handling, and code quality
- **Top 3 actionable issues:**
  1. RACE-01: `_lastAlertTimes` Dictionary accessed from both timer and UI threads without synchronization — use `ConcurrentDictionary` or move `CheckAlerts` into `BeginInvoke`
  2. LEAK-01: `GetHicon()` HICON handles never freed with `DestroyIcon` in `LoadAppIcon()` and `CreateAlertIcon()` — bounded leak (2 handles at startup)
  3. SEC-03: Non-atomic config file writes — crash mid-write could corrupt `config.json`
- No critical security vulnerabilities (no RCE, no injection vectors, no privilege escalation)
- P/Invoke usage is safe (standard system DLLs only, proper marshaling)
- Registry usage is HKCU-scoped with properly quoted paths

## 2026-02-28 00:58 — System RAM Usage Alert (v1.11.0)

**Task:** Add system-wide RAM usage percentage threshold as a separate alert from per-process thresholds.

**Changes:**

`MemoryMonitor.cs` (v1.3.0 → v1.4.0):
- Added `CurrentMemoryLoadPercent` property (uint, 0-100) updated each poll via `GlobalMemoryStatusEx.dwMemoryLoad`
- Queries system memory status at the start of each `BuildSnapshot()` call

`Config.cs` (v1.8.1 → v1.9.0):
- Added `SystemUsageThresholdPercent` property (int, default 90, 0 = disabled)

`ToastNotification.cs` (v1.0.1 → v1.1.0):
- Refactored to use generic `_title`/`_detail` strings instead of process-specific fields
- Added `ShowToast(string title, string detail, Action)` overload for system-wide alerts
- Per-process `ShowToast(MainForm, ProcessMemoryInfo, Action)` now delegates to the string overload

`MainForm.cs` (v1.10.0 → v1.11.0):
- Added `CheckSystemUsageAlert()` — fires toast when `CurrentMemoryLoadPercent >= SystemUsageThresholdPercent` with 5-min cooldown
- Tray icon flash now also turns red when system usage exceeds threshold
- Info label shows system threshold when enabled (e.g. "Sys: 90%")

`Dialogs.cs` (v1.2.0 → v1.3.0):
- Added "System RAM Usage Alert" section: checkbox + textbox (1-99%), hint text, enabled/disabled toggle
- Apply validates 1-99 range; Reset All restores to 90%
- Dialog height 460 → 540

`RamWatchdog.csproj`: Version 1.10.0 → 1.11.0

**Update (01:01):** Updated all documentation for system RAM usage alert feature.
- `README.md`: Updated description, features (added system RAM usage alert + updated tray icon flash), config table (added system usage threshold row), architecture table (updated descriptions for MemoryMonitor, MainForm, Dialogs, ToastNotification)
- `CLAUDE.md`: Updated Config, MemoryMonitor, MainForm, Dialogs, ToastNotification descriptions; updated P/Invoke section; added system usage threshold to Key Conventions
- `Dialogs.cs` Help text: Updated "WHAT IT DOES", "HOW IT WORKS" (system alert, tray icon), "BUTTONS" (Settings description), "COLOR CODING" (red tray icon), "CONFIG" (system usage threshold)

**Build:** 0 warnings, 0 errors.

## 2026-02-28 01:00 — Security Review Updated for v1.11.0

**Task:** Update `docs/security_review_2026-02-28_00-55-33.md` to cover v1.11.0 system-wide RAM alert changes.

**Changes to report:**
- Updated version header from v1.10.0 → v1.11.0
- Updated summary table: Resource Leaks Medium 2→3, Threading Medium 2→3
- SEC-02: Added `SystemUsageThresholdPercent` to unvalidated config fields list
- LEAK-02: Now covers 2 font leaks in Settings (floor hint + system usage hint)
- RACE-01: Added `CheckSystemUsageAlert()` as second timer-thread access path to `_lastAlertTimes`
- Added RACE-03: New `_currentMemoryLoadPercent` cross-thread uint access without memory barrier
- Renumbered former RACE-03 → RACE-04 for `_latestSnapshot`
- Updated all line number references to match v1.11.0 source
- Added remediation priority entry for RACE-03
- Added v1.11.0 Change Impact appendix with file-by-file delta table
