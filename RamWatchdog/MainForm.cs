// MainForm.cs — System tray icon, alert display, and process list GUI v1.13.0
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace RamWatchdog;

public sealed class MainForm : Form
{
    private static readonly string AppVersion =
        typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "1.13.0";

    private static readonly TimeSpan AlertCooldown = TimeSpan.FromMinutes(5);

    // ── Dark mode palette (internal where needed by Dialogs) ──
    internal static readonly Color BgDark       = Color.FromArgb(18, 18, 18);
    internal static readonly Color BgPanel      = Color.FromArgb(28, 28, 28);
    internal static readonly Color FgBright     = Color.White;
    private  static readonly Color NeonGreen    = Color.FromArgb(57, 255, 20);
    internal static readonly Color AccentGreen  = Color.FromArgb(78, 201, 110);
    internal static readonly Color AlertRed     = Color.FromArgb(255, 80, 80);
    private  static readonly Color AlertRedBg   = Color.FromArgb(50, 15, 15);
    internal static readonly Color BorderColor  = Color.FromArgb(50, 50, 50);
    internal static readonly Color HelpYellow   = Color.FromArgb(255, 255, 50);
    internal static readonly Color TextBoxBg    = Color.FromArgb(35, 35, 35);

    // ── Cached fonts (internal where needed by Dialogs) ──
    internal static readonly Font FontNormal     = new("Segoe UI", 9f);
    internal static readonly Font FontSmall      = new("Segoe UI", 8.5f);
    internal static readonly Font FontSemibold   = new("Segoe UI Semibold", 9f);
    private  static readonly Font FontSemiboldSm = new("Segoe UI Semibold", 8.5f);

    // ── Cached GDI objects for owner-drawn hot paths (DrawItem/DrawColumnHeader) ──
    private static readonly SolidBrush BrushBgPanel = new(BgPanel);
    private static readonly SolidBrush BrushFgBright = new(FgBright);
    private static readonly Pen PenBorder = new(BorderColor);
    private static readonly StringFormat FmtLeft  = new() { LineAlignment = StringAlignment.Center };
    private static readonly StringFormat FmtRight = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

    // ── Per-button colors (dark-tinted to fit theme while showing intent) ──
    private static readonly Color BtnSilenceBg    = Color.FromArgb(92, 68, 0);      // amber — mute/unmute
    private static readonly Color BtnSilenceHover = Color.FromArgb(110, 82, 0);
    private static readonly Color BtnIgnoreBg     = Color.FromArgb(74, 53, 112);     // purple — dismiss
    private static readonly Color BtnIgnoreHover  = Color.FromArgb(90, 66, 133);
    private static readonly Color BtnManageBg     = Color.FromArgb(45, 68, 96);      // slate blue — list mgmt
    private static readonly Color BtnManageHover  = Color.FromArgb(58, 85, 117);
    private static readonly Color BtnSettingsBg   = Color.FromArgb(43, 95, 126);     // steel blue — config
    private static readonly Color BtnSettingsHover = Color.FromArgb(54, 112, 148);
    private static readonly Color BtnSaveBg       = Color.FromArgb(30, 110, 99);     // teal — export
    private static readonly Color BtnSaveHover    = Color.FromArgb(38, 128, 120);
    private static readonly Color BtnHelpBg       = Color.FromArgb(107, 91, 0);      // gold — info
    private static readonly Color BtnHelpHover    = Color.FromArgb(125, 108, 0);
    private static readonly Color BtnShutdownBg   = Color.FromArgb(110, 30, 30);     // red — exit
    private static readonly Color BtnShutdownHover = Color.FromArgb(131, 37, 37);

