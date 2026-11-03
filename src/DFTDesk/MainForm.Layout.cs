using System.Drawing;
using System.Windows.Forms;
using Avalonia.Controls;
using Avalonia.Platform;

namespace DFTDesk;

/// <summary>
/// The converter logic still uses the proven WinForms message loop for global
/// Explorer hooks and OLE file transfer. Every visible part of the main window
/// is instead rendered by the MIT-licensed Avalonia style system.
/// </summary>
internal sealed partial class MainForm
{
    private CrystalShell _shell = null!;
    private Avalonia.Controls.Window _avaloniaWindow = null!;

    private void BuildLayout()
    {
        SuspendLayout();
        Text = "DFTDesk background bridge";
        ShowInTaskbar = false;
        Opacity = 0;
        ClientSize = new System.Drawing.Size(1, 1);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        AllowDrop = false;

        _shell = new CrystalShell();
        _avaloniaWindow = new Avalonia.Controls.Window
        {
            Title = "DFTDesk",
            Width = 1080,
            Height = 760,
            MinWidth = 820,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://DFTDesk/Assets/Icons/dftdesk.ico"))),
            Content = _shell,
        };
        var initialSize = _shell.StartupWindowSize;
        _avaloniaWindow.Width = initialSize.Width;
        _avaloniaWindow.Height = initialSize.Height;
        _shell.WindowSizeRequested += (width, height) =>
        {
            _avaloniaWindow.Width = width;
            _avaloniaWindow.Height = height;
        };
        _avaloniaWindow.Closing += (_, e) =>
        {
            if (!_busy && !_clusterBusy) return;
            _cancellation?.Cancel();
            _clusterCancellation?.Cancel();
            e.Cancel = true;
            Log("正在取消任务，请等待运行结束后关闭窗口。");
        };
        _avaloniaWindow.Closed += (_, _) => Close();
        Shown += (_, _) =>
        {
            _avaloniaWindow.Show();
            Hide();
        };

        _shell.BrowseFilesRequested += BrowseFiles;
        _shell.BrowseFolderRequested += BrowseFolder;
        _shell.ClearFilesRequested += ClearFiles;
        _shell.RemoveFilesRequested += removed =>
        {
            if (_busy) return;
            foreach (var path in removed) _paths.Remove(path);
            RefreshFiles();
        };
        _shell.HomeFilesDropped += paths => AddFiles(paths, replace: true);
        _shell.FormatChosen += target => _ = StartBatchAsync(target);
        _shell.AseCheckRequested += () => _ = CheckAseAsync();
        _shell.ClusterBrowseRequested += BrowseClusterFolder;
        _shell.ClusterGenerateRequested += () => _ = GenerateClusterAsync();
        _shell.ClusterCancelRequested += () => _clusterCancellation?.Cancel();
        _shell.OutputDirectoryChanged += directory => _output.Text = directory;
        _shell.CartesianChanged += selected => _cartesian.Checked = selected;
        // Synchronize the user-selected ASE output coordinate mode before the first conversion.
        _cartesian.Checked = _shell.Cartesian;
        _shell.CancelRequested += () => _cancellation?.Cancel();
        _shell.ThemeChanged += (accent, opacity) =>
        {
            Accent = accent;
            _popup.AccentColor = accent;
            _popup.SliceOpacity = opacity;
        };
        _popup.AccentColor = _shell.CurrentAccent;
        _popup.SliceOpacity = _shell.CurrentOpacity;
        _cancel.Click += (_, _) => _cancellation?.Cancel();
        ResumeLayout(performLayout: true);
    }
}
