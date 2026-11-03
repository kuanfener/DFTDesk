using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DFTDesk;

internal sealed partial class MainForm
{
    private string? _cohpInputPath;
    private CohpData? _cohpResult;
    private CancellationTokenSource? _cohpCancellation;
    private bool _cohpBusy;

    private void BrowseCohpFile()
    {
        if (_cohpBusy) return;
        using var dialog = new OpenFileDialog
        {
            Title = "选择 LOBSTER COHPCAR 或 ICOHPLIST",
            Filter = "LOBSTER COHP / ICOHP|COHPCAR*;ICOHPLIST*|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        SetCohpFile(dialog.FileName);
    }

    private void ClearCohpFile()
    {
        if (_cohpBusy) return;
        DisposeCohpResult();
        _cohpInputPath = null;
        _shell.ClearCohpInput();
        _shell.SetCohpStatus("已删除 COHP 数据，可重新拖入文件。");
    }

    private void SetCohpFile(string path)
    {
        if (_cohpBusy) return;
        if (!File.Exists(path) ||
            !(Path.GetFileName(path).StartsWith("COHPCAR", StringComparison.OrdinalIgnoreCase) ||
              Path.GetFileName(path).StartsWith("ICOHPLIST", StringComparison.OrdinalIgnoreCase)))
        {
            _shell.SetCohpStatus("请拖入 COHPCAR.lobster 或 ICOHPLIST.lobster 文件。");
            return;
        }
        DisposeCohpResult();
        _cohpInputPath = path;
        _shell.SetCohpInput(path);
        _ = InspectCohpAsync();
    }

    private async Task InspectCohpAsync()
    {
        if (_cohpBusy || string.IsNullOrWhiteSpace(_cohpInputPath)) return;
        _cohpBusy = true;
        _shell.SetCohpBusy(true);
        _shell.SetCohpStatus("正在读取 LOBSTER 键及 ICOHP 数据…");
        _cohpCancellation = new CancellationTokenSource();
        try
        {
            var metadata = await CohpAnalysis.InspectAsync(
                _cohpInputPath, _shell.PythonExecutablePath, _cohpCancellation.Token);
            _shell.SetCohpMetadata(metadata);
            _shell.SetCohpStatus(metadata.HasCurves
                ? "已读取 COHP/ICOHP 曲线。选择键、自旋和能量区间后点击「分析并绘图」。"
                : "已读取 ICOHPLIST，可导出键强度表；若需曲线，请同时提供 COHPCAR.lobster。");
        }
        catch (OperationCanceledException)
        {
            _shell.SetCohpStatus("已取消 COHP 读取。");
        }
        catch (Exception ex)
        {
            _shell.SetCohpStatus("读取失败：" + ex.Message);
        }
        finally
        {
            _cohpCancellation?.Dispose();
            _cohpCancellation = null;
            _cohpBusy = false;
            _shell.SetCohpBusy(false);
        }
    }

    private async Task AnalyzeCohpAsync()
    {
        if (_cohpBusy) return;
        if (string.IsNullOrWhiteSpace(_cohpInputPath))
        {
            _shell.SetCohpStatus("请先导入 COHPCAR.lobster 或 ICOHPLIST.lobster。");
            return;
        }

        CohpRequest request;
        try { request = _shell.ReadCohpRequest(_cohpInputPath); }
        catch (ArgumentException ex)
        {
            _shell.SetCohpStatus(ex.Message);
            return;
        }

        DisposeCohpResult();
        _cohpBusy = true;
        _shell.SetCohpBusy(true);
        _shell.SetCohpStatus("正在解析 COHP/ICOHP，并生成表格与图像…");
        _cohpCancellation = new CancellationTokenSource();
        try
        {
            var result = await CohpAnalysis.AnalyzeAsync(
                request, _shell.PythonExecutablePath, _cohpCancellation.Token);
            _shell.SetCohpPreview(result);
            _cohpResult = result;
            _shell.SetCohpExportReady(true);
            _shell.SetCohpStatus(result.HasCurves
                ? "分析完成：可导出 Excel / Origin DAT / CSV / PNG / SVG。"
                : "已生成 ICOHP 键强度表：可导出 Excel / Origin DAT / CSV。");
        }
        catch (OperationCanceledException)
        {
            _shell.SetCohpStatus("已取消 COHP 分析。");
        }
        catch (Exception ex)
        {
            _shell.SetCohpStatus("分析失败：" + ex.Message);
        }
        finally
        {
            _cohpCancellation?.Dispose();
            _cohpCancellation = null;
            _cohpBusy = false;
            _shell.SetCohpBusy(false);
        }
    }

    private void ExportCohp()
    {
        if (_cohpBusy || _cohpResult is not { } result) return;
        var selected = _shell.ReadSelectedExportExtensions("Cohp");
        if (selected.Length == 0)
        {
            _shell.SetCohpStatus("请至少勾选一种导出格式。");
            return;
        }
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择 COHP / ICOHP 表格与图像的输出目录",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        var namedSources = new List<(string Source, string Extension)>
        {
            (result.Csv, ".csv"),
            (result.Xlsx, ".xlsx"),
            (result.Dat, ".dat"),
        };
        if (result.HasCurves)
        {
            namedSources.Add((result.Png, ".png"));
            namedSources.Add((result.Svg, ".svg"));
        }
        namedSources = namedSources.Where(item => selected.Contains(item.Extension)).ToList();
        if (namedSources.Count == 0)
        {
            _shell.SetCohpStatus("当前仅有 ICOHP 表格，无法导出图片。请勾选 Excel、CSV 或 Origin DAT。");
            return;
        }
        var prefix = "DFTDesk_COHP_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var suffix = 0;
        string[] destinations;
        do
        {
            var stem = prefix + (suffix == 0 ? "" : "_" + suffix);
            destinations = namedSources.Select(item => Path.Combine(dialog.SelectedPath, stem + item.Extension)).ToArray();
            suffix++;
        } while (destinations.Any(File.Exists));
        var completed = new List<string>();
        try
        {
            for (var i = 0; i < namedSources.Count; i++)
            {
                File.Copy(namedSources[i].Source, destinations[i], overwrite: false);
                completed.Add(destinations[i]);
            }
            _shell.SetCohpStatus("已导出：" + string.Join("、", destinations.Select(Path.GetFileName)));
        }
        catch (Exception ex)
        {
            foreach (var path in completed)
            {
                try { File.Delete(path); }
                catch (IOException) { }
            }
            _shell.SetCohpStatus("导出失败：" + ex.Message);
        }
    }

    private void DisposeCohpResult()
    {
        _shell.ClearCohpPreview();
        if (_cohpResult is not { } result) return;
        _cohpResult = null;
        var directory = result.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(directory) ||
            !Path.GetFileName(directory).StartsWith("DFTDesk-COHP-", StringComparison.Ordinal))
            return;
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