    // Registry key for auto-start
    private const string AutoStartKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AutoStartName = "RamWatchdog";

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("user32.dll")]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    private readonly Config _config;
    private readonly MemoryMonitor _monitor;
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _trayMenu;

    private readonly DarkListView _listView;
    private readonly Button _silenceButton;
    private readonly Button _ignoreButton;
    private readonly Button _manageIgnoredButton;
    private readonly Button _settingsButton;
    private readonly Button _saveButton;
    private readonly Button _helpButton;
    private readonly Button _shutdownButton;
    private readonly Label _infoLabel;
    private readonly ToolStripMenuItem _silenceMenuItem;
    private readonly ToolStripMenuItem _autoStartMenuItem;
    private readonly Icon _appIcon;
    private readonly Icon _alertIcon;

    private readonly Dictionary<string, DateTime> _lastAlertTimes = new(StringComparer.OrdinalIgnoreCase);
    private const string SystemUsageAlertKey = "__SYSTEM_USAGE__";
    private volatile List<ProcessMemoryInfo> _latestSnapshot = [];
    private volatile bool _exiting;

    public MainForm()
    {
        _config = Config.Load();
        _monitor = new MemoryMonitor();
        ApplyConfigSettings();

        // ── Window setup ──
        Text = $"RAM Watchdog v{AppVersion}";
        Size = new Size(640, 460);
        MinimumSize = new Size(580, 340);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = BgDark;
        ForeColor = FgBright;
        DoubleBuffered = true;
        _appIcon = LoadAppIcon();
        _alertIcon = AlertIcon.CreateAlertIcon();
        Icon = _appIcon;

        // ── Info label (threshold + floor + RAM) ──
        _infoLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0),
            BackColor = BgPanel,
            ForeColor = FgBright,
            Font = FontSmall
        };
        UpdateInfoLabel();
        Controls.Add(_infoLabel);

        // ── ListView ──
        _listView = new DarkListView(BgPanel)
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            MultiSelect = false,
            BackColor = BgDark,
            ForeColor = FgBright,
            BorderStyle = BorderStyle.None,
            Font = FontNormal,
            OwnerDraw = true,
            Scrollable = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable
        };
        _listView.Columns.Add("Process", 200);
        _listView.Columns.Add("Memory (GB)", 100, HorizontalAlignment.Right);
        _listView.Columns.Add("Instances", 70, HorizontalAlignment.Right);
        _listView.Columns.Add("Status", 120);

        _listView.DrawColumnHeader += ListView_DrawColumnHeader;
        _listView.DrawItem += ListView_DrawItem;
        _listView.DrawSubItem += ListView_DrawSubItem;
        Controls.Add(_listView);

        // ── Button panel — two rows ──
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 74,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4, 4, 4, 4),
            WrapContents = true,
            BackColor = BgPanel
        };

        _silenceButton = new Button
        {
            Text = _config.AlertsSilenced ? "Unmute Alerts" : "Silence Alerts",
            AutoSize = true, Height = 30
        };
        _silenceButton.Click += OnSilenceToggle;

        _ignoreButton = new Button { Text = "Ignore Selected", AutoSize = true, Height = 30 };
        _ignoreButton.Click += OnIgnoreSelected;

        _manageIgnoredButton = new Button { Text = "Manage Ignored", AutoSize = true, Height = 30 };
        _manageIgnoredButton.Click += OnManageIgnored;

        _settingsButton = new Button { Text = "Settings", AutoSize = true, Height = 30 };
        _settingsButton.Click += OnSettings;

        _saveButton = new Button { Text = "Save Report", AutoSize = true, Height = 30 };
        _saveButton.Click += OnSaveReport;

        _helpButton = new Button { Text = "?", Width = 30, Height = 30 };
        _helpButton.Click += OnShowHelp;

        _shutdownButton = new Button { Text = "Shutdown", AutoSize = true, Height = 30 };
        _shutdownButton.Click += (_, _) => ExitApp();

        // Apply base style then per-button colors
        StyleButton(_silenceButton,  BtnSilenceBg,  BtnSilenceHover);
        StyleButton(_ignoreButton,   BtnIgnoreBg,   BtnIgnoreHover);
        StyleButton(_manageIgnoredButton, BtnManageBg, BtnManageHover);
        StyleButton(_settingsButton, BtnSettingsBg, BtnSettingsHover);
        StyleButton(_saveButton,     BtnSaveBg,     BtnSaveHover);
        StyleButton(_helpButton,     BtnHelpBg,     BtnHelpHover);
        StyleButton(_shutdownButton, BtnShutdownBg, BtnShutdownHover);

        buttonPanel.Controls.AddRange([_silenceButton, _ignoreButton, _manageIgnoredButton,
            _settingsButton, _saveButton, _helpButton, _shutdownButton]);
        Controls.Add(buttonPanel);

        _listView.BringToFront();

        // ── Tray icon ──
        _trayMenu = new ContextMenuStrip { Renderer = new DarkMenuRenderer() };
        _trayMenu.Items.Add("Show", null, (_, _) => ShowWindow());
        _silenceMenuItem = new ToolStripMenuItem(
            _config.AlertsSilenced ? "Unmute Alerts" : "Silence Alerts",
            null, (_, _) => ToggleSilence());
        _trayMenu.Items.Add(_silenceMenuItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _autoStartMenuItem = new ToolStripMenuItem("Start with Windows", null, OnToggleAutoStart)
            { Checked = IsAutoStartEnabled() };
        _trayMenu.Items.Add(_autoStartMenuItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add("Exit", null, (_, _) => ExitApp());

        _trayIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "RAM Watchdog — Starting...",
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        _trayIcon.DoubleClick += (_, _) => ShowWindow();

        _monitor.OnSnapshot += OnMonitorSnapshot;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        EnableDarkTitleBar(Handle);
    }

    /// <summary>Applies immersive dark mode to a window title bar via DWM.</summary>
    internal static void EnableDarkTitleBar(IntPtr handle)
    {
        try
        {
            int value = 1;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }
        catch { }
    }

    // ── Dark mode helpers ──

    /// <summary>Styles a button with custom background/hover colors for the main button bar.</summary>
    private static void StyleButton(Button btn, Color bg, Color hover)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderColor = BorderColor;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.MouseOverBackColor = hover;
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(
            Math.Min(hover.R + 20, 255), Math.Min(hover.G + 20, 255), Math.Min(hover.B + 20, 255));
        btn.BackColor = bg;
        btn.ForeColor = FgBright;
        btn.Font = FontSmall;
        btn.Cursor = Cursors.Hand;
    }

    /// <summary>Applies threshold and display floor from config to the monitor.</summary>
    private void ApplyConfigSettings()
    {
        // Alert threshold: GB, %, or auto
        if (_config.ThresholdGB > 0)
            _monitor.SetThreshold((long)(_config.ThresholdGB * MemoryMonitor.OneGBd));
        else if (_config.ThresholdPercent > 0)
            _monitor.SetThreshold((long)(_monitor.TotalPhysicalRam * (_config.ThresholdPercent / 100.0)));
        else
            _monitor.SetThreshold(_monitor.DefaultThresholdBytes);

        // Display floor: custom GB or default 0.5 GB
        if (_config.DisplayFloorGB > 0)
            _monitor.SetDisplayFloor((long)(_config.DisplayFloorGB * MemoryMonitor.OneGBd));
        else
            _monitor.SetDisplayFloor(0); // resets to default 500 MB
    }

    private void UpdateInfoLabel()
    {
        string mode;
        if (_config.ThresholdGB > 0)
            mode = $"{_config.ThresholdGB:F1} GB";
        else if (_config.ThresholdPercent > 0)
            mode = $"{_config.ThresholdPercent:F0}%";
        else
            mode = "auto";
        string sysUsage = _config.SystemUsageThresholdPercent > 0
            ? $"  |  Sys: {_config.SystemUsageThresholdPercent}%"
            : "";
        _infoLabel.Text = $"Alert: {_monitor.ThresholdGB:F1} GB ({mode})  |  Show: >= {_monitor.DisplayFloorGB:G3} GB  |  RAM: {_monitor.TotalPhysicalRamGB:F1} GB{sysUsage}";
    }

    // ── ListView owner-draw ──

    private void ListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        e.Graphics.FillRectangle(BrushBgPanel, e.Bounds);
        e.Graphics.DrawLine(PenBorder, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

        var fmt = e.Header?.TextAlign == HorizontalAlignment.Right ? FmtRight : FmtLeft;
        var textRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
        e.Graphics.DrawString(e.Header?.Text, FontSemiboldSm, BrushFgBright, textRect, fmt);
    }

    /// <summary>
    /// Draws the ENTIRE row (all columns) here instead of relying on DrawSubItem.
    /// During hover/hot-track, WinForms only calls DrawSubItem for the column under
    /// the mouse — other columns get erased but not repainted, causing them to vanish.
    /// Drawing everything in DrawItem avoids this completely.
    /// </summary>
    private void ListView_DrawItem(object? sender, DrawListViewItemEventArgs e)
    {
        var bgColor = e.Item.BackColor == Color.Empty ? BgDark : e.Item.BackColor;
        var fgColor = e.Item.ForeColor == Color.Empty ? FgBright : e.Item.ForeColor;
        if (e.Item.Selected) { bgColor = Color.FromArgb(45, 45, 48); fgColor = Color.White; }

        // Fill entire row background first
        using var bg = new SolidBrush(bgColor);
        e.Graphics.FillRectangle(bg, e.Bounds);

        // Draw each column's text (using cached StringFormats to avoid GDI leaks)
        using var fg = new SolidBrush(fgColor);
        var font = e.Item.Font ?? _listView.Font;

        for (int i = 0; i < e.Item.SubItems.Count && i < _listView.Columns.Count; i++)
        {
            var col = _listView.Columns[i];
            var subItem = e.Item.SubItems[i];

            // Calculate the sub-item bounds from column positions
            int x = col.DisplayIndex == 0 ? e.Bounds.X : 0;
            for (int c = 0; c < _listView.Columns.Count; c++)
                if (_listView.Columns[c].DisplayIndex < col.DisplayIndex)
                    x += _listView.Columns[c].Width;
            if (col.DisplayIndex == 0) x = e.Bounds.X;

            var cellRect = new Rectangle(x + 4, e.Bounds.Y, col.Width - 8, e.Bounds.Height);
            var fmt = col.TextAlign == HorizontalAlignment.Right ? FmtRight : FmtLeft;
            e.Graphics.DrawString(subItem.Text, font, fg, cellRect, fmt);
        }
    }

    /// <summary>No-op — all drawing handled in DrawItem to prevent hover flicker.</summary>
    private void ListView_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e) { }

    /// <summary>
    /// Loads app.ico from the exe directory. Falls back to generating one in memory if missing.
    /// Used for both the window icon and tray icon.
    /// </summary>
    private static Icon LoadAppIcon()
    {
        // Look for app.ico next to the running executable
        string? exeDir = Path.GetDirectoryName(Environment.ProcessPath ?? Application.ExecutablePath);
        if (exeDir != null)
        {
            string icoPath = Path.Combine(exeDir, "app.ico");
            if (File.Exists(icoPath))
                return new Icon(icoPath);
        }

        // Fallback: generate in memory (same green "R" square)
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(AccentGreen);
        using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var brush = new SolidBrush(Color.White);
        g.DrawString("R", font, brush, 0, 0);
        IntPtr hIcon = bmp.GetHicon();
        using var tempIcon = Icon.FromHandle(hIcon);
        var result = (Icon)tempIcon.Clone();
        DestroyIcon(hIcon); // release the unmanaged HICON handle
        return result;
    }

    // ── Monitor event ──

    private void OnMonitorSnapshot(List<ProcessMemoryInfo> snapshot)
    {
        _latestSnapshot = snapshot;

        string tooltip = snapshot.Count > 0
            ? $"Top: {snapshot[0].Name} — {snapshot[0].MemoryGB:F1} GB"
            : $"No process >= {_monitor.DisplayFloorMB:F0} MB";
        if (tooltip.Length > 63) tooltip = tooltip[..63];

        if (!IsDisposed && IsHandleCreated)
        {
            // All _lastAlertTimes access must be on the UI thread to avoid race conditions
            try { BeginInvoke(() =>
            {
                if (_exiting) return; // guard against post-dispose access from in-flight timer callback
                if (_config.NotificationsEnabled && !_config.AlertsSilenced)
                {
                    CheckAlerts(snapshot);
                    CheckSystemUsageAlert();
                }
                PruneAlertTimes(snapshot);
                _trayIcon.Text = tooltip;
                UpdateListView(snapshot);
            }); }
            catch (ObjectDisposedException) { }
        }
    }

    private void CheckAlerts(List<ProcessMemoryInfo> snapshot)
    {
        var now = DateTime.UtcNow;
        foreach (var proc in snapshot)
        {
            if (!proc.ExceedsThreshold || _config.IsIgnored(proc.Name)) continue;
            if (_lastAlertTimes.TryGetValue(proc.Name, out var last) && (now - last) < AlertCooldown)
                continue;
            _lastAlertTimes[proc.Name] = now;
            FireAlert(proc);
        }
    }

    /// <summary>Removes expired cooldown entries for processes no longer in the snapshot.</summary>
    private void PruneAlertTimes(List<ProcessMemoryInfo> snapshot)
    {
        if (_lastAlertTimes.Count == 0) return;
        var now = DateTime.UtcNow;
        var activeNames = new HashSet<string>(snapshot.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        // Keep system usage key and any process still in the snapshot
        var stale = _lastAlertTimes.Keys
            .Where(k => k != SystemUsageAlertKey && !activeNames.Contains(k) && (now - _lastAlertTimes[k]) >= AlertCooldown)
            .ToList();
        foreach (var key in stale)
            _lastAlertTimes.Remove(key);
    }

    /// <summary>Checks if system-wide RAM usage exceeds the configured threshold. Must be called on UI thread.</summary>
    private void CheckSystemUsageAlert()
    {
        if (_config.SystemUsageThresholdPercent <= 0) return;

        uint currentLoad = _monitor.CurrentMemoryLoadPercent;
        if (currentLoad < _config.SystemUsageThresholdPercent) return;

        var now = DateTime.UtcNow;
        if (_lastAlertTimes.TryGetValue(SystemUsageAlertKey, out var last) && (now - last) < AlertCooldown)
            return;
        _lastAlertTimes[SystemUsageAlertKey] = now;

        ToastNotification.ShowToast(
            "RAM Watchdog — System RAM High",
            $"Total RAM usage: {currentLoad}%  (threshold: {_config.SystemUsageThresholdPercent}%)",
            ShowWindow);
    }

    /// <summary>Shows a toast alert for a process exceeding threshold. Must be called on UI thread.</summary>
    private void FireAlert(ProcessMemoryInfo proc)
    {
        ToastNotification.ShowToast(this, proc, ShowWindow);
    }

    private void UpdateListView(List<ProcessMemoryInfo> snapshot)
    {
        // Preserve selection across refresh so the user can click Ignore
        string? selectedName = _listView.SelectedItems.Count > 0
            ? _listView.SelectedItems[0].Text : null;

        _listView.BeginUpdate();
        _listView.Items.Clear();

        bool isFirst = true;
        foreach (var proc in snapshot)
        {
            bool isIgnored = _config.IsIgnored(proc.Name);
            string status = isIgnored ? "Ignored"
                : proc.ExceedsThreshold ? "OVER THRESHOLD" : "OK";

            var item = new ListViewItem(proc.Name);
            item.SubItems.Add(proc.MemoryGB.ToString("F2"));
            item.SubItems.Add(proc.ProcessCount.ToString());
            item.SubItems.Add(status);
            item.BackColor = BgDark;

            if (proc.ExceedsThreshold && !isIgnored)
            { item.BackColor = AlertRedBg; item.ForeColor = AlertRed; }
            else if (isIgnored)
            { item.ForeColor = Color.FromArgb(160, 160, 160); }
            else if (isFirst)
            { item.ForeColor = NeonGreen; }
            else
            { item.ForeColor = FgBright; }

            _listView.Items.Add(item);

            // Re-select the previously selected process by name
            if (selectedName != null && string.Equals(proc.Name, selectedName, StringComparison.OrdinalIgnoreCase))
                item.Selected = true;

            isFirst = false;
        }
        _listView.EndUpdate();

        // Flash tray icon red if any process exceeds threshold or system RAM usage is high
        bool anyOverThreshold = snapshot.Any(p => p.ExceedsThreshold && !_config.IsIgnored(p.Name));
        bool systemOverThreshold = _config.SystemUsageThresholdPercent > 0
            && _monitor.CurrentMemoryLoadPercent >= _config.SystemUsageThresholdPercent;
        _trayIcon.Icon = (anyOverThreshold || systemOverThreshold) ? _alertIcon : _appIcon;
    }

    // ── Button handlers ──

    private void OnSilenceToggle(object? sender, EventArgs e) => ToggleSilence();

    private void ToggleSilence()
    {
        _config.AlertsSilenced = !_config.AlertsSilenced;
        _config.Save();
        string label = _config.AlertsSilenced ? "Unmute Alerts" : "Silence Alerts";
        _silenceButton.Text = label;
        _silenceMenuItem.Text = label;
    }

    private void OnIgnoreSelected(object? sender, EventArgs e)
    {
        if (_listView.SelectedItems.Count == 0)
        { MessageBox.Show("Select a process from the list first.", "Ignore Process", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        string name = _listView.SelectedItems[0].Text;
        if (MessageBox.Show($"Ignore \"{name}\"? It will no longer trigger alerts.",
            "Ignore Process", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        { _config.AddIgnored(name); _lastAlertTimes.Remove(name); }
    }

    private void OnManageIgnored(object? sender, EventArgs e) =>
        Dialogs.ShowManageIgnored(this, _config);

    private void OnSettings(object? sender, EventArgs e)
    {
        if (Dialogs.ShowSettings(this, _config, _monitor))
        {
            ApplyConfigSettings();
            _lastAlertTimes.Clear();
            UpdateInfoLabel();
        }
    }

    /// <summary>Saves the current process list as a markdown file.</summary>
    private void OnSaveReport(object? sender, EventArgs e)
    {
        var snapshot = _latestSnapshot;
        if (snapshot.Count == 0)
        { MessageBox.Show("No processes to save.", "Save Report", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        // Default save folder: Documents\RamWatchDog
        string docsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string saveDir = Path.Combine(docsFolder, "RamWatchDog");
        Directory.CreateDirectory(saveDir);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"Ram_Watchdog_{timestamp}.md";
        string filePath = Path.Combine(saveDir, fileName);

        double thresholdGB = _monitor.ThresholdGB;
        double totalGB = _monitor.TotalPhysicalRamGB;

        // Build row data first so we can calculate column widths for alignment
        var headers = new[] { "Process", "Memory (GB)", "Instances", "Status" };
        var rows = new List<string[]>();

        foreach (var proc in snapshot)
        {
            string status = _config.IsIgnored(proc.Name) ? "Ignored"
                : proc.ExceedsThreshold ? "**OVER THRESHOLD**" : "OK";
            rows.Add([proc.Name, proc.MemoryGB.ToString("F2"), proc.ProcessCount.ToString(), status]);
        }

        double totalUsed = snapshot.Sum(p => p.MemoryGB);
        rows.Add(["**Total**", $"**{totalUsed:F2}**", "", ""]);

        // Calculate max width per column (account for bold markers in display width)
        int[] widths = new int[4];
        for (int c = 0; c < 4; c++)
            widths[c] = headers[c].Length;
        foreach (var row in rows)
            for (int c = 0; c < 4; c++)
            {
                // Strip ** for width calculation since they're invisible in rendered markdown
                int displayLen = row[c].Replace("**", "").Length;
                if (displayLen > widths[c]) widths[c] = displayLen;
            }

        // Build padded table
        string PadCell(string text, int col)
        {
            int displayLen = text.Replace("**", "").Length;
            int pad = widths[col] - displayLen;
            return text + new string(' ', Math.Max(0, pad));
        }

        string headerLine = "| " + string.Join(" | ", headers.Select((h, i) => h.PadRight(widths[i]))) + " |";
        string separatorLine = "| " + string.Join(" | ", widths.Select(w => new string('-', w))) + " |";

        var lines = new List<string>
        {
            "# RAM Watchdog Report",
            "",
            $"**Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"**System RAM:** {totalGB:F1} GB",
            $"**Alert Threshold:** {thresholdGB:F1} GB ({(_config.ThresholdGB > 0 ? $"{_config.ThresholdGB:F1} GB" : _config.ThresholdPercent > 0 ? $"{_config.ThresholdPercent:F0}%" : "auto")})",
            "",
            $"## Processes >= {_monitor.DisplayFloorMB:F0} MB",
            "",
            headerLine,
            separatorLine
        };

        foreach (var row in rows)
            lines.Add("| " + string.Join(" | ", row.Select((cell, i) => PadCell(cell, i))) + " |");

        if (_config.IgnoredProcesses.Count > 0)
        {
            lines.Add("");
            lines.Add("## Ignored Processes");
            lines.Add("");
            foreach (var name in _config.IgnoredProcesses)
                lines.Add($"- {name}");
        }

        File.WriteAllLines(filePath, lines);

        MessageBox.Show($"Report saved to:\n{filePath}", "Save Report",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnShowHelp(object? sender, EventArgs e) =>
        Dialogs.ShowHelp(this, _monitor);

    // ── Auto-start (registry HKCU\Run) ──

    private static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AutoStartKey, false);
            return key?.GetValue(AutoStartName) is not null;
        }
        catch { return false; }
    }

    private static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AutoStartKey, true);
            if (key is null) return;

            if (enable)
            {
                // Use the current executable path
                string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                key.SetValue(AutoStartName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AutoStartName, false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update auto-start:\n{ex.Message}", "Auto-Start",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnToggleAutoStart(object? sender, EventArgs e)
    {
        bool current = IsAutoStartEnabled();
        SetAutoStart(!current);
        _autoStartMenuItem.Checked = !current;
    }

    // ── Window behavior ──

    private void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized) Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        { e.Cancel = true; Hide(); return; }
        base.OnFormClosing(e);
    }

    private void ExitApp()
    {
        _exiting = true; // signal BeginInvoke lambdas to bail out before touching disposed resources
        _monitor.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _monitor.Dispose();
            // Guard against double-dispose (ExitApp may have already disposed the tray icon)
            try { _trayIcon.Visible = false; _trayIcon.Dispose(); }
            catch (ObjectDisposedException) { }
            _trayMenu.Dispose();
            _appIcon.Dispose();
            _alertIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
