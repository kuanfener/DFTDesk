using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Filee.CrystalConverter;

/// <summary>
/// Filee Soft shell for the standalone crystal converter. Only pages with real
/// converter functionality are present; the original Filee app is unchanged.
/// </summary>
internal sealed partial class MainForm
{
    private static readonly Color Muted = Color.FromArgb(107, 106, 102);
    private static readonly Color Border = Color.FromArgb(226, 223, 220);
    private static readonly Color AccentFaint = Color.FromArgb(239, 237, 247);
    private static readonly Color AccentSoft = Color.FromArgb(223, 220, 246);
    private readonly Dictionary<string, Button> _navButtons = new();
    private readonly Dictionary<string, Control> _pages = new();
    private Panel? _pageHost;
    private Label? _pageHint;
    private ConversionWheel? _themePreviewWheel;
    private bool _stableEventsWired;

    private void BuildLayout()
    {
        SuspendLayout();
        Text = "Filee · 晶体转换";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1080, 760);
        MinimumSize = new Size(850, 630);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Background;
        ForeColor = TextColor;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        KeyPreview = true;
        AllowDrop = true;

        var shell = Grid(2, 1);
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(shell);

        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        sidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        };
        shell.Controls.Add(sidebar, 0, 0);
        CreateSidebar(sidebar);

        _pageHost = new Panel { Dock = DockStyle.Fill, BackColor = Background };
        shell.Controls.Add(_pageHost, 1, 0);
        AddPage("home", CreateHomePage());
        AddPage("batch", CreateBatchPage());
        AddPage("wheel", CreateWheelPage());
        AddPage("settings", CreateSettingsPage());
        AddPage("theme", CreateThemePage());
        AddPage("about", CreateAboutPage());
        Navigate("home");

        if (!_stableEventsWired)
        {
            _stableEventsWired = true;
            _wheel.FormatChosen += target => _ = StartBatchAsync(target);
            _wheel.AllowDrop = true;
            _wheel.DragEnter += WheelDragOver;
            _wheel.DragOver += WheelDragOver;
            _wheel.DragDrop += (_, e) =>
            {
                var target = _wheel.HitTest(_wheel.PointToClient(new Point(e.X, e.Y)));
                if (target is not { } format || e.Data?.GetData(DataFormats.FileDrop) is not string[] paths) return;
                AddFiles(paths, replace: true);
                _ = StartBatchAsync(format);
            };
            AcceptFileDrops(_files);
            _cancel.Click += (_, _) => _cancellation?.Cancel();
        }
        ResumeLayout(performLayout: true);
    }

    private void CreateSidebar(Panel sidebar)
    {
        var branding = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Color.White };
        sidebar.Controls.Add(branding);
        var logo = new PictureBox
        {
            Location = new Point(22, 22), Size = new Size(30, 30),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = LoadFileeLogo(),
        };
        branding.Controls.Add(logo);
        branding.Controls.Add(new Label
        {
            Text = "Filee", Location = new Point(60, 15), Size = new Size(132, 30),
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = TextColor,
        });
        branding.Controls.Add(new Label
        {
            Text = "拖一拖，放一放，转换完成。",
            Location = new Point(61, 47), Size = new Size(158, 20),
            Font = new Font("Microsoft YaHei UI", 7.8f),
            ForeColor = Muted,
        });

        var navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 340, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, Padding = new Padding(13, 0, 13, 0),
            BackColor = Color.White,
        };
        sidebar.Controls.Add(navigation);
        navigation.BringToFront();
        // FlowLayoutPanel is placed just below the brand, not over it.
        sidebar.Controls.SetChildIndex(branding, 0);
        AddNav(navigation, "⌂", "首页", "home");
        AddNav(navigation, "▤", "批量转换", "batch");
        AddNav(navigation, "◉", "环形工具栏", "wheel");
        AddNav(navigation, "⚙", "转换设置", "settings");
        AddNav(navigation, "●", "主题", "theme");
        AddNav(navigation, "ⓘ", "关于", "about");

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 78, BackColor = Color.White };
        var repo = FileeButton("GitHub", 94);
        repo.Location = new Point(18, 7);
        repo.Click += (_, _) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "https://github.com/kuanfener/Filee/pull/2") { UseShellExecute = true });
        };
        footer.Controls.Add(repo);
        footer.Controls.Add(new Label
        {
            Text = "独立晶体转换器 · POSCAR ⇄ CIF",
            Location = new Point(25, 45), Size = new Size(194, 26),
            Font = new Font("Microsoft YaHei UI", 8), ForeColor = Muted,
        });
        sidebar.Controls.Add(footer);
    }

    private static Image? LoadFileeLogo()
    {
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("Filee.CrystalConverter.FileeIcon.png");
        return stream is null ? null : new Bitmap(stream);
    }

    private void AddNav(FlowLayoutPanel sidebar, string icon, string text, string page)
    {
        var button = new Button
        {
            Text = $"  {icon}    {text}", Size = new Size(200, 39),
            Margin = new Padding(0, 1, 0, 4),
            FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.White, ForeColor = TextColor,
            Font = new Font("Microsoft YaHei UI", 9.5f), Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AccentFaint;
        button.Click += (_, _) => Navigate(page);
        sidebar.Controls.Add(button);
        _navButtons[page] = button;
    }

    private void AddPage(string id, Control page)
    {
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _pageHost!.Controls.Add(page);
        _pages[id] = page;
    }

    private void Navigate(string page)
    {
        foreach (var (id, item) in _pages) item.Visible = id == page;
        _pages[page].BringToFront();
        foreach (var (id, button) in _navButtons)
        {
            var selected = id == page;
            button.BackColor = selected ? AccentSoft : Color.White;
            button.ForeColor = selected ? Color.FromArgb(90, 83, 168) : TextColor;
            button.Font = new Font("Microsoft YaHei UI", 9.5f,
                selected ? FontStyle.Bold : FontStyle.Regular);
        }
    }

    private Control CreateHomePage()
    {
        var page = PageGrid(5);
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(PageLabel("首页", 19, true), 0, 0);

        var drop = new RoundedSurface
        {
            Dock = DockStyle.Fill, Radius = 23,
            FillColor = AccentFaint, StrokeColor = AccentSoft, BorderWidth = 2,
            Cursor = Cursors.Hand,
        };
        var dropContent = Grid(1, 5);
        dropContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        dropContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        dropContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        dropContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        dropContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        dropContent.Controls.Add(new UploadMark { Anchor = AnchorStyles.None, Size = new Size(66, 66) }, 0, 1);
        dropContent.Controls.Add(PageLabel("将文件拖放到这里", 12, true,
            ContentAlignment.MiddleCenter), 0, 2);
        dropContent.Controls.Add(PageLabel("或在资源管理器中按住 Ctrl 拖动文件，环形工具栏会在光标处打开。\n点击此处也可选择多个 CIF / POSCAR / CONTCAR 文件。",
            9, false, ContentAlignment.MiddleCenter, Muted), 0, 3);
        drop.Controls.Add(dropContent);
        page.Controls.Add(drop, 0, 1);

        // Like Filee's home page, a drop opens the donut instead of selecting a
        // default target format. Defer the popup until Explorer finishes its OLE drop.
        void Dropped(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths) return;
            AddFiles(paths, replace: true);
            if (_paths.Count > 0) BeginInvoke(new Action(ShowConversionWheelAtCursor));
        }
        void Entered(object? sender, DragEventArgs e)
        {
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                ? DragDropEffects.Copy : DragDropEffects.None;
            drop.StrokeColor = e.Effect == DragDropEffects.Copy ? Accent : AccentSoft;
            drop.Invalidate();
        }
        void Left(object? sender, EventArgs e) { drop.StrokeColor = AccentSoft; drop.Invalidate(); }
        void Clicked(object? sender, EventArgs e) => BrowseFiles();
        foreach (var control in new Control[] { drop, dropContent }.Concat(dropContent.Controls.Cast<Control>()))
        {
            control.AllowDrop = true;
            control.DragEnter += Entered;
            control.DragOver += Entered;
            control.DragDrop += Dropped;
            control.DragLeave += Left;
            control.Click += Clicked;
            control.Cursor = Cursors.Hand;
        }

        var recentHeader = Grid(2, 1);
        recentHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        recentHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        recentHeader.Controls.Add(PageLabel("最近的转换", 12, true), 0, 0);
        var clear = FileeButton("清除", 72);
        clear.Dock = DockStyle.Fill;
        clear.Click += (_, _) =>
        {
            if (_busy) return;
            _log.Items.Clear();
            _log.Visible = false;
            if (_recentEmpty is not null) _recentEmpty.Visible = true;
        };
        recentHeader.Controls.Add(clear, 1, 0);
        page.Controls.Add(recentHeader, 0, 3);

        var history = new Panel { Dock = DockStyle.Fill, BackColor = Background };
        _log.Dock = DockStyle.Fill;
        _log.BackColor = Background;
        _log.ForeColor = TextColor;
        _log.BorderStyle = BorderStyle.None;
        _log.Font = new Font("Microsoft YaHei UI", 9);
        _log.Visible = _log.Items.Count > 0;
        history.Controls.Add(_log);
        _recentEmpty = PageLabel("转换后的文件会显示在这里。", 9,
            color: Muted);
        _recentEmpty.Dock = DockStyle.Top;
        _recentEmpty.Height = 35;
        _recentEmpty.Visible = _log.Items.Count == 0;
        history.Controls.Add(_recentEmpty);
        page.Controls.Add(history, 0, 4);
        return page;
    }

    private Control CreateBatchPage()
    {
        var page = PageGrid(5);
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        page.Controls.Add(PageLabel("批量转换", 19, true), 0, 0);
        _selectedCount = PageLabel($"已选择 {_paths.Count} 个结构文件", 10, color: Muted);
        page.Controls.Add(_selectedCount, 0, 1);
        var filesCard = Card(16);
        var fileRows = Grid(1, 2);
        fileRows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        fileRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        _files.Dock = DockStyle.Fill;
        _files.BackColor = Color.FromArgb(251, 250, 248);
        _files.ForeColor = TextColor;
        _files.BorderStyle = BorderStyle.None;
        _files.SelectionMode = SelectionMode.MultiExtended;
        _files.Font = new Font("Consolas", 10);
        fileRows.Controls.Add(_files, 0, 0);
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty,
        };
        var add = FileeButton("＋ 添加文件", 134, filled: true);
        add.Click += (_, _) => BrowseFiles();
        row.Controls.Add(add);
        var remove = FileeButton("移除选中", 112);
        remove.Click += (_, _) => RemoveSelected();
        row.Controls.Add(remove);
        var clear = FileeButton("清空列表", 112);
        clear.Click += (_, _) => ClearFiles();
        row.Controls.Add(clear);
        fileRows.Controls.Add(row, 0, 1);
        filesCard.Controls.Add(fileRows);
        page.Controls.Add(filesCard, 0, 2);

        var convert = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Padding = new Padding(0, 16, 0, 0),
        };
        var toCif = FileeButton("导出 CIF", 150, filled: true);
        toCif.Click += (_, _) => _ = StartBatchAsync(ExportFormat.Cif);
        convert.Controls.Add(toCif);
        var toPoscar = FileeButton("导出 POSCAR", 170, filled: true);
        toPoscar.Click += (_, _) => _ = StartBatchAsync(ExportFormat.Poscar);
        convert.Controls.Add(toPoscar);
        var hint = PageLabel("目标格式相同的输入会自动跳过，源文件不会被覆盖。", 9, color: Muted);
        hint.Dock = DockStyle.None;
        hint.Size = new Size(350, 42);
        convert.Controls.Add(hint);
        page.Controls.Add(convert, 0, 3);

        var progressRow = Grid(3, 1);
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        _progress.Dock = DockStyle.Fill;
        _progress.Margin = new Padding(0, 14, 20, 14);
        progressRow.Controls.Add(_progress, 0, 0);
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleRight;
        _status.ForeColor = Muted;
        progressRow.Controls.Add(_status, 1, 0);
        _cancel.Text = "取消";
        _cancel.Dock = DockStyle.Fill;
        _cancel.FlatStyle = FlatStyle.Flat;
        _cancel.FlatAppearance.BorderColor = Border;
        _cancel.ForeColor = TextColor;
        _cancel.BackColor = Color.White;
        _cancel.Margin = new Padding(12, 4, 0, 4);
        _cancel.Enabled = false;
        progressRow.Controls.Add(_cancel, 2, 0);
        page.Controls.Add(progressRow, 0, 4);
        return page;
    }

    private Control CreateWheelPage()
    {
        var page = PageGrid(3);
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        page.Controls.Add(PageLabel("环形工具栏", 19, true), 0, 0);
        var preview = Card(22);
        var content = Grid(2, 1);
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _wheel.Anchor = AnchorStyles.None;
        _wheel.Size = new Size(270, 270);
        _wheel.BackColor = Color.White;
        content.Controls.Add(_wheel, 0, 0);
        var instructions = PageLabel("导出 CIF  /  导出 POSCAR\n\n① 资源管理器中选择一个或多个文件。\n② 按住 Ctrl + 左键拖动到轮盘扇区。\n③ 松开鼠标，执行批量转换。\n\n也可以按 Ctrl + Alt + Space 呼出轮盘后直接点击。", 11, color: Muted);
        content.Controls.Add(instructions, 1, 0);
        preview.Controls.Add(content);
        page.Controls.Add(preview, 0, 1);
        page.Controls.Add(PageLabel("提示：若 Windows 显示“×”，请检查资源管理器与转换器是否以相同权限运行。", 9,
            color: Muted), 0, 2);
        return page;
    }

    private Control CreateSettingsPage()
    {
        var page = PageGrid(4);
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 198));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(PageLabel("转换设置", 19, true), 0, 0);
        var card = Card(24);
        var rows = Grid(1, 4);
        rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        rows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rows.Controls.Add(PageLabel("输出目录", 11, true), 0, 0);
        var path = Grid(2, 1);
        path.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        path.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        _output.Dock = DockStyle.Fill;
        _output.BackColor = Color.FromArgb(251, 250, 248);
        _output.ForeColor = TextColor;
        _output.BorderStyle = BorderStyle.FixedSingle;
        _output.Margin = new Padding(0, 6, 12, 6);
        path.Controls.Add(_output, 0, 0);
        var browse = FileeButton("浏览…", 113);
        browse.Dock = DockStyle.Fill;
        browse.Click += (_, _) => BrowseFolder();
        path.Controls.Add(browse, 1, 0);
        rows.Controls.Add(path, 0, 1);
        rows.Controls.Add(PageLabel("留空时保存到源文件夹；如已存在同名文件，自动使用序号避让。", 9, color: Muted), 0, 2);
        _cartesian.Text = "POSCAR 使用 Cartesian 坐标（默认 Direct）";
        _cartesian.Dock = DockStyle.Fill;
        _cartesian.BackColor = Color.White;
        _cartesian.ForeColor = TextColor;
        rows.Controls.Add(_cartesian, 0, 3);
        card.Controls.Add(rows);
        page.Controls.Add(card, 0, 1);
        page.Controls.Add(PageLabel("数值输出参考 VESTA：晶格及晶胞参数固定 10 位小数，原子坐标固定 8 位小数。", 9,
            color: Muted), 0, 3);
        return page;
    }

    private Control CreateThemePage()
    {
        var page = PageGrid(2);
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(PageLabel("主题", 19, true), 0, 0);
        var sections = Grid(2, 1);
        sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63));
        sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));
        page.Controls.Add(sections, 0, 1);

        var themeDetails = Grid(1, 3);
        themeDetails.Margin = new Padding(0, 0, 18, 0);
        themeDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 146));
        themeDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        themeDetails.RowStyles.Add(new RowStyle(SizeType.Absolute, 218));
        var styleCard = Card(18);
        var styleRows = Grid(1, 2);
        styleRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        styleRows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        styleRows.Controls.Add(PageLabel("主题样式", 10, color: Muted), 0, 0);
        var soft = new RoundedSurface
        {
            FillColor = AccentFaint, StrokeColor = Accent, Radius = 14,
            BorderWidth = 2, Dock = DockStyle.Fill,
        };
        soft.Controls.Add(PageLabel("Filee Soft\n经典 Filee 外观，柔和的圆角与浅色表面。", 10, true,
            color: Color.FromArgb(92, 84, 166)));
        styleRows.Controls.Add(soft, 0, 1);
        styleCard.Controls.Add(styleRows);
        themeDetails.Controls.Add(styleCard, 0, 0);

        var colors = Card(20);
        var colorRows = Grid(1, 3);
        colorRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        colorRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        colorRows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        colorRows.Controls.Add(PageLabel("强调色", 11, true), 0, 0);
        var swatches = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        foreach (var color in new[]
        {
            Color.FromArgb(127, 119, 221), Color.FromArgb(27, 155, 121),
            Color.FromArgb(225, 89, 52), Color.FromArgb(53, 135, 219),
            Color.FromArgb(215, 73, 123), Color.FromArgb(191, 123, 15),
        })
        {
            var swatch = new Button
            {
                Size = new Size(30, 30), Margin = new Padding(2, 0, 8, 0),
                BackColor = color, ForeColor = Color.White, Text = color == Accent ? "✓" : "",
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            };
            swatch.FlatAppearance.BorderSize = color == Accent ? 2 : 0;
            swatch.FlatAppearance.BorderColor = TextColor;
            swatch.Click += (_, _) => SelectAccent(color, swatches);
            swatches.Controls.Add(swatch);
        }
        colorRows.Controls.Add(swatches, 0, 1);
        colorRows.Controls.Add(PageLabel("独立转换器沿用 Filee Soft 的浅色布局。\n修改强调色会同步影响弹出轮盘和导出按钮。",
            9, color: Muted), 0, 2);
        colors.Controls.Add(colorRows);
        themeDetails.Controls.Add(colors, 0, 2);
        sections.Controls.Add(themeDetails, 0, 0);

        var previewCard = Card(18);
        var previewRows = Grid(1, 3);
        previewRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        previewRows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previewRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        previewRows.Controls.Add(PageLabel("预览", 10, color: Muted), 0, 0);
        var previewArea = new RoundedSurface
        {
            Dock = DockStyle.Fill, Radius = 22,
            FillColor = Color.FromArgb(221, 217, 249),
            StrokeColor = Color.FromArgb(221, 217, 249),
        };
        var previewWheel = new ConversionWheel
        {
            Size = new Size(255, 255), Anchor = AnchorStyles.None,
            BackColor = previewArea.FillColor,
        };
        _themePreviewWheel = previewWheel;
        previewArea.Controls.Add(previewWheel);
        previewArea.Resize += (_, _) =>
            previewWheel.Location = new Point((previewArea.Width - previewWheel.Width) / 2,
                (previewArea.Height - previewWheel.Height) / 2);
        previewRows.Controls.Add(previewArea, 0, 1);
        previewRows.Controls.Add(PageLabel("POSCAR / CIF · 双扇区轮盘", 9,
            align: ContentAlignment.MiddleCenter, color: Muted), 0, 2);
        previewCard.Controls.Add(previewRows);
        sections.Controls.Add(previewCard, 1, 0);
        _pageHint = previewRows.Controls[2] as Label;
        return page;
    }

    private void SelectAccent(Color color, FlowLayoutPanel swatches)
    {
        Accent = color;
        _wheel.AccentColor = color;
        _popup.AccentColor = color;
        if (_themePreviewWheel is not null) _themePreviewWheel.AccentColor = color;
        foreach (var button in swatches.Controls.OfType<Button>())
        {
            var selected = button.BackColor == color;
            button.Text = selected ? "✓" : "";
            button.FlatAppearance.BorderSize = selected ? 2 : 0;
        }
        if (_pageHint is not null)
            _pageHint.Text = $"强调色已更新为 #{color.R:X2}{color.G:X2}{color.B:X2}，弹出轮盘同步生效。";
    }

    private Control CreateAboutPage()
    {
        var page = PageGrid(2);
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        page.Controls.Add(PageLabel("关于", 19, true), 0, 0);
        var card = Card(25);
        card.Controls.Add(PageLabel("Filee 晶体转换器\n\nWindows 独立程序 · POSCAR / CONTCAR ⇄ CIF\n支持批量导出、Filee 风格轮盘、Direct / Cartesian 与 VESTA 风格的数值排版。\n\n不会修改原版 Filee 主程序。", 11));
        page.Controls.Add(card, 0, 1);
        return page;
    }

    private void WheelDragOver(object? sender, DragEventArgs e)
    {
        var target = _wheel.HitTest(_wheel.PointToClient(new Point(e.X, e.Y)));
        _wheel.SetHighlight(target);
        e.Effect = target is not null && e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private static void AcceptFileDrops(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private static TableLayoutPanel Grid(int columns, int rows) =>
        new()
        {
            ColumnCount = columns, RowCount = rows, Dock = DockStyle.Fill,
            Margin = Padding.Empty, Padding = Padding.Empty,
        };

    private static TableLayoutPanel PageGrid(int rows)
    {
        var grid = Grid(1, rows);
        grid.BackColor = Background;
        grid.Padding = new Padding(32, 24, 32, 24);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return grid;
    }

    private static Label PageLabel(string text, float size, bool bold = false,
        ContentAlignment align = ContentAlignment.MiddleLeft, Color? color = null) =>
        new()
        {
            Text = text, Dock = DockStyle.Fill, Margin = Padding.Empty,
            AutoEllipsis = false, TextAlign = align, ForeColor = color ?? TextColor,
            Font = new Font("Microsoft YaHei UI", size,
                bold ? FontStyle.Bold : FontStyle.Regular),
        };

    private static RoundedSurface Card(int padding = 16) =>
        new() { Dock = DockStyle.Fill, Radius = 16, Padding = new Padding(padding),
            FillColor = Color.White, StrokeColor = Border };

    private static Button FileeButton(string label, int width, bool filled = false)
    {
        var button = new Button
        {
            Text = label, Width = width, Height = 36,
            Margin = new Padding(0, 3, 10, 3),
            FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false,
            BackColor = filled ? Accent : Color.White,
            ForeColor = filled ? Color.White : TextColor,
            Font = new Font("Microsoft YaHei UI", 9),
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderColor = filled ? Accent : Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = filled ? Color.FromArgb(155, 145, 227) : AccentFaint;
        return button;
    }
}

/// <summary>Soft filled cards, matching the rounded surfaces in Filee's Home and Theme pages.</summary>
internal sealed class RoundedSurface : Panel
{
    public int Radius { get; set; } = 16;
    public Color FillColor { get; set; } = Color.White;
    public Color StrokeColor { get; set; } = Color.FromArgb(226, 223, 220);
    public float BorderWidth { get; set; } = 1;

    public RoundedSurface()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    private GraphicsPath Outline()
    {
        var w = Math.Max(2, Width - 2);
        var h = Math.Max(2, Height - 2);
        var d = Math.Min(Radius * 2, Math.Min(w, h));
        var p = new GraphicsPath();
        p.AddArc(1, 1, d, d, 180, 90);
        p.AddArc(w - d, 1, d, d, 270, 90);
        p.AddArc(w - d, h - d, d, d, 0, 90);
        p.AddArc(1, h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Color.White);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var outline = Outline();
        using var fill = new SolidBrush(FillColor);
        e.Graphics.FillPath(fill, outline);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var outline = Outline();
        using var pen = new Pen(StrokeColor, BorderWidth);
        e.Graphics.DrawPath(pen, outline);
    }
}

/// <summary>The round upload glyph from Filee's light Home drop zone.</summary>
internal sealed class UploadMark : Control
{
    public UploadMark()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var soft = new SolidBrush(Color.FromArgb(219, 215, 241));
        using var violet = new SolidBrush(Color.FromArgb(101, 93, 178));
        var s = Math.Min(Width, Height);
        g.FillEllipse(soft, (Width - s) / 2f, (Height - s) / 2f, s, s);
        var cx = Width / 2f;
        var cy = Height / 2f;
        var arrow = new PointF[] { new(cx, cy - 16), new(cx - 13, cy - 2),
            new(cx - 6, cy - 2), new(cx - 6, cy + 11),
            new(cx + 6, cy + 11), new(cx + 6, cy - 2), new(cx + 13, cy - 2) };
        g.FillPolygon(violet, arrow);
        g.FillRectangle(violet, cx - 13, cy + 15, 26, 4);
    }
}
