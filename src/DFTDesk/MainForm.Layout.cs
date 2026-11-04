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
            if (!_busy && !_clusterBusy && !_dosBusy && !_cohpBusy && !_stepBusy && !_surfaceBusy) return;
            _cancellation?.Cancel();
            _clusterCancellation?.Cancel();
            _dosCancellation?.Cancel();
            _cohpCancellation?.Cancel();
            _stepCancellation?.Cancel();
            _surfaceCancellation?.Cancel();
            e.Cancel = true;
            Log("正在取消任务，请等待运行结束后关闭窗口。");
        };
        _avaloniaWindow.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Controls.Window.WindowStateProperty &&
                _avaloniaWindow.WindowState == Avalonia.Controls.WindowState.Minimized &&
                CanMinimizeToTray)
                HideToTray();
        };
        _avaloniaWindow.Closed += (_, _) => Close();
        Shown += (_, _) =>
        {
            if (_startHiddenInTray && CanMinimizeToTray)
            {
                _avaloniaWindow.ShowInTaskbar = false;
                _avaloniaWindow.WindowState = Avalonia.Controls.WindowState.Minimized;
                _avaloniaWindow.Show();
                _avaloniaWindow.Hide();
            }
            else
            {
                _avaloniaWindow.Show();
            }
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
        // Dropping more files into the unified import/list card adds to its batch;
        // only the Explorer wheel intentionally replaces the selected batch.
        _shell.HomeFilesDropped += paths => AddFiles(paths);
        _shell.FormatChosen += target => _ = StartBatchAsync(target);
        _shell.AseCheckRequested += () => _ = CheckAseAsync();
        _shell.StepBrowseRequested += BrowseStepFile;
        _shell.StepClearRequested += ClearStepFile;
        _shell.StepTemplateRequested += () => _ = SaveStepTemplateAsync();
        _shell.StepFileDropped += SetStepFile;
        _shell.StepAnalyzeRequested += () => _ = AnalyzeStepAsync();
        _shell.StepExportRequested += () => _ = ExportStepAsync();
        _shell.StepCancelRequested += () => _stepCancellation?.Cancel();
        _shell.SurfaceBrowseRequested += BrowseSurfaceFile;
        _shell.SurfaceClearRequested += ClearSurfaceFile;
        _shell.SurfaceFileDropped += SetSurfaceFile;
        _shell.SurfaceAnalyzeRequested += () => _ = AnalyzeSurfaceAsync();
        _shell.SurfaceExportRequested += ExportSurface;
        _shell.SurfaceCancelRequested += () => _surfaceCancellation?.Cancel();
        _shell.DosFileDropped += SetDosFile;
        _shell.CohpFileDropped += SetCohpFile;
        _shell.DosBrowseRequested += BrowseDosFile;
        _shell.DosClearRequested += ClearDosFile;
        _shell.DosAnalyzeRequested += () => _ = AnalyzeDosAsync();
        _shell.DosExportRequested += ExportDos;
        _shell.DosCancelRequested += () => _dosCancellation?.Cancel();
        _shell.PymatgenCheckRequested += () => _ = CheckPackageAsync("pymatgen", "pymatgen.io.lobster");
        _shell.NumpyCheckRequested += () => _ = CheckPackageAsync("numpy", "numpy");
        _shell.MatplotlibCheckRequested += () => _ = CheckPackageAsync("matplotlib", "matplotlib");
        _shell.OpenpyxlCheckRequested += () => _ = CheckPackageAsync("openpyxl", "openpyxl");
        _shell.OriginProCheckRequested += () => _ = CheckOriginProAsync();
        _shell.CohpBrowseRequested += BrowseCohpFile;
        _shell.CohpClearRequested += ClearCohpFile;
        _shell.CohpAnalyzeRequested += () => _ = AnalyzeCohpAsync();
        _shell.CohpExportRequested += ExportCohp;
        _shell.CohpCancelRequested += () => _cohpCancellation?.Cancel();
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
