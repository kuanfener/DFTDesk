using System.Drawing;
using System.Windows.Forms;
using Avalonia.Win32.Interoperability;

namespace Filee.CrystalConverter;

/// <summary>
/// The converter logic still uses the proven WinForms message loop for global
/// Explorer hooks and OLE file transfer. Every visible part of the main window
/// is instead rendered by the original Filee Avalonia style system.
/// </summary>
internal sealed partial class MainForm
{
    private CrystalShell _shell = null!;

    private void BuildLayout()
    {
        SuspendLayout();
        Text = "Filee";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1080, 760);
        MinimumSize = new Size(820, 560);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        AllowDrop = true;

        _shell = new CrystalShell();
        Controls.Add(new WinFormsAvaloniaControlHost
        {
            Dock = DockStyle.Fill,
            Content = _shell,
        });

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
