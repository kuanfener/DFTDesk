using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Filee.CrystalConverter;

/// <summary>Two-target crystal export wheel, usable in the main window and as a popup over Explorer.</summary>
internal sealed class ConversionWheel : Control
{
    private const int Outer = 122;
    private const int Inner = 51;
    private ExportFormat? _hovered;

    public event Action<ExportFormat>? FormatChosen;

    public ConversionWheel()
    {
        DoubleBuffered = true;
        Size = new Size(278, 278);
        BackColor = Color.FromArgb(21, 26, 43);
        Cursor = Cursors.Hand;
    }

    public ExportFormat? HitTest(Point point)
    {
        var x = point.X - Width / 2.0;
        var y = point.Y - Height / 2.0;
        var radius = Math.Sqrt(x * x + y * y);
        return radius is >= Inner and <= Outer
            ? y < 0 ? ExportFormat.Cif : ExportFormat.Poscar
            : null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var format = HitTest(e.Location);
        if (format == _hovered) return;
        _hovered = format;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = null;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && HitTest(e.Location) is { } format)
            FormatChosen?.Invoke(format);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var center = new PointF(Width / 2f, Height / 2f);
        var outer = new RectangleF(center.X - Outer, center.Y - Outer, 2 * Outer, 2 * Outer);
        var inner = new RectangleF(center.X - Inner, center.Y - Inner, 2 * Inner, 2 * Inner);

        using (var top = Sector(outer, inner, 181, 178))
        using (var topBrush = new SolidBrush(_hovered == ExportFormat.Cif
            ? Color.FromArgb(93, 196, 251) : Color.FromArgb(49, 126, 177)))
            g.FillPath(topBrush, top);
        using (var bottom = Sector(outer, inner, 1, 178))
        using (var bottomBrush = new SolidBrush(_hovered == ExportFormat.Poscar
            ? Color.FromArgb(119, 225, 181) : Color.FromArgb(56, 157, 129)))
            g.FillPath(bottomBrush, bottom);

        using var rim = new Pen(Color.FromArgb(65, 85, 117), 1.5f);
        g.DrawEllipse(rim, outer);
        g.DrawEllipse(rim, inner);
        using var primary = new Font("Segoe UI", 18, FontStyle.Bold);
        using var smaller = new Font("Segoe UI", 9, FontStyle.Regular);
        using var centerFont = new Font("Segoe UI", 11, FontStyle.Bold);
        CenterText(g, "导出 CIF", primary, Brushes.White, new PointF(center.X, center.Y - 78));
        CenterText(g, "导出 POSCAR", primary, Brushes.White, new PointF(center.X, center.Y + 70));
        CenterText(g, "晶体", centerFont, Brushes.White, new PointF(center.X, center.Y - 11));
        CenterText(g, "转换", centerFont, Brushes.White, new PointF(center.X, center.Y + 11));
        CenterText(g, "选择目标格式", smaller, Brushes.LightGray, new PointF(center.X, center.Y + Outer + 12));
    }

    private static GraphicsPath Sector(RectangleF outer, RectangleF inner, float start, float sweep)
    {
        var path = new GraphicsPath();
        path.AddArc(outer, start, sweep);
        path.AddArc(inner, start + sweep, -sweep);
        path.CloseFigure();
        return path;
    }

    private static void CenterText(Graphics g, string text, Font font, Brush brush, PointF center)
    {
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, center, format);
    }
}

/// <summary>Non-activating topmost wheel for Ctrl-drag gestures started in Explorer.</summary>
internal sealed class WheelPopup : Form
{
    private readonly ConversionWheel _wheel = new() { Dock = DockStyle.Fill };

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            return cp;
        }
    }

    public event Action<ExportFormat>? FormatChosen;

    public WheelPopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        ClientSize = _wheel.Size;
        BackColor = _wheel.BackColor;
        Controls.Add(_wheel);
        _wheel.FormatChosen += format => FormatChosen?.Invoke(format);
    }

    public void OpenAt(Point point)
    {
        var bounds = Screen.FromPoint(point).WorkingArea;
        Location = new Point(Math.Clamp(point.X - Width / 2, bounds.Left, bounds.Right - Width),
            Math.Clamp(point.Y - Height / 2, bounds.Top, bounds.Bottom - Height));
        Show();
        BringToFront();
    }

    public ExportFormat? HitScreenPoint(Point screenPoint) =>
        _wheel.HitTest(_wheel.PointToClient(screenPoint));
}
