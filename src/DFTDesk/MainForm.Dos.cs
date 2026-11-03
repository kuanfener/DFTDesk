using System;
using System.IO;
using System.Windows.Forms;

namespace DFTDesk;

internal sealed partial class MainForm
{
    private string? _dosInputPath;
    private DosData? _dosResult;
    private CancellationTokenSource? _dosCancellation;
    private bool _dosBusy;

    private void BrowseDosFile()
    {
        if (_dosBusy) return;
        using var dialog = new OpenFileDialog
        {
            Title = "选择 VASP DOSCAR 或 vasprun.xml",
            Filter = "VASP DOS 文件|DOSCAR;DOSCAR.*;vasprun.xml;vasprun.xml.gz;*.xml|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        DisposeDosResult();
        _dosInputPath = dialog.FileName;
        _shell.SetDosInput(dialog.FileName);
        _ = InspectDosAsync();
    }

    private async Task InspectDosAsync()
    {
        if (_dosBusy || string.IsNullOrWhiteSpace(_dosInputPath)) return;
        _dosBusy = true;
        _shell.SetDosBusy(true);
        _shell.SetDosStatus("正在读取 DOS 能量网格、自旋与元素信息…");
        _dosCancellation = new CancellationTokenSource();
        try
        {
            var metadata = await DosAnalysis.InspectAsync(
                _dosInputPath, _shell.PythonExecutablePath, _dosCancellation.Token);
            _shell.SetDosMetadata(metadata);
            _shell.SetDosStatus(metadata.HasPdos
                ? "数据已载入。选择元素/轨道/自旋与能量范围后点击「分析并绘图」。"
                : "已读取 TDOS，但无可用元素 PDOS；可以直接绘制总态密度。");
        }
        catch (OperationCanceledException)
        {
            _shell.SetDosStatus("已取消读取 DOS。");
        }
        catch (Exception ex)
        {
            _shell.SetDosStatus("读取失败：" + ex.Message);
        }
        finally
        {
            _dosCancellation.Dispose();
            _dosCancellation = null;
            _dosBusy = false;
            _shell.SetDosBusy(false);
        }
    }

    private async Task AnalyzeDosAsync()
    {
        if (_dosBusy) return;
        if (string.IsNullOrWhiteSpace(_dosInputPath))
        {
            _shell.SetDosStatus("请先导入 DOSCAR 或 vasprun.xml 文件。");
            return;
        }

        DosRequest request;
        try
        {
            request = _shell.ReadDosRequest(_dosInputPath);
        }
        catch (ArgumentException ex)
        {
            _shell.SetDosStatus(ex.Message);
            return;
        }

        DisposeDosResult();
        _dosBusy = true;
        _shell.SetDosBusy(true);
        _shell.SetDosStatus("正在使用 pymatgen / NumPy 分析 DOS 并绘图…");
        _dosCancellation = new CancellationTokenSource();
        try
        {
            var result = await DosAnalysis.AnalyzeAsync(
                request, _shell.PythonExecutablePath, _dosCancellation.Token);
            _shell.SetDosPreview(result);
            _dosResult = result;
            _shell.SetDosExportReady(true);
            _shell.SetDosStatus("分析完成，已生成 DOS 曲线、积分和 d 带中心；可导出 CSV / Excel / Origin DAT / PNG / SVG。");
        }
        catch (OperationCanceledException)
        {
            _shell.SetDosStatus("已取消 DOS 分析。");
        }
        catch (Exception ex)
        {
            _shell.SetDosStatus("分析失败：" + ex.Message);
        }
        finally
        {
            _dosCancellation.Dispose();
            _dosCancellation = null;
            _dosBusy = false;
            _shell.SetDosBusy(false);
        }
    }

    private void ExportDos()
    {
        if (_dosBusy || _dosResult is null) return;
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择 DOS 曲线、CSV 数据的保存目录",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        var sources = new[] { _dosResult.Csv, _dosResult.Xlsx, _dosResult.Dat, _dosResult.Png, _dosResult.Svg };
        var extensions = new[] { ".csv", ".xlsx", ".dat", ".png", ".svg" };
        var label = _dosInputPath is null ? "DOS" : Path.GetFileNameWithoutExtension(_dosInputPath);
        var prefix = "DFTDesk_" + label + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string[] destinations;
        var suffix = 0;
        do
        {
            var stem = prefix + (suffix == 0 ? "" : $"_{suffix}");
            destinations = extensions.Select(extension => Path.Combine(dialog.SelectedPath, stem + extension)).ToArray();
            suffix++;
        } while (destinations.Any(File.Exists));

        var completed = new List<string>();
        try
        {
            for (var i = 0; i < sources.Length; i++)
            {
                File.Copy(sources[i], destinations[i], overwrite: false);
                completed.Add(destinations[i]);
            }
            _shell.SetDosStatus("已保存：" + string.Join("、", destinations.Select(Path.GetFileName)));
        }
        catch (Exception ex)
        {
            foreach (var path in completed)
            {
                try { File.Delete(path); }
                catch (IOException) { }
            }
            _shell.SetDosStatus("导出失败：" + ex.Message);
        }
    }

    private void DisposeDosResult()
    {
        _shell.ClearDosPreview();
        if (_dosResult is not { } result) return;
        _dosResult = null;
        var directory = result.WorkingDirectory;
        // Only delete directories created for DOS previews by DosAnalysis.
        if (string.IsNullOrWhiteSpace(directory) ||
            !Path.GetFileName(directory).StartsWith("DFTDesk-DOS-", StringComparison.Ordinal))
            return;
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
