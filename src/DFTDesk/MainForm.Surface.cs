using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DFTDesk;

internal sealed partial class MainForm
{
    private string? _surfaceInputPath;
    private SurfacePourbaixData? _surfaceResult;
    private CancellationTokenSource? _surfaceCancellation;
    private bool _surfaceBusy;

    private void BrowseSurfaceFile()
    {
        if (_surfaceBusy) return;
        using var dialog = new OpenFileDialog
        {
            Title = "选择催化剂吸附自由能 Excel（可包含多个催化剂）",
            Filter = "Excel 自由能表格|*.xlsx;*.xlsm|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog() == DialogResult.OK) SetSurfaceFile(dialog.FileName);
    }

    private void ClearSurfaceFile()
    {
        if (_surfaceBusy) return;
        DisposeSurfaceResult();
        _surfaceInputPath = null;
        _shell.ClearSurfaceInput();
        _shell.SetSurfaceStatus("已删除表面吸附态 Excel，可重新拖入文件。");
    }

    private void SetSurfaceFile(string path)
    {
        if (_surfaceBusy) return;
        if (!File.Exists(path) ||
            !new[] { ".xlsx", ".xlsm" }.Contains(Path.GetExtension(path).ToLowerInvariant()))
        {
            _shell.SetSurfaceStatus("请选择 .xlsx 或 .xlsm 自由能表格，不能直接导入 .opju。");
            return;
        }
        DisposeSurfaceResult();
        _surfaceInputPath = path;
        _shell.SetSurfaceInput(path);
        _shell.SetSurfaceStatus("已载入 Excel；可直接使用台阶图中的 ORR/OER 数据绘制吸附态稳定区。");
    }

    private async Task AnalyzeSurfaceAsync()
    {
        if (_surfaceBusy) return;
        if (string.IsNullOrWhiteSpace(_surfaceInputPath))
        {
            _shell.SetSurfaceStatus("请点击云朵或拖入 Excel 自由能表。");
            return;
        }
        SurfacePourbaixRequest request;
        try { request = _shell.ReadSurfaceRequest(_surfaceInputPath); }
        catch (ArgumentException ex)
        {
            _shell.SetSurfaceStatus("参数错误：" + ex.Message);
            return;
        }
        DisposeSurfaceResult();
        _surfaceBusy = true;
        _shell.SetSurfaceBusy(true);
        _shell.SetSurfaceStatus("正在用 pymatgen 校验吸附态组成，扫描 pH–电位稳定区…");
        _surfaceCancellation = new CancellationTokenSource();
        try
        {
            var result = await SurfacePourbaixAnalysis.AnalyzeAsync(
                request, _shell.PythonExecutablePath, _surfaceCancellation.Token);
            _surfaceResult = result;
            _shell.SetSurfacePreview(result);
            _shell.SetSurfaceStatus("绘制完成：" + result.Summary);
        }
        catch (OperationCanceledException)
        {
            _shell.SetSurfaceStatus("已取消绘图。");
        }
        catch (Exception ex)
        {
            _shell.SetSurfaceStatus("绘图失败：" + ex.Message);
        }
        finally
        {
            _surfaceCancellation?.Dispose();
            _surfaceCancellation = null;
            _surfaceBusy = false;
            _shell.SetSurfaceBusy(false);
        }
    }

    private void ExportSurface()
    {
        if (_surfaceBusy || _surfaceResult is not { } data) return;
        var selected = _shell.ReadSelectedExportExtensions("Surface");
        if (selected.Length == 0)
        {
            _shell.SetSurfaceStatus("请至少勾选一种导出格式。");
            return;
        }
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择表面 Pourbaix 结果导出目录",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        var files = new[] {
            (data.Png, ".png"), (data.Svg, ".svg"), (data.Csv, ".csv"),
            (data.Xlsx, ".xlsx"), (data.Dat, ".dat")
        }.Where(item => selected.Contains(item.Item2)).ToArray();
        var stem = "DFTDesk_SurfacePourbaix_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var count = 0;
        string[] destinations;
        do
        {
            var prefix = stem + (count == 0 ? "" : "_" + count);
            destinations = files.Select(file =>
                Path.Combine(dialog.SelectedPath, prefix + file.Item2)).ToArray();
            count++;
        } while (destinations.Any(File.Exists));
        var copied = new List<string>();
        try
        {
            for (var i = 0; i < files.Length; i++)
            {
                File.Copy(files[i].Item1, destinations[i], overwrite: false);
                copied.Add(destinations[i]);
            }
            _shell.SetSurfaceStatus("已导出：" +
                string.Join("、", destinations.Select(Path.GetFileName)));
        }
        catch (Exception ex)
        {
            foreach (var item in copied)
            {
                try { File.Delete(item); }
                catch (IOException) { }
            }
            _shell.SetSurfaceStatus("导出失败：" + ex.Message);
        }
    }

    private void DisposeSurfaceResult()
    {
        _shell.ClearSurfacePreview();
        if (_surfaceResult is not { } result) return;
        _surfaceResult = null;
        var path = result.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(path) ||
            !Path.GetFileName(path).StartsWith("DFTDesk-SURFACE-", StringComparison.Ordinal))
            return;
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
