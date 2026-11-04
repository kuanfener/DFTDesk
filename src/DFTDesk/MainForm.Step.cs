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

    private async Task SaveStepTemplateAsync()
    {
        if (_stepBusy) return;
        var mode = _shell.SelectedStepMode;
        var name = mode == "HER" ? "DFTDesk_HER_Template.xlsx" : "DFTDesk_ORR_OER_Template.xlsx";
        using var dialog = new SaveFileDialog
        {
            Title = "保存可直接填写的台阶图 Excel 模板",
            Filter = "Excel 工作簿|*.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            FileName = name,
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try
        {
            _shell.SetStepStatus("正在生成 " + mode + " 标准 Excel 输入模板…");
            await StepDiagramAnalysis.CreateTemplateAsync(
                mode, dialog.FileName);
            _shell.SetStepStatus("模板已保存：" + dialog.FileName +
                (mode == "HER"
                    ? "。HER 为原始七列分块表，所有数值已清空；请填写 B/C 列及参考气体能量。"
                    : "。ORR/OER 为原始五行分块表，所有数值已清空；请填写参考气体能量、催化剂 B/C 列，软件会重新计算反应自由能。"));
        }
        catch (Exception ex)
        {
            _shell.SetStepStatus("模板生成失败：" + ex.Message);
        }
    }

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

    private void ClearStepFile()
    {
        if (_stepBusy) return;
        DisposeStepResult();
        _stepInputPath = null;
        _shell.ClearStepInput();
        _shell.SetStepStatus("已删除台阶图 Excel，可重新拖入文件。");
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
                "。可选择导出 PNG、SVG、CSV、Excel、Origin DAT 或使用本机 Origin 的原生 OPJU。");
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

    private async Task ExportStepAsync()
    {
        if (_stepBusy || _stepResult is not { } data) return;
        var selected = _shell.ReadSelectedExportExtensions("Step");
        if (selected.Length == 0)
        {
            _shell.SetStepStatus("请至少勾选一种导出格式。");
            return;
        }

        string? originTemplate = null;
        if (selected.Contains(".opju"))
        {
            using var templateDialog = new OpenFileDialog
            {
                Title = "选择原始 Origin .opju 工程（保留其 Book 和 Graph）",
                Filter = "Origin Unicode 工程|*.opju",
                CheckFileExists = true,
                Multiselect = false,
            };
            if (templateDialog.ShowDialog() != DialogResult.OK) return;
            originTemplate = templateDialog.FileName;
        }

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
        }.Where(item => selected.Contains(item.Item2)).ToArray();
        var stem = "DFTDesk_StepDiagram_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var count = 0;
        string[] destinations;
        string originOutput;
        do
        {
            var prefix = stem + (count == 0 ? "" : "_" + count);
            destinations = files.Select(file => Path.Combine(dialog.SelectedPath, prefix + file.Item2)).ToArray();
            originOutput = Path.Combine(dialog.SelectedPath, prefix + ".opju");
            count++;
        } while (destinations.Any(File.Exists) ||
                 (originTemplate != null && File.Exists(originOutput)));

        _stepBusy = true;
        _shell.SetStepBusy(true);
        _stepCancellation = new CancellationTokenSource();
        var copied = new List<string>();
        try
        {
            for (var i = 0; i < files.Length; i++)
            {
                File.Copy(files[i].Item1, destinations[i], overwrite: false);
                copied.Add(destinations[i]);
            }

            if (originTemplate != null)
            {
                _shell.SetStepStatus("正在检测 OriginPro 包和 Origin 自动化接口…");
                try
                {
                    var status = await OriginProProbe.ProbeAsync(
                        _shell.PythonExecutablePath, _stepCancellation.Token);
                    _shell.SetPythonPackageStatus("originpro", "OriginPro：可用 · " + status);
                }
                catch (Exception ex)
                {
                    _shell.SetPythonPackageStatus("originpro", "OriginPro：不可用 · " + ex.Message);
                    throw;
                }
                _shell.SetStepStatus("正在通过本机 Origin 更新原始工程中的已有曲线并保存 OPJU…");
                await StepDiagramAnalysis.ExportNativeOriginAsync(
                    data.Xlsx, originTemplate, originOutput,
                    _shell.PythonExecutablePath, _stepCancellation.Token);
                copied.Add(originOutput);
            }

            _shell.SetStepStatus("已导出：" +
                string.Join("、", copied.Select(Path.GetFileName)) +
                (originTemplate != null ? "。原始 Origin 模板未被修改。" : ""));
        }
        catch (Exception ex)
        {
            foreach (var item in copied.Concat(originTemplate != null ? new[] { originOutput } : Array.Empty<string>()))
            {
                try { if (File.Exists(item)) File.Delete(item); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            _shell.SetStepStatus(ex is OperationCanceledException
                ? "已取消 Origin 项目导出。"
                : "导出失败：" + ex.Message);
        }
        finally
        {
            _stepCancellation?.Dispose();
            _stepCancellation = null;
            _stepBusy = false;
            _shell.SetStepBusy(false);
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
