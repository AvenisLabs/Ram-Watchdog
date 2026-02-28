// ToastNotification.cs — Custom dark-themed toast popup for RAM alerts v1.1.0
namespace RamWatchdog;

/// <summary>
/// Borderless dark popup that appears at bottom-right of the screen.
/// Replaces Windows balloon tips which may be suppressed by OS settings.
/// Supports both per-process alerts and system-wide RAM usage alerts.
/// </summary>
internal sealed class ToastNotification : Form
{
    private const int ToastWidth = 340;
    private const int ToastHeight = 80;
    private const int AutoDismissMs = 10000;

    private static ToastNotification? _current;

    private readonly System.Windows.Forms.Timer _dismissTimer;

    private ToastNotification()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(ToastWidth, ToastHeight);
        BackColor = MainForm.BgDark;
        ForeColor = MainForm.FgBright;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;

        // Position at bottom-right of primary screen working area
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetBounds(Point.Empty);
        Location = new Point(workArea.Right - ToastWidth - 12, workArea.Bottom - ToastHeight - 12);

        _dismissTimer = new System.Windows.Forms.Timer { Interval = AutoDismissMs };
        _dismissTimer.Tick += (_, _) => DismissToast();
    }

    private string _title = "";
    private string _detail = "";
    private Action? _showWindowCallback;

    /// <summary>
    /// Shows a toast notification for a high-memory process.
    /// If a toast is already visible, replaces its content.
    /// </summary>
    internal static void ShowToast(MainForm owner, ProcessMemoryInfo proc, Action showWindowCallback)
    {
        string detail = $"{proc.Name}  —  {proc.MemoryGB:F1} GB  ({proc.ProcessCount} instance{(proc.ProcessCount > 1 ? "s" : "")})";
        ShowToast("RAM Watchdog — High Memory", detail, showWindowCallback);
    }

    /// <summary>
    /// Shows a toast notification with a custom title and detail message.
    /// Used for system-wide RAM usage alerts.
    /// </summary>
    internal static void ShowToast(string title, string detail, Action showWindowCallback)
    {
        if (_current != null && !_current.IsDisposed)
        {
            _current.UpdateContent(title, detail, showWindowCallback);
            return;
        }

        var toast = new ToastNotification();
        toast.UpdateContent(title, detail, showWindowCallback);
        _current = toast;
        toast.Show();
    }

    private void UpdateContent(string title, string detail, Action showWindowCallback)
    {
        _title = title;
        _detail = detail;
        _showWindowCallback = showWindowCallback;

        Invalidate(); // trigger repaint with new content

        // Restart auto-dismiss timer
        _dismissTimer.Stop();
        _dismissTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        // Red left accent bar
        using var accentBrush = new SolidBrush(MainForm.AlertRed);
        g.FillRectangle(accentBrush, 0, 0, 4, Height);

        // Border
        using var borderPen = new Pen(MainForm.BorderColor);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

        // Title
        using var titleFont = new Font("Segoe UI Semibold", 10f);
        g.DrawString(_title, titleFont, Brushes.White, 12, 8);

        // Detail
        using var detailFont = new Font("Segoe UI", 9f);
        using var detailBrush = new SolidBrush(MainForm.AlertRed);
        g.DrawString(_detail, detailFont, detailBrush, 12, 34);

        // Hint text
        using var hintFont = new Font("Segoe UI", 7.5f);
        using var hintBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        g.DrawString("Click to open  •  auto-dismiss in 10s", hintFont, hintBrush, 12, 56);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        _showWindowCallback?.Invoke();
        DismissToast();
    }

    private void DismissToast()
    {
        _dismissTimer.Stop();
        _current = null;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dismissTimer.Dispose();
            if (_current == this) _current = null;
        }
        base.Dispose(disposing);
    }
}
