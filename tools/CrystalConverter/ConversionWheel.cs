using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Filee.CrystalConverter;

/// <summary>A Filee-style two-target donut, with exact annular hit-testing.</summary>
internal sealed class ConversionWheel : Control
{
    private static readonly Color Surface = Color.FromArgb(38, 38, 36);
    private static readonly Color Slice = Color.FromArgb(60, 59, 66);
    private static readonly Color Highlight = Color.FromArgb(127, 119, 221);
    private static readonly Color TextPrimary = Color.FromArgb(241, 239, 232);
    private ExportFormat? _hovered;

    public event Action<ExportFormat>? FormatChosen;

    public ConversionWheel()
    {
        DoubleBuffered = true;
        Size = new Size(250, 250);
        BackColor = Surface;
    }

    private (PointF Center, float Outer, float Inner) Ring =>
        (new PointF(Width / 2f, Height / 2f), Math.Min(Width, Height) * .43f,
            Math.Min(Width, Height) * .19f);

    public ExportFormat? HitTest(Point point)
    {
        var (center, outer, inner) = Ring;
        var x = point.X - center.X;
        var y = point.Y - center.Y;
        var radiusSquared = x * x + y * y;
        return radiusSquared >= inner * inner && radiusSquared <= outer * outer
            ? y < 0 ? ExportFormat.Cif : ExportFormat.Poscar
            : null;
    }

    public void SetHighlight(ExportFormat? format)
    {
        if (_hovered == format) return;
        _hovered = format;
        Cursor = format is null ? Cursors.Default : Cursors.Hand;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHighlight(HitTest(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHighlight(null);
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
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var (center, outerRadius, innerRadius) = Ring;
        var outer = Circle(center, outerRadius);
        var inner = Circle(center, innerRadius);

        using (var upper = Sector(outer, inner, 181.5f, 177))
        using (var brush = new SolidBrush(_hovered == ExportFormat.Cif ? Highlight : Slice))
            g.FillPath(brush, upper);
        using (var lower = Sector(outer, inner, 1.5f, 177))
        using (var brush = new SolidBrush(_hovered == ExportFormat.Poscar ? Highlight : Slice))
            g.FillPath(brush, lower);
        using (var holeBrush = new SolidBrush(Surface))
            g.FillEllipse(holeBrush, inner);
        using (var outline = new Pen(Color.FromArgb(97, 94, 110), 1.15f))
        {
            g.DrawEllipse(outline, outer);
            g.DrawEllipse(outline, inner);
        }

        var factor = Math.Min(Width, Height) / 250f;
        using var title = new Font("Microsoft YaHei UI", 14 * factor, FontStyle.Bold);
        using var poscarTitle = new Font("Microsoft YaHei UI", 11 * factor, FontStyle.Bold);
        using var secondary = new Font("Microsoft YaHei UI", 8 * factor);
        using var centreFont = new Font("Microsoft YaHei UI", 10 * factor, FontStyle.Bold);
        using var primary = new SolidBrush(TextPrimary);
        using var active = new SolidBrush(Color.White);
        var upperText = _hovered == ExportFormat.Cif ? active : primary;
        var lowerText = _hovered == ExportFormat.Poscar ? active : primary;
        CenterText(g, "导出为", secondary, upperText, new PointF(center.X, center.Y - outerRadius * .77f));
        CenterText(g, "CIF", title, upperText, new PointF(center.X, center.Y - outerRadius * .50f));
        CenterText(g, "POSCAR", poscarTitle, lowerText, new PointF(center.X, center.Y + outerRadius * .55f));
        CenterText(g, "导出为", secondary, lowerText, new PointF(center.X, center.Y + outerRadius * .81f));
        CenterText(g, "晶体", centreFont, primary, new PointF(center.X, center.Y - 10 * factor));
        CenterText(g, "转换", centreFont, primary, new PointF(center.X, center.Y + 10 * factor));
        if (_hovered is not null)
        {
            using var border = new Pen(Color.FromArgb(200, 177, 168, 255), 2 * factor);
            g.DrawArc(border, outer, _hovered == ExportFormat.Cif ? 183 : 3, 174);
        }
    }

    private static RectangleF Circle(PointF center, float radius) =>
        new(center.X - radius, center.Y - radius, radius * 2, radius * 2);

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

/// <summary>Non-activating native drop target for Explorer's Ctrl-drag gesture.</summary>
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
    public event Action<ExportFormat, string[]>? FilesDropped;

    public WheelPopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        ClientSize = new Size(280, 280);
        BackColor = Color.Fuchsia;
        TransparencyKey = Color.Fuchsia;
        _wheel.BackColor = Color.Fuchsia;
        Controls.Add(_wheel);
        _wheel.FormatChosen += format => FormatChosen?.Invoke(format);

        // WinForms dispatches OLE drag events to whichever child is under the cursor.
        // Without these native drop targets, Explorer always displays its "×" cursor.
        AllowDrop = true;
        _wheel.AllowDrop = true;
        DragEnter += HandleDragOver;
        DragOver += HandleDragOver;
        DragDrop += HandleDrop;
        DragLeave += (_, _) => _wheel.SetHighlight(null);
        _wheel.DragEnter += HandleDragOver;
        _wheel.DragOver += HandleDragOver;
        _wheel.DragDrop += HandleDrop;
        _wheel.DragLeave += (_, _) => _wheel.SetHighlight(null);
    }

    private void HandleDragOver(object? sender, DragEventArgs e)
    {
        var target = HitScreenPoint(new Point(e.X, e.Y));
        _wheel.SetHighlight(target);
        // Never advertise Move: Explorer must not delete the original structure.
        e.Effect = target is not null && e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void HandleDrop(object? sender, DragEventArgs e)
    {
        var target = HitScreenPoint(new Point(e.X, e.Y));
        if (target is not { } format || e.Data?.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            e.Effect = DragDropEffects.None;
            return;
        }
        e.Effect = DragDropEffects.Copy;
        FilesDropped?.Invoke(format, paths);
    }

    public void OpenAt(Point point)
    {
        _wheel.SetHighlight(null);
        var bounds = Screen.FromPoint(point).WorkingArea;
        Location = new Point(Math.Clamp(point.X - Width / 2, bounds.Left, bounds.Right - Width),
            Math.Clamp(point.Y - Height / 2, bounds.Top, bounds.Bottom - Height));
        Show();
        BringToFront();
    }

    public ExportFormat? HitScreenPoint(Point screenPoint) =>
        _wheel.HitTest(_wheel.PointToClient(screenPoint));
}
