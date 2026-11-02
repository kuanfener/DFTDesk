using System.Drawing;
using System.Windows.Forms;

namespace Filee.CrystalConverter;

internal sealed class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(16, 20, 33);
    private static readonly Color PanelColor = Color.FromArgb(25, 31, 48);
    private static readonly Color TextColor = Color.FromArgb(230, 237, 247);
    private static readonly Color Accent = Color.FromArgb(97, 205, 179);

    private readonly ListBox _files = new();
    private readonly ListBox _log = new();
    private readonly TextBox _output = new();
    private readonly CheckBox _cartesian = new();
    private readonly Label _status = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _cancel = new();
    private readonly ConversionWheel _wheel = new();
    private readonly WheelPopup _popup = new();
    private readonly GlobalTriggerHost _global = new();
    private readonly List<string> _paths = [];
    private CancellationTokenSource? _cancellation;
    private bool _busy;

    public MainForm()
    {
        Text = "Filee Crystal · POSCAR ↔ CIF";
        ClientSize = new Size(960, 715);
        MinimumSize = new Size(960, 715);
        MaximumSize = new Size(960, 715);
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Background;
        ForeColor = TextColor;
        Font = new Font("Microsoft YaHei UI", 10);
        KeyPreview = true;
        AllowDrop = true;

        Controls.Add(TextLabel("Filee Crystal", 29, 20, 440, 47, 24, Accent, true));
        Controls.Add(TextLabel("独立晶体结构转换器 · VASP POSCAR / CONTCAR ↔ CIF", 31, 71, 720, 30, 10));
        Controls.Add(TextLabel("Ctrl + 左键拖动（资源管理器）  /  Ctrl + Alt + Space", 580, 26, 350, 57, 9,
            Color.FromArgb(168, 180, 201)));

        var left = Card(25, 123, 566, 285);
        Controls.Add(left);
        left.Controls.Add(TextLabel("结构文件", 18, 11, 220, 29, 12, TextColor, true));
        left.Controls.Add(TextLabel("支持多选、拖放；无扩展名 POSCAR / CONTCAR 可直接识别", 19, 40, 520, 26, 9,
            Color.FromArgb(170, 185, 203)));
        _files.Location = new Point(17, 73);
        _files.Size = new Size(530, 154);
        _files.BorderStyle = BorderStyle.None;
        _files.SelectionMode = SelectionMode.MultiExtended;
        _files.BackColor = Color.FromArgb(18, 24, 38);
        _files.ForeColor = TextColor;
        _files.Font = new Font("Consolas", 10);
        left.Controls.Add(_files);
        left.Controls.Add(ActionButton("＋ 添加文件", 17, 236, 145, 35, (_, _) => BrowseFiles()));
        left.Controls.Add(ActionButton("移除选中", 173, 236, 112, 35, (_, _) => RemoveSelected()));
        left.Controls.Add(ActionButton("清空列表", 295, 236, 112, 35, (_, _) => ClearFiles()));

        Controls.Add(TextLabel("选择导出格式", 651, 111, 260, 29, 12, TextColor, true));
        _wheel.Location = new Point(616, 138);
        _wheel.FormatChosen += target => _ = StartBatchAsync(target);
        Controls.Add(_wheel);

        var options = Card(25, 427, 909, 93);
        Controls.Add(options);
        options.Controls.Add(TextLabel("输出目录", 17, 11, 130, 24, 10, TextColor, true));
        _output.Location = new Point(130, 10);
        _output.Size = new Size(610, 27);
        _output.BackColor = Color.FromArgb(18, 24, 38);
        _output.ForeColor = TextColor;
        _output.BorderStyle = BorderStyle.FixedSingle;
        options.Controls.Add(_output);
        options.Controls.Add(ActionButton("浏览…", 752, 8, 131, 32, (_, _) => BrowseFolder()));
        options.Controls.Add(TextLabel("留空则输出到原文件所在文件夹，自动避开已有同名文件", 17, 52, 530, 25, 9,
            Color.FromArgb(170, 185, 203)));
        _cartesian.Text = "POSCAR 输出用 Cartesian 坐标（默认 Direct）";
        _cartesian.Location = new Point(540, 50);
        _cartesian.Size = new Size(345, 29);
        _cartesian.ForeColor = TextColor;
        options.Controls.Add(_cartesian);

        Controls.Add(TextLabel("批量导出记录", 29, 536, 250, 27, 11, TextColor, true));
        _log.Location = new Point(25, 566);
        _log.Size = new Size(909, 93);
        _log.BackColor = PanelColor;
        _log.ForeColor = TextColor;
        _log.BorderStyle = BorderStyle.None;
        _log.Font = new Font("Consolas", 9);
        Controls.Add(_log);

        _progress.Location = new Point(25, 671);
        _progress.Size = new Size(585, 13);
        Controls.Add(_progress);
        _status.Location = new Point(621, 663);
        _status.Size = new Size(230, 30);
        _status.ForeColor = Color.FromArgb(170, 185, 203);
        _status.TextAlign = ContentAlignment.MiddleRight;
        Controls.Add(_status);
        _cancel = ActionButton("取消", 866, 662, 68, 31, (_, _) => _cancellation?.Cancel());
        _cancel.Enabled = false;
        Controls.Add(_cancel);

        _popup.FormatChosen += target =>
        {
            _popup.Hide();
            _ = StartBatchAsync(target);
        };
        _global.DragStarted += (x, y) => Post(() => OpenExplorerWheel(new Point(x, y)));
        _global.DragEnded += (x, y) => Post(() => EndExplorerDrag(new Point(x, y)));
        _global.ShortcutPressed += (x, y) => Post(() => OpenExplorerWheel(new Point(x, y)));
        _global.Failure += error => Post(() => Log("全局手势不可用：" + error));

        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths) AddFiles(paths);
        };
        Shown += (_, _) => _global.Start();
        FormClosing += (_, e) =>
        {
            if (!_busy) return;
            _cancellation?.Cancel();
            e.Cancel = true;
            Log("正在取消转换，请完成后再关闭窗口。");
        };
        FormClosed += (_, _) =>
        {
            _cancellation?.Cancel();
            _global.Dispose();
            _popup.Dispose();
        };
        UpdateStatus();
    }

    private static Panel Card(int x, int y, int width, int height) =>
        new() { Location = new Point(x, y), Size = new Size(width, height), BackColor = PanelColor };

    private static Label TextLabel(string text, int x, int y, int width, int height, float size,
        Color? color = null, bool bold = false) => new()
        {
            Text = text, Location = new Point(x, y), Size = new Size(width, height),
            ForeColor = color ?? TextColor,
            Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
        };

    private static Button ActionButton(string label, int x, int y, int width, int height, EventHandler click)
    {
        var button = new Button
        {
            Text = label, Location = new Point(x, y), Size = new Size(width, height),
            BackColor = Color.FromArgb(49, 68, 92), ForeColor = TextColor,
            FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        return button;
    }

    private void BrowseFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 POSCAR / CONTCAR / CIF 文件（可多选）",
            Filter = "晶体结构 (*.cif;*.vasp;*.poscar)|*.cif;*.vasp;*.poscar|所有文件 (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) AddFiles(dialog.FileNames);
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择批量转换结果的保存文件夹" };
        if (dialog.ShowDialog(this) == DialogResult.OK) _output.Text = dialog.SelectedPath;
    }

    private void AddFiles(IEnumerable<string> paths, bool replace = false)
    {
        if (_busy) return;
        if (replace) _paths.Clear();
        var seen = _paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (!File.Exists(path) || ConversionBatch.Detect(path) is null || !seen.Add(path)) continue;
            _paths.Add(path);
        }
        RefreshFiles();
    }

    private void RefreshFiles()
    {
        _files.BeginUpdate();
        _files.Items.Clear();
        foreach (var path in _paths) _files.Items.Add(path);
        _files.EndUpdate();
        UpdateStatus();
    }

    private void RemoveSelected()
    {
        if (_busy) return;
        foreach (var selected in _files.SelectedItems.Cast<string>().ToArray()) _paths.Remove(selected);
        RefreshFiles();
    }

    private void ClearFiles()
    {
        if (_busy) return;
        _paths.Clear();
        RefreshFiles();
    }

    private void OpenExplorerWheel(Point at)
    {
        if (_busy || _popup.Visible) return;
        var selection = _global.SelectedFiles();
        if (!selection.Any(path => ConversionBatch.Detect(path) is not null)) return;
        AddFiles(selection, replace: true);
        _popup.OpenAt(at);
    }

    private void EndExplorerDrag(Point at)
    {
        if (!_popup.Visible) return;
        var choice = _popup.HitScreenPoint(at);
        _popup.Hide();
        if (choice is { } target) _ = StartBatchAsync(target);
    }

    private void Post(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); }
        catch (InvalidOperationException) { /* Window has just been closed. */ }
    }

    private async Task StartBatchAsync(ExportFormat target)
    {
        if (_busy) return;
        var inputs = _paths.Where(File.Exists).Where(path => ConversionBatch.Detect(path) != target).ToArray();
        if (inputs.Length == 0)
        {
            MessageBox.Show(this, "请先添加需要转换的文件；已是目标格式的文件会自动跳过。",
                "没有待转换的文件", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var directory = string.IsNullOrWhiteSpace(_output.Text) ? null : _output.Text.Trim();
        var cartesian = _cartesian.Checked;
        if (directory is not null && !Directory.Exists(directory))
        {
            MessageBox.Show(this, "输出文件夹不存在，请重新选择。", "输出目录错误",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _busy = true;
        _cancellation = new CancellationTokenSource();
        _cancel.Enabled = true;
        _progress.Maximum = inputs.Length;
        _progress.Value = 0;
        var success = 0;
        var failed = 0;
        Log($"开始批量导出 {(target == ExportFormat.Cif ? "CIF" : "POSCAR")}，共 {inputs.Length} 个文件。");
        try
        {
            foreach (var input in inputs)
            {
                if (_cancellation.IsCancellationRequested) break;
                try
                {
                    var output = await Task.Run(() => ConversionBatch.ConvertOne(input, directory, target,
                        cartesian), _cancellation.Token);
                    success++;
                    Log($"✓ {Path.GetFileName(input)} → {Path.GetFileName(output)}");
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    failed++;
                    Log($"✕ {Path.GetFileName(input)}：{ex.Message}");
                }
                _progress.Value = Math.Min(inputs.Length, success + failed);
            }
        }
        finally
        {
            var cancelled = _cancellation.IsCancellationRequested;
            _cancellation.Dispose();
            _cancellation = null;
            _busy = false;
            _cancel.Enabled = false;
            Log($"完成：成功 {success}，失败 {failed}" + (cancelled ? "（已取消）" : ""));
            UpdateStatus();
        }
    }

    private void Log(string message)
    {
        _log.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        if (_log.Items.Count > 300) _log.Items.RemoveAt(0);
        _log.TopIndex = _log.Items.Count - 1;
    }

    private void UpdateStatus() => _status.Text = _busy ? "正在转换…" : $"已选择 {_paths.Count} 个文件";
}
