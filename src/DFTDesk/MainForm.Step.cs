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
    private string? _stepInputPath;
    private StepDiagramData? _stepResult;
    private CancellationTokenSource? _stepCancellation;
    private bool _stepBusy;

    private void BrowseStepFile()
    {
        if (_stepBusy) return;
        using var dialog = new OpenFileDialog
        {
            Title = "选择用于绘制台阶图的 Excel 原始数据",
            Filter = "Excel 原始数据|*.xlsx;*.xlsm|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog() == DialogResult.OK)
            SetStepFile(dialog.FileName);
    }

    private void SetStepFile(string path)
    {
        if (_stepBusy) return;
        if (!File.Exists(path) ||
            !new[] { ".xlsx", ".xlsm" }.Contains(Path.GetExtension(path).ToLowerInvariant()))
        {
            _shell.SetStepStatus("请选择 Excel .xlsx / .xlsm 原始数据，不能将 .opju 当作能量数据导入。");
            return;
        }
        DisposeStepResult();
        _stepInputPath = path;
        _shell.SetStepInput(path);
        _shell.SetStepStatus("已载入 Excel；点击「绘制台阶图」生成预览。");
    }

    private async Task AnalyzeStepAsync()
    {
        if (_stepBusy) return;
        if (string.IsNullOrWhiteSpace(_stepInputPath))
        {
            _shell.SetStepStatus("请先点击云朵或拖入 Excel 原始数据文件。");
            return;
        }
        StepDiagramRequest request;
        try { request = _shell.ReadStepRequest(_stepInputPath); }
        catch (ArgumentException ex)
        {
            _shell.SetStepStatus("参数错误：" + ex.Message);
            return;
        }
        DisposeStepResult();
        _stepBusy = true;
        _shell.SetStepBusy(true);
        _shell.SetStepStatus("正在识别各体系中间态、计算自由能并绘制台阶图…");
        _stepCancellation = new CancellationTokenSource();
        try
        {
            var result = await StepDiagramAnalysis.AnalyzeAsync(
                request, _shell.PythonExecutablePath, _stepCancellation.Token);
            _stepResult = result;
            _shell.SetStepPreview(result);
            _shell.SetStepStatus("绘图完成：" + result.Summary +
                "。可导出 PNG、SVG、CSV、Excel 和 Origin DAT。");
        }
        catch (OperationCanceledException)
        {
            _shell.SetStepStatus("已取消台阶图绘制。");
        }
        catch (Exception ex)
        {
            _shell.SetStepStatus("绘图失败：" + ex.Message);
        }
        finally
        {
            _stepCancellation?.Dispose();
            _stepCancellation = null;
            _stepBusy = false;
            _shell.SetStepBusy(false);
        }
    }

    private void ExportStep()
    {
        if (_stepBusy || _stepResult is not { } data) return;
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择台阶图及 Origin / Excel 数据的导出目录",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        var files = new[]
        {
            (data.Png, ".png"), (data.Svg, ".svg"), (data.Csv, ".csv"),
            (data.Xlsx, ".xlsx"), (data.Dat, ".dat")
        };
        var stem = "DFTDesk_StepDiagram_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var count = 0;
        string[] destinations;
        do
        {
            var prefix = stem + (count == 0 ? "" : "_" + count);
            destinations = files.Select(file => Path.Combine(dialog.SelectedPath, prefix + file.Item2)).ToArray();
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
            _shell.SetStepStatus("已导出：" + string.Join("、", destinations.Select(Path.GetFileName)));
        }
        catch (Exception ex)
        {
            foreach (var item in copied)
            {
                try { File.Delete(item); }
                catch (IOException) { }
            }
            _shell.SetStepStatus("导出失败：" + ex.Message);
        }
    }

    private void DisposeStepResult()
    {
        _shell.ClearStepPreview();
        if (_stepResult is not { } result) return;
        _stepResult = null;
        var path = result.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(path) ||
            !Path.GetFileName(path).StartsWith("DFTDesk-STEP-", StringComparison.Ordinal))
            return;
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
