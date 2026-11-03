using System.Drawing;
using System.Windows.Forms;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Filee.CrystalConverter;

/// <summary>
/// The converter logic still uses the proven WinForms message loop for global
/// Explorer hooks and OLE file transfer. Every visible part of the main window
/// is instead rendered by the original Filee Avalonia style system.
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
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://DFTDesk/Assets/Icons/filee.ico"))),
            Content = _shell,
        };
        _avaloniaWindow.Closing += (_, e) =>
        {
            if (!_busy) return;
            _cancellation?.Cancel();
            e.Cancel = true;
            Log("正在取消转换，请等待任务结束后关闭窗口。");
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
        _shell.FilesDropped += paths => AddFiles(paths, replace: true);
        _shell.HomeFilesDropped += paths =>
        {
            AddFiles(paths, replace: true);
            if (_paths.Count > 0)
                BeginInvoke(new Action(ShowConversionWheelAtCursor));
        };
        _shell.FormatChosen += target => _ = StartBatchAsync(target);
        _shell.AseCheckRequested += () => _ = CheckAseAsync();
        _shell.OutputDirectoryChanged += directory => _output.Text = directory;
        _shell.CartesianChanged += selected => _cartesian.Checked = selected;
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
