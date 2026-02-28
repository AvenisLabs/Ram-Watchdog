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
