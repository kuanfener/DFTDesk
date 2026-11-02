// Uses Filee.App.Controls.DonutMenu from the original source tree. The same
// renderer, geometry, hover springs and opening/closing animations are used in
// the standalone converter; only the two available export items differ.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Win32.Interoperability;
using Filee.App.Controls;
using DrawingColor = System.Drawing.Color;
using DrawingPoint = System.Drawing.Point;
using AvaloniaPoint = Avalonia.Point;

namespace Filee.CrystalConverter;

internal static class FileeDonutFactory
{
    public static DonutMenu Create(double radius, DrawingColor accent)
    {
        var donut = new DonutMenu
        {
            Items =
            [
                new DonutItem { Id = "cif", Label = "CIF", Caption = "导出 CIF" },
                new DonutItem { Id = "poscar", Label = "POSCAR", Caption = "导出 POSCAR" },
            ],
            CenterTitle = "晶体",
            CenterSubtitle = "转换",
            OuterRadius = radius,
            HoleRatio = .42,
            SliceOpacity = .96,
            CornerRadius = new CornerRadius(16),
            SliceBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            SliceBorderBrush = new SolidColorBrush(Color.FromRgb(222, 219, 238)),
            CenterBrush = new SolidColorBrush(Color.FromRgb(249, 248, 245)),
            Foreground = new SolidColorBrush(Color.FromRgb(31, 30, 29)),
            SecondaryForeground = new SolidColorBrush(Color.FromRgb(107, 106, 102)),
            DangerBrush = new SolidColorBrush(Color.FromRgb(215, 70, 70)),
        };
        ApplyAccent(donut, accent);
        return donut;
    }

    public static void ApplyAccent(DonutMenu donut, DrawingColor color)
    {
        donut.AccentBrush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        donut.OnAccentBrush = Brushes.White;
        donut.InvalidateVisual();
    }

    public static ExportFormat? Format(DonutHit hit) =>
        hit.Kind == DonutHitKind.Segment ? hit.Index switch
        {
            0 => ExportFormat.Cif,
            1 => ExportFormat.Poscar,
            _ => null,
        } : null;

    public static string[] Files(IDataTransfer transfer) =>
        (transfer.TryGetFiles() ?? []).Select(item => item.TryGetLocalPath())
            .OfType<string>().Where(File.Exists).Where(path => ConversionBatch.Detect(path) is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static void AttachDrop(DonutMenu donut, Action<ExportFormat, string[]> onDrop)
    {
        DragDrop.SetAllowDrop(donut, true);
        DragDrop.AddDragEnterHandler(donut, (_, e) => Over(donut, e));
        DragDrop.AddDragOverHandler(donut, (_, e) => Over(donut, e));
        DragDrop.AddDragLeaveHandler(donut, (_, _) => donut.SetExternalHighlight(-1));
        DragDrop.AddDropHandler(donut, (_, e) =>
        {
            var hit = donut.HitTest(e.GetPosition(donut));
            var format = Format(hit);
            var files = Files(e.DataTransfer);
            e.DragEffects = format is not null && files.Length > 0
                ? DragDropEffects.Copy : DragDropEffects.None;
            donut.SetExternalHighlight(-1);
            if (format is { } target && files.Length > 0)
                onDrop(target, files);
            e.Handled = true;
        });
    }

    private static void Over(DonutMenu donut, Avalonia.Input.DragEventArgs e)
    {
        var point = e.GetPosition(donut);
        var hit = donut.HitTest(point);
        donut.SetExternalHit(hit, point);
        e.DragEffects = donut.Converts(hit) && Files(e.DataTransfer).Length > 0
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
}

/// <summary>An embedded preview using the exact same renderer as Filee's live donut.</summary>
internal sealed class FileeDonutHost : WinFormsAvaloniaControlHost
{
    private readonly DonutMenu _donut;
    private DrawingColor _accent = DrawingColor.FromArgb(127, 119, 221);

    public event Action<ExportFormat>? FormatChosen;
    public event Action<ExportFormat, string[]>? FilesDropped;

    public FileeDonutHost()
    {
        Size = new System.Drawing.Size(270, 270);
        _donut = FileeDonutFactory.Create(105, _accent);
        Content = _donut;
        _donut.SegmentInvoked += (_, index) =>
        {
            if (index is 0 or 1)
                FormatChosen?.Invoke(index == 0 ? ExportFormat.Cif : ExportFormat.Poscar);
        };
        FileeDonutFactory.AttachDrop(_donut, (format, files) => FilesDropped?.Invoke(format, files));
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DrawingColor AccentColor
    {
        get => _accent;
        set
        {
            _accent = value;
            FileeDonutFactory.ApplyAccent(_donut, value);
        }
    }

    public ExportFormat? HitTest(DrawingPoint point)
    {
        if (Width <= 0 || Height <= 0 || _donut.Bounds.Width <= 0 || _donut.Bounds.Height <= 0)
            return null;
        var at = new AvaloniaPoint(point.X * _donut.Bounds.Width / Width,
            point.Y * _donut.Bounds.Height / Height);
        return FileeDonutFactory.Format(_donut.HitTest(at));
    }

    public void SetHighlight(ExportFormat? format) =>
        _donut.SetExternalHighlight(format is null ? -1 : format == ExportFormat.Cif ? 0 : 1);
}

/// <summary>Original Filee donut in its own native transparent Avalonia window.</summary>
internal sealed class FileeDonutPopup : IDisposable
{
    private readonly DonutMenu _donut;
    private readonly Avalonia.Controls.Window _window;
    private DrawingColor _accent = DrawingColor.FromArgb(127, 119, 221);

    public event Action<ExportFormat>? FormatChosen;
    public event Action<ExportFormat, string[]>? FilesDropped;

    public FileeDonutPopup()
    {
        _donut = FileeDonutFactory.Create(125, _accent);
        _window = new Avalonia.Controls.Window
        {
            Title = "Filee Crystal Donut",
            Content = _donut,
            WindowDecorations = WindowDecorations.None,
            Background = Brushes.Transparent,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            CanResize = false,
            SizeToContent = SizeToContent.WidthAndHeight,
        };
        _donut.SegmentInvoked += (_, index) =>
        {
            if (index is 0 or 1)
                FormatChosen?.Invoke(index == 0 ? ExportFormat.Cif : ExportFormat.Poscar);
        };
        FileeDonutFactory.AttachDrop(_donut, (format, files) => FilesDropped?.Invoke(format, files));
    }

    public bool Visible => _window.IsVisible;

    public DrawingColor AccentColor
    {
        get => _accent;
        set
        {
            _accent = value;
            FileeDonutFactory.ApplyAccent(_donut, value);
        }
    }

    public void OpenAt(DrawingPoint point, bool activate = true)
    {
        // PixelPoint is in screen pixels, matching SharpHook's global pointer.
        const int side = 300;
        var work = System.Windows.Forms.Screen.FromPoint(point).WorkingArea;
        var x = Math.Clamp(point.X - side / 2, work.Left, work.Right - side);
        var y = Math.Clamp(point.Y - side / 2, work.Top, work.Bottom - side);
        _window.Position = new PixelPoint(x, y);
        _window.ShowActivated = activate;
        _donut.PlayOpenAnimation();
        _window.Show();
        if (activate) _donut.Focus();
    }

    public void Hide()
    {
        if (!_window.IsVisible) return;
        _donut.SetExternalHighlight(-1);
        _donut.PlayCloseAnimation(() => _window.Hide());
    }

    public void Dispose() => _window.Close();
}
