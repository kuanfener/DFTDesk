using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DFTDesk;

internal sealed record CohpRequest(string InputPath, string[] Labels, string Spin,
    double EnergyMin, double EnergyMax, bool PlotNegative);

internal sealed class CohpBond
{
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public double Length { get; set; }
    public int Count { get; set; } = 1;
    public double? IcohpUp { get; set; }
    public double? IcohpDown { get; set; }
    public override string ToString() => Description;
}

internal sealed class CohpData
{
    public string Version { get; set; } = "";
    public CohpBond[] Bonds { get; set; } = [];
    public bool HasCurves { get; set; }
    public bool SpinPolarized { get; set; }
    public double EnergyMin { get; set; }
    public double EnergyMax { get; set; }
    public string Info { get; set; } = "";
    public string Png { get; set; } = "";
    public string Svg { get; set; } = "";
    public string Csv { get; set; } = "";
    public string Xlsx { get; set; } = "";
    public string Dat { get; set; } = "";
    public string Summary { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
}

/// <summary>
/// Launches pymatgen's LOBSTER parser in the configured external Python environment.
/// Arguments are never passed through a shell; temporary scripts come from EXE resources.
/// </summary>
internal static class CohpAnalysis
{
    private const string ScriptResource = "DFTDesk.CohpScripts.cohp_analyzer.py";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<string> ProbeAsync(string python, CancellationToken token = default)
    {
        var result = await ExecuteAsync(python, ["--action", "check"], token, TimeSpan.FromMinutes(1));
        if (string.IsNullOrWhiteSpace(result.Version))
            throw new InvalidOperationException("COHP 引擎没有返回有效的版本信息。");
        return result.Version;
    }

    public static Task<CohpData> InspectAsync(string path, string python, CancellationToken token = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("COHP / ICOHP 文件不存在。", path);
        return ExecuteAsync(python, ["--action", "inspect", "--input", Path.GetFullPath(path)],
            token, TimeSpan.FromMinutes(4));
    }

    public static async Task<CohpData> AnalyzeAsync(CohpRequest request, string python,
        CancellationToken token = default)
    {
        if (!File.Exists(request.InputPath)) throw new FileNotFoundException("COHP 输入文件不存在。");
        if (request.Labels.Length == 0) throw new ArgumentException("请至少选择一个键。");
        if (!double.IsFinite(request.EnergyMin) || !double.IsFinite(request.EnergyMax) ||
            request.EnergyMax <= request.EnergyMin)
            throw new ArgumentException("能量范围需满足下限 < 上限。");
        var directory = Path.Combine(Path.GetTempPath(), "DFTDesk-COHP-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var arguments = new System.Collections.Generic.List<string>
            {
                "--action", "analyze", "--input", Path.GetFullPath(request.InputPath),
                "--labels", string.Join(",", request.Labels),
                "--spin", request.Spin,
                "--emin", request.EnergyMin.ToString("G17", CultureInfo.InvariantCulture),
                "--emax", request.EnergyMax.ToString("G17", CultureInfo.InvariantCulture),
                "--outdir", directory,
            };
            if (request.PlotNegative) arguments.Add("--negative");
            var data = await ExecuteAsync(python, arguments.ToArray(), token, TimeSpan.FromMinutes(5));
            data.WorkingDirectory = directory;
            var files = data.HasCurves
                ? new[] { data.Csv, data.Xlsx, data.Dat, data.Png, data.Svg }
                : new[] { data.Csv, data.Xlsx, data.Dat };
            foreach (var file in files)
                if (string.IsNullOrWhiteSpace(file) || !File.Exists(file) || new FileInfo(file).Length == 0)
                    throw new IOException("COHP / ICOHP 未生成完整的导出文件。");
            return data;
        }
        catch
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
            throw;
        }
    }

    private static async Task<CohpData> ExecuteAsync(string python, string[] arguments,
        CancellationToken cancellationToken, TimeSpan timeout)
    {
        var script = Path.Combine(Path.GetTempPath(), "dftdesk_cohp_" + Guid.NewGuid().ToString("N") + ".py");
        try
        {
            using (var input = typeof(CohpAnalysis).Assembly.GetManifestResourceStream(ScriptResource)
                   ?? throw new InvalidOperationException("COHP 脚本没有嵌入 DFTDesk EXE。"))
            using (var output = new FileStream(script, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await input.CopyToAsync(output, cancellationToken);

            var info = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(python) ? "python" : python.Trim().Trim('"'),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            info.Environment["PYTHONIOENCODING"] = "utf-8";
            info.Environment["MPLBACKEND"] = "Agg";
            info.ArgumentList.Add("-u");
            info.ArgumentList.Add(script);
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            using var process = new Process { StartInfo = info };
            try
            {
                if (!process.Start()) throw new InvalidOperationException("无法启动 COHP Python 引擎。");
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException("无法启动 Python；请在「工作引擎」中配置正确的 python.exe。", ex);
            }
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(deadline.Token);
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
                throw new TimeoutException("COHP / ICOHP 解析超时；请检查文件是否完整。");
            }
            var result = (await stdout).Trim();
            var errors = (await stderr).Trim();
            if (process.ExitCode != 0)
            {
                var detail = errors.Length > 1800 ? errors[^1800..] : errors;
                if (detail.Contains("ModuleNotFoundError", StringComparison.OrdinalIgnoreCase) ||
                    detail.Contains("No module named", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "缺少 COHP Python 依赖。请在相同环境执行 python -m pip install pymatgen numpy matplotlib openpyxl。\n" + detail);
                throw new InvalidOperationException("COHP / ICOHP 分析失败：" + detail);
            }
            try
            {
                return JsonSerializer.Deserialize<CohpData>(result, JsonOptions)
                    ?? throw new JsonException("Python 返回空数据。");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    "COHP 引擎返回的 JSON 无法解析：" + result[..Math.Min(300, result.Length)], ex);
            }
        }
        finally
        {
            if (File.Exists(script)) File.Delete(script);
        }
    }
}
