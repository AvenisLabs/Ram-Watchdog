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
