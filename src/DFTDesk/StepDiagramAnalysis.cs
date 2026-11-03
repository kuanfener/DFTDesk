using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DFTDesk;

internal sealed record StepDiagramRequest(
    string InputPath, string Mode, double Potential, double Ph, string Reference);

internal sealed class StepDiagramData
{
    public string Png { get; set; } = "";
    public string Svg { get; set; } = "";
    public string Csv { get; set; } = "";
    public string Xlsx { get; set; } = "";
    public string Dat { get; set; } = "";
    public string Summary { get; set; } = "";
    public int SeriesCount { get; set; }
    public string WorkingDirectory { get; set; } = "";
}

internal static class StepDiagramAnalysis
{
    private const string Resource = "DFTDesk.StepScripts.step_diagram.py";
    private const string OriginProjectResource = "DFTDesk.StepScripts.origin_project_export.py";
    private const string HerTemplateResource = "DFTDesk.StepScripts.DFTDesk_HER_Template.xlsx";
    private const string OxygenTemplateResource = "DFTDesk.StepScripts.DFTDesk_ORR_OER_Template.xlsx";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static async Task<StepDiagramData> AnalyzeAsync(StepDiagramRequest request, string python,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.InputPath))
            throw new FileNotFoundException("台阶图 Excel 文件不存在。", request.InputPath);
        if (request.Mode is not ("HER" or "ORR" or "OER"))
            throw new ArgumentException("未知的反应模式。");
        if (request.Reference is not ("RHE" or "SHE") ||
            !double.IsFinite(request.Potential) || !double.IsFinite(request.Ph))
            throw new ArgumentException("电位、pH 或电位参考标尺无效。");
        var folder = Path.Combine(Path.GetTempPath(), "DFTDesk-STEP-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var data = await ExecuteAsync(request, folder, python, cancellationToken);
            data.WorkingDirectory = folder;
            foreach (var file in new[] { data.Png, data.Svg, data.Csv, data.Xlsx, data.Dat })
                if (string.IsNullOrWhiteSpace(file) || !File.Exists(file) || new FileInfo(file).Length == 0)
                    throw new IOException("台阶图导出文件缺失或为空。");
            return data;
        }
        catch
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            throw;
        }
    }

    public static async Task CreateTemplateAsync(string mode, string destination)
    {
        if (mode is not ("HER" or "ORR" or "OER"))
            throw new ArgumentException("未知的反应模式。", nameof(mode));
        // These are the original supplied workbooks. Copying embedded resources
        // preserves workbook layout, labels and cell styles; scientific input cells are blank.
        var resource = mode == "HER" ? HerTemplateResource : OxygenTemplateResource;
        using var source = typeof(StepDiagramAnalysis).Assembly
            .GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("Excel 原始模板未包含在可执行文件中。");
        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(output);
    }

    public static async Task ExportNativeOriginAsync(string profileWorkbook, string projectTemplate,
        string outputPath, string python, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(profileWorkbook))
            throw new FileNotFoundException("台阶图 Excel 结果不存在。", profileWorkbook);
        if (!File.Exists(projectTemplate) ||
            !string.Equals(Path.GetExtension(projectTemplate), ".opju", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("请选择存在的 Origin .opju 工程模板。");
        if (string.Equals(Path.GetFullPath(outputPath), Path.GetFullPath(projectTemplate),
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("不能用导出结果覆盖原始 Origin 模板。");
        if (File.Exists(outputPath))
            throw new IOException("目标 Origin 工程已存在，请选择新的保存名称。");

        var script = Path.Combine(Path.GetTempPath(),
            "dftdesk_origin_" + Guid.NewGuid().ToString("N") + ".py");
        try
        {
            using (var input = typeof(StepDiagramAnalysis).Assembly
                       .GetManifestResourceStream(OriginProjectResource)
                       ?? throw new InvalidOperationException("Origin 原生导出脚本未包含在可执行文件中。"))
            using (var output = new FileStream(script, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await input.CopyToAsync(output, cancellationToken);

            var start = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(python) ? "python" : python.Trim().Trim('"'),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            start.Environment["PYTHONIOENCODING"] = "utf-8";
            foreach (var arg in new[]
            {
                "-u", script, "--template", Path.GetFullPath(projectTemplate),
                "--input", Path.GetFullPath(profileWorkbook),
                "--output", Path.GetFullPath(outputPath),
            }) start.ArgumentList.Add(arg);

            using var process = new Process { StartInfo = start };
            try
            {
                if (!process.Start())
                    throw new InvalidOperationException("Origin 自动化 Python 引擎无法启动。");
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException(
                    "无法启动 Python；请在「工作引擎」配置正确的 python.exe。", ex);
            }

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) { }
                    await process.WaitForExitAsync(CancellationToken.None);
                }
                if (cancellationToken.IsCancellationRequested) throw;
                throw new TimeoutException("Origin 原生工程导出超过 10 分钟，请检查是否有 Origin 弹窗。");
            }

            var outputText = (await stdout).Trim();
            var errorText = (await stderr).Trim();
            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(errorText) ? outputText : errorText;
                if (message.Length > 1800) message = message[^1800..];
                throw new InvalidOperationException(message);
            }
            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                throw new IOException("Origin 引擎没有生成有效的 OPJU 工程。");
        }
        finally
        {
            if (File.Exists(script)) File.Delete(script);
        }
    }

    private static async Task<StepDiagramData> ExecuteAsync(StepDiagramRequest request, string folder,
        string python, CancellationToken cancellationToken)
    {
        var script = Path.Combine(Path.GetTempPath(), "dftdesk_step_" + Guid.NewGuid().ToString("N") + ".py");
        try
        {
            using (var input = typeof(StepDiagramAnalysis).Assembly.GetManifestResourceStream(Resource)
                   ?? throw new InvalidOperationException("台阶图脚本未包含在可执行文件中。"))
            using (var output = new FileStream(script, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await input.CopyToAsync(output, cancellationToken);

            var start = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(python) ? "python" : python.Trim().Trim('"'),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            start.Environment["PYTHONIOENCODING"] = "utf-8";
            start.Environment["MPLBACKEND"] = "Agg";
            foreach (var arg in new[]
            {
                "-u", script, "--action", "analyze", "--input", Path.GetFullPath(request.InputPath),
                "--outdir", folder, "--mode", request.Mode,
                "--potential", request.Potential.ToString("G17", System.Globalization.CultureInfo.InvariantCulture),
                "--ph", request.Ph.ToString("G17", System.Globalization.CultureInfo.InvariantCulture),
                "--reference", request.Reference
            }) start.ArgumentList.Add(arg);
            using var process = new Process { StartInfo = start };
            try
            {
                if (!process.Start()) throw new InvalidOperationException("无法启动绘图 Python 引擎。");
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException("无法启动 Python，请在「工作引擎」中设置正确的 python.exe。", ex);
            }
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(4));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) { }
                    await process.WaitForExitAsync(CancellationToken.None);
                }
                if (cancellationToken.IsCancellationRequested) throw;
                throw new TimeoutException("台阶图绘制超过时间限制。");
            }
            var outputText = (await stdout).Trim();
            var errorText = (await stderr).Trim();
            if (process.ExitCode != 0)
            {
                var last = errorText.Length > 1500 ? errorText[^1500..] : errorText;
                throw new InvalidOperationException("绘图失败：" + last);
            }
            try
            {
                return JsonSerializer.Deserialize<StepDiagramData>(outputText, Json)
                    ?? throw new JsonException("返回空数据");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("台阶图引擎返回数据错误：" +
                    outputText[..Math.Min(250, outputText.Length)], ex);
            }
        }
        finally
        {
            if (File.Exists(script)) File.Delete(script);
        }
    }
}
