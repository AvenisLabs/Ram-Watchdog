// Dialogs.cs — Settings, Help, and Manage Ignored dialog builders v1.0.0
namespace RamWatchdog;

/// <summary>
/// Static factory methods for dark-themed dialogs used by MainForm.
/// Each method creates a modal dialog, shows it, and returns.
/// </summary>
internal static class Dialogs
{
    private static void StyleDialog(Form dialog)
    {
        dialog.BackColor = MainForm.BgDark;
        dialog.ForeColor = MainForm.FgBright;
    }

    /// <summary>Styles a dialog button with neutral dark colors.</summary>
    private static void StyleDialogButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderColor = MainForm.BorderColor;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(58, 58, 58);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 70, 70);
        btn.BackColor = Color.FromArgb(42, 42, 42);
        btn.ForeColor = MainForm.FgBright;
        btn.Font = MainForm.FontSmall;
        btn.Cursor = Cursors.Hand;
    }

    /// <summary>Applies dark theme styling to a TextBox.</summary>
    private static void StyleTextBox(TextBox tb)
    {
        tb.BackColor = MainForm.TextBoxBg;
        tb.ForeColor = MainForm.FgBright;
        tb.BorderStyle = BorderStyle.FixedSingle;
        tb.Font = MainForm.FontNormal;
    }

    /// <summary>Shows dialog to view and remove entries from the ignore list.</summary>
    internal static void ShowManageIgnored(Form owner, Config config)
    {
        if (config.IgnoredProcesses.Count == 0)
        { MessageBox.Show("No processes are currently ignored.", "Manage Ignored", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        using var dialog = new Form
        {
            Text = "Manage Ignored Processes", Size = new Size(320, 300),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false
        };
        StyleDialog(dialog);
        MainForm.EnableDarkTitleBar(dialog.Handle);

        var listBox = new ListBox
        { Dock = DockStyle.Fill, SelectionMode = SelectionMode.One, BackColor = MainForm.BgDark, ForeColor = MainForm.FgBright, BorderStyle = BorderStyle.None, Font = MainForm.FontNormal };
        foreach (var n in config.IgnoredProcesses) listBox.Items.Add(n);

        var removeBtn = new Button { Text = "Remove Selected", Dock = DockStyle.Bottom, Height = 35 };
        StyleDialogButton(removeBtn);
        removeBtn.Click += (_, _) => { if (listBox.SelectedItem is string s) { config.RemoveIgnored(s); listBox.Items.Remove(s); } };

        dialog.Controls.Add(listBox);
        dialog.Controls.Add(removeBtn);
        dialog.ShowDialog(owner);
    }

    /// <summary>
    /// Shows the Settings dialog for alert threshold and display floor.
    /// Returns true if the user applied or reset settings (caller should refresh).
    /// </summary>
    internal static bool ShowSettings(Form owner, Config config, MemoryMonitor monitor)
    {
        bool settingsChanged = false;

        using var dialog = new Form
        {
            Text = "Settings", Size = new Size(380, 340),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false
        };
        StyleDialog(dialog);
        MainForm.EnableDarkTitleBar(dialog.Handle);

        double totalGB = monitor.TotalPhysicalRamGB;

        var infoLabel = new Label
        {
            Text = $"System RAM: {totalGB:F1} GB  |  Auto default: {monitor.DefaultThresholdGB:F1} GB  |  Current: {monitor.ThresholdGB:F1} GB",
            Dock = DockStyle.Top, Height = 30, ForeColor = MainForm.FgBright,
            Font = MainForm.FontSmall, Padding = new Padding(10, 8, 8, 0)
        };

        // ── Alert Threshold section ──
        var thresholdLabel = new Label
        {
            Text = "Alert Threshold", Dock = DockStyle.Top, Height = 22,
            ForeColor = MainForm.AccentGreen, Font = MainForm.FontSemibold,
            Padding = new Padding(10, 4, 0, 0)
        };
        var thresholdPanel = new Panel { Dock = DockStyle.Top, Height = 66 };

        var rbGB = new RadioButton
        {
            Text = "Specific GB:", Location = new Point(10, 4), AutoSize = true,
            ForeColor = MainForm.FgBright, Font = MainForm.FontNormal
        };
        var tbGB = new TextBox
        {
            Text = (config.ThresholdGB > 0 ? config.ThresholdGB : monitor.ThresholdGB).ToString("F1"),
            Location = new Point(140, 3), Width = 70
        };
        StyleTextBox(tbGB);
        var lblGBUnit = new Label
        {
            Text = "GB", Location = new Point(215, 6), AutoSize = true,
            ForeColor = MainForm.FgBright, Font = MainForm.FontNormal
        };

        var rbPct = new RadioButton
        {
            Text = "Percent of RAM:", Location = new Point(10, 34), AutoSize = true,
            ForeColor = MainForm.FgBright, Font = MainForm.FontNormal
        };
        var tbPct = new TextBox
        {
            Text = (config.ThresholdPercent > 0 ? config.ThresholdPercent : 30).ToString("F0"),
            Location = new Point(140, 33), Width = 70
        };
        StyleTextBox(tbPct);
        var lblPctUnit = new Label
        {
            Text = "%", Location = new Point(215, 36), AutoSize = true,
            ForeColor = MainForm.FgBright, Font = MainForm.FontNormal
        };
        var lblPctPreview = new Label
        {
            Location = new Point(235, 36), AutoSize = true,
            ForeColor = Color.FromArgb(160, 160, 160), Font = MainForm.FontSmall
        };
        void UpdatePctPreview()
        {
            if (double.TryParse(tbPct.Text, out double p) && p > 0 && p <= 90)
                lblPctPreview.Text = $"= {totalGB * p / 100.0:F1} GB";
            else
                lblPctPreview.Text = "";
        }
        tbPct.TextChanged += (_, _) => UpdatePctPreview();
        UpdatePctPreview();

        if (config.ThresholdPercent > 0)
            rbPct.Checked = true;
        else
            rbGB.Checked = true;
        rbGB.CheckedChanged += (_, _) => { if (rbGB.Checked) tbGB.Focus(); };
        rbPct.CheckedChanged += (_, _) => { if (rbPct.Checked) tbPct.Focus(); };

        thresholdPanel.Controls.AddRange([rbGB, tbGB, lblGBUnit, rbPct, tbPct, lblPctUnit, lblPctPreview]);

        // ── Display Floor section ──
        var floorLabel = new Label
        {
            Text = "Display Floor (minimum RAM to show a process)",
            Dock = DockStyle.Top, Height = 22, ForeColor = MainForm.AccentGreen,
            Font = MainForm.FontSemibold, Padding = new Padding(10, 4, 0, 0)
        };
        var floorPanel = new Panel { Dock = DockStyle.Top, Height = 50 };

        var tbFloor = new TextBox
        {
            Text = (config.DisplayFloorGB > 0 ? config.DisplayFloorGB : 0.5).ToString("G3"),
            Location = new Point(10, 4), Width = 70
        };
        StyleTextBox(tbFloor);
        var lblFloorUnit = new Label
        {
            Text = "GB", Location = new Point(85, 7), AutoSize = true,
            ForeColor = MainForm.FgBright, Font = MainForm.FontNormal
        };
        var lblFloorHint = new Label
        {
            Text = "e.g. 0.5 = 500 MB, 1.0 = 1 GB, 0.25 = 256 MB",
            Location = new Point(10, 28), AutoSize = true,
            ForeColor = Color.FromArgb(140, 140, 140), Font = new Font("Segoe UI", 8f)
        };
        floorPanel.Controls.AddRange([tbFloor, lblFloorUnit, lblFloorHint]);

        // ── Buttons ──
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 44,
            FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6), BackColor = MainForm.BgPanel
        };

        var applyBtn = new Button { Text = "Apply", Width = 80, Height = 30 };
        StyleDialogButton(applyBtn);
        applyBtn.Click += (_, _) =>
        {
            // Validate threshold
            if (rbGB.Checked)
            {
                if (!double.TryParse(tbGB.Text, out double gb) || gb < 0.5 || gb > 128)
                { MessageBox.Show("Enter a GB value between 0.5 and 128.", "Invalid", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                config.ThresholdGB = gb;
                config.ThresholdPercent = 0;
            }
            else
            {
                if (!double.TryParse(tbPct.Text, out double pct) || pct < 1 || pct > 90)
                { MessageBox.Show("Enter a percent between 1 and 90.", "Invalid", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                config.ThresholdPercent = pct;
                config.ThresholdGB = 0;
            }

            // Validate display floor
            if (!double.TryParse(tbFloor.Text, out double floor) || floor < 0.01 || floor > 64)
            { MessageBox.Show("Enter a display floor between 0.01 and 64 GB.", "Invalid", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            config.DisplayFloorGB = Math.Abs(floor - 0.5) < 0.001 ? 0 : floor; // 0.5 is the default, store 0

            config.Save();
            settingsChanged = true;
            dialog.Close();
        };

        var resetBtn = new Button { Text = "Reset All", Width = 80, Height = 30 };
        StyleDialogButton(resetBtn);
        resetBtn.Click += (_, _) =>
        {
            config.ThresholdGB = 0;
            config.ThresholdPercent = 0;
            config.DisplayFloorGB = 0;
            config.Save();
            settingsChanged = true;
            dialog.Close();
        };

        var cancelBtn = new Button { Text = "Cancel", Width = 80, Height = 30 };
        StyleDialogButton(cancelBtn);
        cancelBtn.Click += (_, _) => dialog.Close();

        btnPanel.Controls.AddRange([applyBtn, resetBtn, cancelBtn]);

        // Dock order: bottom-up stacking (last added at top)
        dialog.Controls.Add(floorPanel);
        dialog.Controls.Add(floorLabel);
        dialog.Controls.Add(thresholdPanel);
        dialog.Controls.Add(thresholdLabel);
        dialog.Controls.Add(infoLabel);
        dialog.Controls.Add(btnPanel);
        // Bring to front in visual top-to-bottom order
        infoLabel.BringToFront();
        thresholdLabel.BringToFront();
        thresholdPanel.BringToFront();
        floorLabel.BringToFront();
        floorPanel.BringToFront();
        dialog.ShowDialog(owner);

        return settingsChanged;
    }

    /// <summary>Shows a dark help popup with yellow text explaining the app.</summary>
    internal static void ShowHelp(Form owner, MemoryMonitor monitor)
    {
        using var dialog = new Form
        {
            Text = "RAM Watchdog — Help",
            Size = new Size(480, 420),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = MainForm.BgDark,
            ForeColor = MainForm.HelpYellow
        };
        MainForm.EnableDarkTitleBar(dialog.Handle);

        var helpText = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = MainForm.BgDark,
            ForeColor = MainForm.HelpYellow,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5f),
            Text = $@"RAM WATCHDOG — Lightweight Memory Monitor

WHAT IT DOES
Monitors all running processes every 2 seconds and alerts you when any single process (or group of same-named processes) uses too much RAM. Designed to catch memory leaks before they crash your system.

HOW IT WORKS
- Polls Process.WorkingSet64 (physical RAM only, not pagefile)
- Groups processes by name (e.g. all chrome.exe instances)
- Shows processes using {monitor.DisplayFloorMB:F0} MB or more (configurable in Settings)
- Fires a sound + balloon notification when a process exceeds the alert threshold
- Re-alerts every 5 minutes if still over threshold

BUTTONS
- Silence Alerts: Suppresses all sound/balloon alerts (toggle)
- Ignore Selected: Click a process, then this button to permanently skip it
- Manage Ignored: View/remove entries from the ignore list
- Settings: Configure alert threshold (GB or %) and display floor (minimum RAM to show)
- Save Report: Export the current process list as a Markdown file
- ?: You're reading it!
- Shutdown: Fully exit the application

COLOR CODING
- Green (top row): Highest memory consumer
- Red: Process exceeding the alert threshold
- Gray: Ignored process

SYSTEM TRAY
- The app lives in the system tray (notification area)
- Double-click the tray icon to show the window
- Right-click for context menu (Show, Silence, Auto-start, Exit)
- Closing the window hides to tray — use Exit or Shutdown to quit

AUTO-START
- Right-click the tray icon and toggle 'Start with Windows'
- This adds/removes a registry entry (HKCU\Run)

CONFIG
- Settings saved to: %APPDATA%\RamWatchdog\config.json
- Reports saved to: Documents\RamWatchDog\"
        };

        var closeBtn = new Button { Text = "Got it", Dock = DockStyle.Bottom, Height = 35 };
        StyleDialogButton(closeBtn);
        closeBtn.ForeColor = MainForm.HelpYellow;
        closeBtn.Click += (_, _) => dialog.Close();

        dialog.Controls.Add(helpText);
        dialog.Controls.Add(closeBtn);
        dialog.ShowDialog(owner);
    }
}
