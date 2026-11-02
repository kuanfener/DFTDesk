using System.Drawing;
using System.Windows.Forms;

namespace Filee.CrystalConverter;

/// <summary>Resizable, DPI-aware standalone UI using Filee's Soft dark theme palette.</summary>
internal sealed partial class MainForm
{
    private static readonly Color Muted = Color.FromArgb(174, 172, 164);
    private static readonly Color ControlSurface = Color.FromArgb(29, 29, 28);
    private static readonly Color Border = Color.FromArgb(69, 68, 73);

    private void BuildLayout()
    {
        SuspendLayout();
        Text = "Filee · 晶体转换";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1020, 760);
        MinimumSize = new Size(870, 690);
        BackColor = Background;
        ForeColor = TextColor;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        KeyPreview = true;
        AllowDrop = true;

        var root = Grid(1, 5);
        root.Dock = DockStyle.Fill;
        root.Padding = new Padding(24, 18, 24, 18);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 314));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = Grid(2, 1);
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        var heading = Grid(1, 2);
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Controls.Add(Label("Filee  /  晶体转换", 24, Accent, true), 0, 0);
        heading.Controls.Add(Label("VASP POSCAR / CONTCAR  ⇄  CIF", 10, Muted), 0, 1);
        header.Controls.Add(heading, 0, 0);
        header.Controls.Add(Label("资源管理器中 Ctrl + 左键拖动  ·  Ctrl + Alt + Space", 9, Muted,
            align: ContentAlignment.MiddleRight), 1, 0);
        root.Controls.Add(header, 0, 0);

        var sections = Grid(2, 1);
        sections.Margin = new Padding(0, 0, 0, 12);
        sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        root.Controls.Add(sections, 0, 1);

        var filesCard = SurfaceCard();
        filesCard.Margin = new Padding(0, 0, 12, 0);
        var filesLayout = Grid(1, 4);
        filesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        filesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        filesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        filesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        filesLayout.Controls.Add(Label("结构文件", 13, TextColor, true), 0, 0);
        filesLayout.Controls.Add(Label("支持多选和拖入文件，包括无扩展名的 POSCAR / CONTCAR", 9, Muted), 0, 1);
        _files.Dock = DockStyle.Fill;
        _files.Margin = new Padding(0, 0, 0, 8);
        _files.BorderStyle = BorderStyle.None;
        _files.SelectionMode = SelectionMode.MultiExtended;
        _files.BackColor = ControlSurface;
        _files.ForeColor = TextColor;
        _files.Font = new Font("Consolas", 9.5f);
        filesLayout.Controls.Add(_files, 0, 2);
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, Margin = Padding.Empty, Padding = Padding.Empty,
        };
        actions.Controls.Add(Button("＋ 添加文件", 135, (_, _) => BrowseFiles(), primary: true));
        actions.Controls.Add(Button("移除选中", 104, (_, _) => RemoveSelected()));
        actions.Controls.Add(Button("清空列表", 104, (_, _) => ClearFiles()));
        filesLayout.Controls.Add(actions, 0, 3);
        filesCard.Controls.Add(filesLayout);
        sections.Controls.Add(filesCard, 0, 0);

        var wheelCard = SurfaceCard(12);
        var wheelLayout = Grid(1, 3);
        wheelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        wheelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        wheelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        wheelLayout.Controls.Add(Label("选择导出格式", 13, TextColor, true,
            ContentAlignment.MiddleCenter), 0, 0);
        _wheel.Anchor = AnchorStyles.None;
        _wheel.Size = new Size(244, 244);
        _wheel.FormatChosen += target => _ = StartBatchAsync(target);
        wheelLayout.Controls.Add(_wheel, 0, 1);
        wheelLayout.Controls.Add(Label("点击扇区即可批量转换", 9, Muted,
            align: ContentAlignment.MiddleCenter), 0, 2);
        wheelCard.Controls.Add(wheelLayout);
        sections.Controls.Add(wheelCard, 1, 0);

        var optionsCard = SurfaceCard();
        optionsCard.Margin = new Padding(0, 0, 0, 12);
        var optionRows = Grid(1, 2);
        optionRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        optionRows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var pathRow = Grid(3, 1);
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        pathRow.Controls.Add(Label("输出目录", 10, TextColor, true), 0, 0);
        _output.Dock = DockStyle.Fill;
        _output.Margin = new Padding(0, 4, 12, 7);
        _output.BackColor = ControlSurface;
        _output.ForeColor = TextColor;
        _output.BorderStyle = BorderStyle.FixedSingle;
        pathRow.Controls.Add(_output, 1, 0);
        var browse = Button("浏览…", 104, (_, _) => BrowseFolder());
        browse.Dock = DockStyle.Fill;
        browse.Margin = new Padding(0, 3, 0, 7);
        pathRow.Controls.Add(browse, 2, 0);
        optionRows.Controls.Add(pathRow, 0, 0);

        var optionFooter = Grid(2, 1);
        optionFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        optionFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        optionFooter.Controls.Add(Label("留空则保存到源文件夹；同名时自动生成新文件，不覆盖原文件",
            9, Muted), 0, 0);
        _cartesian.Text = "POSCAR 使用 Cartesian 坐标（默认 Direct）";
        _cartesian.Dock = DockStyle.Fill;
        _cartesian.TextAlign = ContentAlignment.MiddleRight;
        _cartesian.ForeColor = Muted;
        _cartesian.BackColor = PanelColor;
        _cartesian.Margin = Padding.Empty;
        optionFooter.Controls.Add(_cartesian, 1, 0);
        optionRows.Controls.Add(optionFooter, 0, 1);
        optionsCard.Controls.Add(optionRows);
        root.Controls.Add(optionsCard, 0, 2);

        var logCard = SurfaceCard();
        logCard.Margin = new Padding(0, 0, 0, 8);
        var logRows = Grid(1, 2);
        logRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        logRows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        logRows.Controls.Add(Label("批量转换记录", 11, TextColor, true), 0, 0);
        _log.Dock = DockStyle.Fill;
        _log.BorderStyle = BorderStyle.None;
        _log.BackColor = ControlSurface;
        _log.ForeColor = TextColor;
        _log.Font = new Font("Consolas", 9);
        logRows.Controls.Add(_log, 0, 1);
        logCard.Controls.Add(logRows);
        root.Controls.Add(logCard, 0, 3);

        var footer = Grid(3, 1);
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 195));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        _progress.Dock = DockStyle.Fill;
        _progress.Margin = new Padding(1, 12, 18, 13);
        footer.Controls.Add(_progress, 0, 0);
        _status.Dock = DockStyle.Fill;
        _status.ForeColor = Muted;
        _status.TextAlign = ContentAlignment.MiddleRight;
        footer.Controls.Add(_status, 1, 0);
        _cancel.Text = "取消";
        _cancel.FlatStyle = FlatStyle.Flat;
        _cancel.FlatAppearance.BorderColor = Border;
        _cancel.FlatAppearance.BorderSize = 1;
        _cancel.BackColor = ControlSurface;
        _cancel.ForeColor = TextColor;
        _cancel.Dock = DockStyle.Fill;
        _cancel.Margin = new Padding(12, 4, 0, 4);
        _cancel.Click += (_, _) => _cancellation?.Cancel();
        _cancel.Enabled = false;
        footer.Controls.Add(_cancel, 2, 0);
        root.Controls.Add(footer, 0, 4);

        // Register native file drop targets on child controls, not just the outer form.
        foreach (var control in new Control[] { root, filesCard, filesLayout, _files, optionsCard, logCard })
            AcceptFileDrops(control);
        _wheel.AllowDrop = true;
        _wheel.DragEnter += WheelDragOver;
        _wheel.DragOver += WheelDragOver;
        _wheel.DragDrop += (_, e) =>
        {
            var format = _wheel.HitTest(_wheel.PointToClient(new Point(e.X, e.Y)));
            if (format is null || e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;
            AddFiles(files, replace: true);
            _ = StartBatchAsync(format.Value);
        };

        ResumeLayout(performLayout: true);
    }

    private void WheelDragOver(object? sender, DragEventArgs e)
    {
        var target = _wheel.HitTest(_wheel.PointToClient(new Point(e.X, e.Y)));
        _wheel.SetHighlight(target);
        e.Effect = target is not null && e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void AcceptFileDrops(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy : DragDropEffects.None;
        control.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths) AddFiles(paths);
        };
    }

    private static TableLayoutPanel Grid(int columns, int rows) =>
        new()
        {
            ColumnCount = columns, RowCount = rows, Dock = DockStyle.Fill,
            Margin = Padding.Empty, Padding = Padding.Empty,
        };

    private static Panel SurfaceCard(int padding = 18)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill, Padding = new Padding(padding),
            BackColor = PanelColor, Margin = Padding.Empty,
        };
        panel.Paint += (_, e) =>
        {
            using var border = new Pen(Border);
            e.Graphics.DrawRectangle(border, 0, 0, panel.Width - 1, panel.Height - 1);
        };
        return panel;
    }

    private static Label Label(string text, float size, Color color, bool bold = false,
        ContentAlignment align = ContentAlignment.MiddleLeft) =>
        new()
        {
            Text = text, Dock = DockStyle.Fill, Margin = Padding.Empty,
            AutoEllipsis = true, TextAlign = align, ForeColor = color,
            Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        };

    private static Button Button(string text, int width, EventHandler click, bool primary = false)
    {
        var button = new Button
        {
            Text = text, Width = width, Height = 37, Margin = new Padding(0, 4, 9, 3),
            FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false,
            BackColor = primary ? Accent : Color.FromArgb(52, 51, 55),
            ForeColor = TextColor, Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 9),
        };
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = primary
            ? Color.FromArgb(149, 142, 235) : Color.FromArgb(69, 67, 77);
        button.Click += click;
        return button;
    }
}
