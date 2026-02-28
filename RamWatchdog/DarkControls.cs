// DarkControls.cs — Dark-themed WinForms control classes for ListView and context menus v1.0.2
using System.Runtime.InteropServices;

namespace RamWatchdog;

/// <summary>
/// Subclasses the ListView's native header control to paint the blank area
/// (to the right of the last column) with the dark background color.
/// Intercepts both WM_ERASEBKGND and WM_PAINT to fully eliminate white gaps.
/// </summary>
internal sealed class DarkHeaderNativeWindow : NativeWindow
{
    private const int WM_PAINT = 0x000F;
    private const int WM_ERASEBKGND = 0x0014;
    private const int HDM_FIRST = 0x1200;
    private const int HDM_GETITEMCOUNT = HDM_FIRST + 0;
    private const int HDM_GETITEMRECT = HDM_FIRST + 7;

    private readonly Color _bgColor;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    public DarkHeaderNativeWindow(Color bgColor) => _bgColor = bgColor;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ERASEBKGND)
        {
            // Fill entire header background dark before any column painting
            GetClientRect(Handle, out var rc);
            using var g = Graphics.FromHdc(m.WParam);
            using var brush = new SolidBrush(_bgColor);
            g.FillRectangle(brush, rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top);
            m.Result = (IntPtr)1; // mark as handled
            return;
        }

        base.WndProc(ref m);

        if (m.Msg == WM_PAINT)
            PaintBlankArea();
    }

    private void PaintBlankArea()
    {
        int count = (int)SendMessage(Handle, HDM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
        if (count <= 0) return;

        var rectBytes = Marshal.AllocHGlobal(Marshal.SizeOf<RECT>());
        try
        {
            SendMessage(Handle, HDM_GETITEMRECT, (IntPtr)(count - 1), rectBytes);
            var lastRect = Marshal.PtrToStructure<RECT>(rectBytes);
            GetClientRect(Handle, out var clientRect);

            if (lastRect.Right < clientRect.Right)
            {
                using var g = Graphics.FromHwnd(Handle);
                using var brush = new SolidBrush(_bgColor);
                g.FillRectangle(brush, lastRect.Right, clientRect.Top,
                    clientRect.Right - lastRect.Right, clientRect.Bottom - clientRect.Top);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(rectBytes);
        }
    }
}

/// <summary>
/// Dark-themed ListView with double-buffering and header subclassing.
/// </summary>
internal sealed class DarkListView : ListView
{
    private const int LVM_FIRST = 0x1000;
    private const int LVM_SETEXTENDEDLISTVIEWSTYLE = LVM_FIRST + 54;
    private const int LVM_GETEXTENDEDLISTVIEWSTYLE = LVM_FIRST + 55;
    private const int LVM_GETHEADER = LVM_FIRST + 31;
    private const int LVS_EX_DOUBLEBUFFER  = 0x00010000;
    private const int LVS_EX_FULLROWSELECT = 0x00000020;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private DarkHeaderNativeWindow? _headerSubclass;
    private readonly Color _headerBgColor;

    public DarkListView(Color headerBgColor) => _headerBgColor = headerBgColor;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        int styles = (int)SendMessage(Handle, LVM_GETEXTENDEDLISTVIEWSTYLE, IntPtr.Zero, IntPtr.Zero);
        styles |= LVS_EX_DOUBLEBUFFER | LVS_EX_FULLROWSELECT;
        SendMessage(Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, IntPtr.Zero, (IntPtr)styles);

        IntPtr headerHwnd = SendMessage(Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
        if (headerHwnd != IntPtr.Zero)
        {
            _headerSubclass = new DarkHeaderNativeWindow(_headerBgColor);
            _headerSubclass.AssignHandle(headerHwnd);
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _headerSubclass?.ReleaseHandle();
        _headerSubclass = null;
        base.OnHandleDestroyed(e);
    }
}

/// <summary>Custom renderer for dark-themed context menus.</summary>
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color MenuBg    = Color.FromArgb(28, 28, 28);
    private static readonly Color MenuHover = Color.FromArgb(50, 50, 50);
    private static readonly Color SepColor  = Color.FromArgb(50, 50, 50);
    private static readonly Font CheckFont  = new("Segoe UI", 9f, FontStyle.Bold);

    // ── Cached GDI objects for per-render hot paths ──
    private static readonly SolidBrush BrushMenuBg    = new(MenuBg);
    private static readonly SolidBrush BrushMenuHover = new(MenuHover);
    private static readonly SolidBrush BrushCheckAccent = new(Color.FromArgb(78, 201, 110));
    private static readonly Pen PenSep = new(SepColor);

    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var brush = e.Item.Selected ? BrushMenuHover : BrushMenuBg;
        e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    { e.TextColor = Color.White; base.OnRenderItemText(e); }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        e.Graphics.FillRectangle(BrushMenuBg, bounds);
        e.Graphics.DrawLine(PenSep, bounds.Left + 4, bounds.Height / 2, bounds.Right - 4, bounds.Height / 2);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        e.Graphics.DrawRectangle(PenSep, 0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
    }

    // Render checkmark for auto-start menu item
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var rect = e.ImageRectangle;
        e.Graphics.FillRectangle(BrushMenuHover, rect);
        e.Graphics.DrawString("\u2713", CheckFont, BrushCheckAccent, rect.Left - 1, rect.Top);
    }
}

/// <summary>Color table overrides for dark menu backgrounds.</summary>
internal sealed class DarkColorTable : ProfessionalColorTable
{
    private static readonly Color Bg = Color.FromArgb(28, 28, 28);
    private static readonly Color Border = Color.FromArgb(50, 50, 50);
    public override Color MenuItemSelected => Color.FromArgb(50, 50, 50);
    public override Color MenuBorder => Border;
    public override Color MenuItemBorder => Border;
    public override Color ToolStripDropDownBackground => Bg;
    public override Color ImageMarginGradientBegin => Bg;
    public override Color ImageMarginGradientMiddle => Bg;
    public override Color ImageMarginGradientEnd => Bg;
    public override Color CheckBackground => Bg;
    public override Color CheckSelectedBackground => Color.FromArgb(50, 50, 50);
    public override Color SeparatorDark => Border;
    public override Color SeparatorLight => Border;
}
