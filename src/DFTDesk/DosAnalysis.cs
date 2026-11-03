using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DFTDesk;

internal sealed record DosRequest(string InputPath, string[] Elements, string[] Orbitals,
    string Spin, double EnergyMin, double EnergyMax);

internal sealed class DosData
{
    public string Version { get; set; } = "";
    public string[] Elements { get; set; } = [];
    public bool HasPdos { get; set; }
    public bool SpinPolarized { get; set; }
    public double Fermi { get; set; }
    public double EnergyMin { get; set; }
    public double EnergyMax { get; set; }
    public string Info { get; set; } = "";
    public string Png { get; set; } = "";
    public string Svg { get; set; } = "";
    public string Csv { get; set; } = "";
    public string Summary { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
}

/// <summary>
/// DOS processing runs in the configured external Python environment. Scripts are
/// embedded in the single-file EXE; all subprocess arguments are passed without a shell.
/// </summary>
internal static class DosAnalysis
{
    private const string ScriptResource = "DFTDesk.DosScripts.dos_analyzer.py";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<string> ProbeAsync(string python, CancellationToken token = default)
    {
        var info = await ExecuteAsync(python, ["--action", "check"], token, TimeSpan.FromSeconds(30));
        if (string.IsNullOrWhiteSpace(info.Version))
            throw new InvalidOperationException("Python 未返回有效的 pymatgen / NumPy / Matplotlib 版本。");
        return info.Version;
    }

    public static Task<DosData> InspectAsync(string input, string python, CancellationToken token = default)
    {
        if (!File.Exists(input)) throw new FileNotFoundException("DOS 输入文件不存在。", input);
        return ExecuteAsync(python, ["--action", "inspect", "--input", Path.GetFullPath(input)],
            token, TimeSpan.FromMinutes(3));
    }

    public static async Task<DosData> AnalyzeAsync(DosRequest request, string python,
        CancellationToken token = default)
    {
        if (!File.Exists(request.InputPath)) throw new FileNotFoundException("DOS 输入文件不存在。");
        if (!double.IsFinite(request.EnergyMin) || !double.IsFinite(request.EnergyMax) ||
            request.EnergyMax <= request.EnergyMin)
            throw new ArgumentException("能量范围必须是有效数字，且最小值小于最大值。");
        if (request.Orbitals.Any(s => s is not ("s" or "p" or "d")))
            throw new ArgumentException("只支持 s / p / d 投影轨道。");

        var directory = Path.Combine(Path.GetTempPath(), "DFTDesk-DOS-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var data = await ExecuteAsync(python,
                ["--action", "analyze", "--input", Path.GetFullPath(request.InputPath),
                 "--elements", string.Join(",", request.Elements),
                 "--orbitals", string.Join(",", request.Orbitals),
                 "--spin", request.Spin,
                 "--emin", request.EnergyMin.ToString("G17", CultureInfo.InvariantCulture),
                 "--emax", request.EnergyMax.ToString("G17", CultureInfo.InvariantCulture),
                 "--outdir", directory],
                token, TimeSpan.FromMinutes(5));
            data.WorkingDirectory = directory;
            foreach (var file in new[] { data.Png, data.Csv, data.Svg })
                if (string.IsNullOrWhiteSpace(file) || !File.Exists(file) || new FileInfo(file).Length == 0)
                    throw new IOException("DOS 分析未生成完整的 CSV / PNG / SVG 文件。");
            return data;
        }
        catch
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            throw;
        }
    }

    private static async Task<DosData> ExecuteAsync(string python, string[] arguments,
        CancellationToken cancellationToken, TimeSpan timeout)
    {
        var script = Path.Combine(Path.GetTempPath(), "dftdesk_dos_" + Guid.NewGuid().ToString("N") + ".py");
        try
        {
            using (var input = typeof(DosAnalysis).Assembly.GetManifestResourceStream(ScriptResource)
                   ?? throw new InvalidOperationException("DOS 脚本未包含在 DFTDesk 可执行文件中。"))
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
            start.ArgumentList.Add("-u");
            start.ArgumentList.Add(script);
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = new Process { StartInfo = start };
            try
            {
                if (!process.Start()) throw new InvalidOperationException("无法启动 DOS Python 引擎。");
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException("无法启动 Python，请在「工作引擎」中设置正确的 python.exe。", ex);
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
                throw new TimeoutException("DOS 解析超过时间限制；请检查文件是否完整。");
            }
            var result = (await stdout).Trim();
            var errors = (await stderr).Trim();
            if (process.ExitCode != 0)
            {
                var detail = errors.Length > 1800 ? errors[^1800..] : errors;
                if (detail.Contains("ModuleNotFoundError", StringComparison.OrdinalIgnoreCase) ||
                    detail.Contains("No module named", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "DOS 引擎缺少 Python 依赖。请在同一环境执行 python -m pip install pymatgen numpy matplotlib。\n" + detail);
                throw new InvalidOperationException("DOS 分析失败：" + detail);
            }
            try
            {
                return JsonSerializer.Deserialize<DosData>(result, JsonOptions)
                    ?? throw new JsonException("返回空数据");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("DOS 引擎返回的数据无法解析：" + result[..Math.Min(300, result.Length)], ex);
            }
        }
        finally
        {
            if (File.Exists(script)) File.Delete(script);
        }
    }
}
