using System.Drawing;
using System.Windows.Forms;

namespace DFTDesk;

internal sealed partial class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(249, 248, 245);
    private static readonly Color PanelColor = Color.White;
    private static readonly Color TextColor = Color.FromArgb(31, 30, 29);
    private static Color Accent = Color.FromArgb(127, 119, 221);

    private readonly ListBox _files = new();
    private readonly ListBox _log = new();
    private readonly TextBox _output = new();
    private readonly CheckBox _cartesian = new();
    private readonly Label _status = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _cancel = new();
    private readonly DftDonutHost _wheel = new();
    private readonly DftDonutPopup _popup = new();
    private readonly GlobalTriggerHost _global = new();
    private readonly System.Windows.Forms.Timer _dragGraceTimer = new() { Interval = 350 };
    private readonly List<string> _paths = [];
    private CancellationTokenSource? _cancellation;
    private bool _busy;
    private bool _popupFromDrag;
    private Label? _recentEmpty;
    private Label? _selectedCount;

    public MainForm()
    {
        BuildLayout();

        _popup.FormatChosen += target =>
        {
            _dragGraceTimer.Stop();
            _popup.Hide();
            _popupFromDrag = false;
            _ = StartBatchAsync(target);
        };
        _popup.FilesDropped += (target, files) =>
        {
            _dragGraceTimer.Stop();
            _popup.Hide();
            _popupFromDrag = false;
            AddFiles(files, replace: true);
            _ = StartBatchAsync(target);
        };
        _dragGraceTimer.Tick += (_, _) =>
        {
            _dragGraceTimer.Stop();
            if (_popupFromDrag) _popup.Hide();
            _popupFromDrag = false;
        };
        _global.DragStarted += (x, y) => Post(() => OpenExplorerWheel(new Point(x, y), dragMode: true));
        _global.DragEnded += (x, y) => Post(() => EndExplorerDrag(new Point(x, y)));
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
            if (!_busy && !_clusterBusy && !_dosBusy) return;
            _cancellation?.Cancel();
            _clusterCancellation?.Cancel();
            _dosCancellation?.Cancel();
            e.Cancel = true;
            Log("正在取消任务，请等待任务完成后再关闭窗口。");
        };
        FormClosed += (_, _) =>
        {
            _cancellation?.Cancel();
            _clusterCancellation?.Cancel();
            _dosCancellation?.Cancel();
            DisposeDosResult();
            _dragGraceTimer.Dispose();
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
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            AddFiles(dialog.FileNames);
        }
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择批量转换结果的保存文件夹" };
        if (dialog.ShowDialog() == DialogResult.OK) _output.Text = dialog.SelectedPath;
    }

    private void AddFiles(IEnumerable<string> paths, bool replace = false)
    {
        if (_busy) return;
        if (replace) _paths.Clear();
        var seen = _paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (!File.Exists(path) || CrystalFiles.Detect(path) is null || !seen.Add(path)) continue;
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
        _shell.SetFiles(_paths);
        if (_selectedCount is not null)
            _selectedCount.Text = $"已选择 {_paths.Count} 个结构文件";
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

    private void OpenExplorerWheel(Point at, bool dragMode)
    {
        if (_busy || _popup.Visible) return;
        var selection = _global.SelectedFiles();
        if (!selection.Any(path => CrystalFiles.Detect(path) is not null)) return;
        AddFiles(selection, replace: true);
        _popupFromDrag = dragMode;
        _popup.OpenAt(at, activate: !dragMode);
    }

    private void EndExplorerDrag(Point at)
    {
        if (!_popup.Visible || !_popupFromDrag) return;
        // The OLE Drop event can arrive slightly after the global mouse-up.
        // Do not hide the drop target first or Explorer will show "×" and lose the drop.
        _dragGraceTimer.Stop();
        _dragGraceTimer.Start();
    }

    private void Post(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); }
        catch (InvalidOperationException) { /* Window has just been closed. */ }
    }

    private async Task CheckAseAsync()
    {
        var python = _shell.PythonExecutablePath;
        _shell.SetAseStatus("正在检测 Python / ASE…");
        try
        {
            var version = await AseConversion.ProbeAsync(python);
            _shell.SetAseStatus($"可用：{version}（{python}）");
        }
        catch (Exception ex)
        {
            _shell.SetAseStatus("ASE 不可用：" + ex.Message);
        }
    }

    private async Task StartBatchAsync(ExportFormat target)
    {
        if (_busy || _clusterBusy) return;
        var inputs = _paths.Where(File.Exists).Where(path => CrystalFiles.Detect(path) != target).ToArray();
        if (inputs.Length == 0)
        {
            MessageBox.Show("请先添加需要转换的文件；已是目标格式的文件会自动跳过。",
                "没有待转换的文件", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var directory = string.IsNullOrWhiteSpace(_output.Text) ? null : _output.Text.Trim();
        var cartesian = _cartesian.Checked;
        var python = _shell.PythonExecutablePath;
        if (directory is not null && !Directory.Exists(directory))
        {
            MessageBox.Show("输出文件夹不存在，请重新选择。", "输出目录错误",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _shell.SetAseStatus("正在检查 ASE 环境…");
        try
        {
            var version = await AseConversion.ProbeAsync(python);
            _shell.SetAseStatus($"可用：{version}（{python}）");
        }
        catch (Exception ex)
        {
            _shell.SetAseStatus("ASE 不可用：" + ex.Message);
            MessageBox.Show("ASE 工作引擎不可用：" + ex.Message +
                "\n\n请打开左侧「工作引擎」，检查 Python 路径并确认已安装 ASE。",
                "ASE 环境检查", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _busy = true;
        _shell.SetBusy(true);
        _cancellation = new CancellationTokenSource();
        _cancel.Enabled = true;
        _progress.Maximum = inputs.Length;
        _progress.Value = 0;
        var success = 0;
        var failed = 0;
        Log($"开始批量导出 {(target == ExportFormat.Cif ? "CIF" : "POSCAR")}，共 {inputs.Length} 个文件，工作引擎：ASE。");
        try
        {
            foreach (var input in inputs)
            {
                if (_cancellation.IsCancellationRequested) break;
                try
                {
                    var output = await AseConversion.ConvertOneAsync(input, directory, target, cartesian, python,
                        _cancellation.Token);
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
                _shell.SetProgress((double)(success + failed) / inputs.Length);
            }
        }
        finally
        {
            var cancelled = _cancellation.IsCancellationRequested;
            _cancellation.Dispose();
            _cancellation = null;
            _busy = false;
            _shell.SetBusy(false);
            _cancel.Enabled = false;
            Log($"完成：成功 {success}，失败 {failed}" + (cancelled ? "（已取消）" : ""));
            UpdateStatus();
        }
    }

    private void Log(string message)
    {
        _log.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        _shell.AppendRecord($"[{DateTime.Now:HH:mm:ss}] {message}");
        if (_log.Items.Count > 300) _log.Items.RemoveAt(0);
        _log.TopIndex = _log.Items.Count - 1;
        _log.Visible = true;
        if (_recentEmpty is not null)
            _recentEmpty.Visible = false;
    }

    private void UpdateStatus()
    {
        _status.Text = _busy ? "正在转换…" : $"已选择 {_paths.Count} 个文件";
        _shell.SetStatus(_status.Text);
    }
}
