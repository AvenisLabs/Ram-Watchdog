// AlertIcon.cs — Red alert icon generator for tray icon flash v1.0.1
namespace RamWatchdog;

/// <summary>
/// Generates an in-memory red "R" icon used when any process exceeds the alert threshold.
/// Mirrors the green app icon style but with a red background to signal active alerts.
/// </summary>
internal static class AlertIcon
{
    /// <summary>
    /// Creates a 16x16 red "R" icon. Called once at startup and cached.
    /// </summary>
    internal static Icon CreateAlertIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(MainForm.AlertRed);
        using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var brush = new SolidBrush(Color.White);
        g.DrawString("R", font, brush, 0, 0);
        IntPtr hIcon = bmp.GetHicon();
        using var tempIcon = Icon.FromHandle(hIcon);
        var result = (Icon)tempIcon.Clone();
        MainForm.DestroyIcon(hIcon); // release the unmanaged HICON handle
        return result;
    }
}
