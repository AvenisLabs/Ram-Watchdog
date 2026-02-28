// ToastNotification.cs — Custom dark-themed toast popup for RAM alerts v1.0.1
namespace RamWatchdog;

/// <summary>
/// Borderless dark popup that appears at bottom-right of the screen.
/// Replaces Windows balloon tips which may be suppressed by OS settings.
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

    private string _processName = "";
    private string _memoryText = "";
    private string _instanceText = "";

    /// <summary>
    /// Shows a toast notification for a high-memory process.
    /// If a toast is already visible, replaces its content.
    /// </summary>
    /// <param name="owner">MainForm instance (used for ShowWindow callback).</param>
    /// <param name="proc">Process info to display.</param>
    /// <param name="showWindowCallback">Action to invoke when the toast is clicked.</param>
    internal static void ShowToast(MainForm owner, ProcessMemoryInfo proc, Action showWindowCallback)
    {
        if (_current != null && !_current.IsDisposed)
        {
            // Reuse existing toast — update content and restart timer
            _current.UpdateContent(proc, showWindowCallback);
            return;
        }

        var toast = new ToastNotification();
        toast.UpdateContent(proc, showWindowCallback);
        _current = toast;
        toast.Show();
    }

    private Action? _showWindowCallback;

    private void UpdateContent(ProcessMemoryInfo proc, Action showWindowCallback)
    {
        _processName = proc.Name;
        _memoryText = $"{proc.MemoryGB:F1} GB";
        _instanceText = $"{proc.ProcessCount} instance{(proc.ProcessCount > 1 ? "s" : "")}";
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
        g.DrawString("RAM Watchdog — High Memory", titleFont, Brushes.White, 12, 8);

        // Process details
        string detail = $"{_processName}  —  {_memoryText}  ({_instanceText})";
        using var detailFont = new Font("Segoe UI", 9f);
        using var detailBrush = new SolidBrush(MainForm.AlertRed);
        g.DrawString(detail, detailFont, detailBrush, 12, 34);

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
