using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DFTDesk;

internal sealed record SurfacePourbaixRequest(string InputPath, double PhMin, double PhMax,
    double UMin, double UMax, int Resolution = 121, string Layout = "horizontal");

internal sealed class SurfacePourbaixData
{
    public string Png { get; set; } = "";
    public string Svg { get; set; } = "";
    public string Csv { get; set; } = "";
    public string Dat { get; set; } = "";
    public string Xlsx { get; set; } = "";
    public string Summary { get; set; } = "";
    public int SeriesCount { get; set; }
    public string WorkingDirectory { get; set; } = "";
}

internal static class SurfacePourbaixAnalysis
{
    private const string ScriptResource = "DFTDesk.SurfaceScripts.surface_pourbaix.py";
    private const string StepResource = "DFTDesk.StepScripts.step_diagram.py";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static async Task<SurfacePourbaixData> AnalyzeAsync(SurfacePourbaixRequest request,
        string python, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.InputPath))
            throw new FileNotFoundException("吸附自由能 Excel 文件不存在。", request.InputPath);
        if (request.PhMin < 0 || request.PhMax > 14 || request.PhMin >= request.PhMax ||
            request.UMin < -3 || request.UMax > 3 || request.UMin >= request.UMax ||
            request.Resolution < 40 || request.Resolution > 201 ||
            (request.Layout != "horizontal" && request.Layout != "vertical") ||
            !double.IsFinite(request.PhMin) || !double.IsFinite(request.PhMax) ||
            !double.IsFinite(request.UMin) || !double.IsFinite(request.UMax))
            throw new ArgumentException("pH 或 SHE 电位范围无效。");

        var folder = Path.Combine(Path.GetTempPath(), "DFTDesk-SURFACE-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var script = Path.Combine(folder, "surface_pourbaix.py");
            var stepScript = Path.Combine(folder, "step_diagram.py");
            await ExtractAsync(ScriptResource, script, cancellationToken);
            await ExtractAsync(StepResource, stepScript, cancellationToken);
            var start = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(python) ? "python" : python.Trim().Trim('"'),
                WorkingDirectory = folder,
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
                "-u", script, "--input", Path.GetFullPath(request.InputPath), "--outdir", folder,
                "--phmin", request.PhMin.ToString("G17", CultureInfo.InvariantCulture),
                "--phmax", request.PhMax.ToString("G17", CultureInfo.InvariantCulture),
                "--umin", request.UMin.ToString("G17", CultureInfo.InvariantCulture),
                "--umax", request.UMax.ToString("G17", CultureInfo.InvariantCulture),
                "--resolution", request.Resolution.ToString(CultureInfo.InvariantCulture),
                "--layout", request.Layout,
            }) start.ArgumentList.Add(arg);

            using var process = new Process { StartInfo = start };
            try
            {
                if (!process.Start()) throw new InvalidOperationException("无法启动表面 Pourbaix 绘图引擎。");
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException("无法启动 Python，请在「工作引擎」配置 python.exe。", ex);
            }
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(6));
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
                throw new TimeoutException("表面 Pourbaix 绘制超时，请缩小网格范围或减少催化剂数量。");
            }
            var output = (await stdout).Trim();
            var error = (await stderr).Trim();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("表面 Pourbaix 绘制失败：" +
                    (error.Length > 1600 ? error[^1600..] : error));
            SurfacePourbaixData data;
            try
            {
                data = JsonSerializer.Deserialize<SurfacePourbaixData>(output, Json)
                    ?? throw new JsonException("返回空结果");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("表面 Pourbaix 引擎返回格式错误：" +
                    output[..Math.Min(300, output.Length)], ex);
            }
            foreach (var file in new[] { data.Png, data.Svg, data.Csv, data.Dat, data.Xlsx })
                if (string.IsNullOrWhiteSpace(file) || !File.Exists(file) || new FileInfo(file).Length == 0)
                    throw new IOException("表面 Pourbaix 结果文件缺失或为空。");
            // Python modules live in this private, validated scratch directory.
            File.Delete(script);
            File.Delete(stepScript);
            data.WorkingDirectory = folder;
            return data;
        }
        catch
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            throw;
        }
    }

    private static async Task ExtractAsync(string name, string destination, CancellationToken cancellationToken)
    {
        using var input = typeof(SurfacePourbaixAnalysis).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("缺少内置表面 Pourbaix 脚本：" + name);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output, cancellationToken);
    }
}
