using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DFTDesk;

internal sealed record ClusterRequest(
    bool Faceted, string Element, string Numbers, string Facets,
    string LatticeConstant, string Vacuum, string OutputDirectory, bool Cartesian = true,
    bool ExportPoscar = true, bool ExportCif = true);

internal sealed record ClusterResult(string Directory, string Log, int StructureCount);

/// <summary>
/// Runs the two user-provided ASE generation algorithms via an embedded Python script.
/// The temporary script and staging directory are cleaned up; results move into a unique
/// run directory only after successful generation. No existing POSCAR/CIF is overwritten.
/// </summary>
internal static class ClusterGeneration
{
    private const string ScriptResource = "DFTDesk.ClusterScripts.cluster_generator.py";

    public static async Task<ClusterResult> GenerateAsync(
        ClusterRequest request, string pythonExecutable, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (!request.ExportPoscar && !request.ExportCif)
            throw new ArgumentException("请至少勾选 POSCAR 或 CIF 一种输出格式。");
        var element = request.Element.Trim();
        if (!Regex.IsMatch(element, @"^[A-Za-z]{1,2}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("请填写有效元素符号，例如 Pt、Fe、Au。");
        element = char.ToUpperInvariant(element[0]) +
            (element.Length > 1 ? element[1..].ToLowerInvariant() : "");

        if (string.IsNullOrWhiteSpace(request.Numbers) || request.Numbers.Length > 200)
            throw new ArgumentException("请填写原子数，例如 4,5,6,13 或 4:12:2。");

        var root = string.IsNullOrWhiteSpace(request.OutputDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : request.OutputDirectory.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(root)) throw new DirectoryNotFoundException("请选择输出根目录。");
        Directory.CreateDirectory(root);

        var id = Guid.NewGuid().ToString("N")[..8];
        var stem = element + (request.Faceted ? "_Faceted_Clusters" : "_Clusters");
        var destination = Path.Combine(root, $"{stem}_{DateTime.Now:yyyyMMdd_HHmmss}_{id}");
        var staging = Path.Combine(root, ".dftdesk-cluster-" + Guid.NewGuid().ToString("N"));
        var script = Path.Combine(Path.GetTempPath(), "dftdesk_cluster_" + Guid.NewGuid().ToString("N") + ".py");

        try
        {
            using (var stream = typeof(ClusterGeneration).Assembly.GetManifestResourceStream(ScriptResource)
                ?? throw new InvalidOperationException("DFTDesk 团簇脚本资源未正确打包。"))
            using (var output = new FileStream(script, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await stream.CopyToAsync(output, cancellationToken);

            var python = string.IsNullOrWhiteSpace(pythonExecutable)
                ? "python" : pythonExecutable.Trim().Trim('"');
            var start = new ProcessStartInfo
            {
                FileName = python,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            start.Environment["PYTHONIOENCODING"] = "utf-8";
            foreach (var argument in new[] { "-u", script,
                "--mode", request.Faceted ? "facet" : "general",
                "--element", element,
                "--numbers", request.Numbers.Trim(),
                "--out", staging })
                start.ArgumentList.Add(argument);
            if (request.Faceted)
            {
                start.ArgumentList.Add("--facets");
                start.ArgumentList.Add(string.IsNullOrWhiteSpace(request.Facets)
                    ? "all" : request.Facets.Trim());
                if (!string.IsNullOrWhiteSpace(request.LatticeConstant))
                {
                    start.ArgumentList.Add("--a");
                    start.ArgumentList.Add(request.LatticeConstant.Trim());
                }

            }
            start.ArgumentList.Add("--vacuum");
            start.ArgumentList.Add(string.IsNullOrWhiteSpace(request.Vacuum)
                ? "12" : request.Vacuum.Trim());
            if (!request.Cartesian) start.ArgumentList.Add("--direct");
            start.ArgumentList.Add("--formats");
            start.ArgumentList.Add(request.ExportPoscar && request.ExportCif ? "both" :
                request.ExportPoscar ? "poscar" : "cif");
            using var process = new Process { StartInfo = start };
            try
            {
                if (!process.Start()) throw new InvalidOperationException("Python 进程启动失败。");
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException(
                    "无法启动 Python，请先在「工作引擎」配置包含 ASE 的 python.exe。", ex);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
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
                throw new TimeoutException("团簇生成超过 10 分钟，已取消。可缩小原子数或减少晶面。");
            }

            var text = (await stdoutTask).Trim();
            var error = (await stderrTask).Trim();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("团簇生成失败：" +
                    (error.Length > 1200 ? error[^1200..] : error) +
                    (string.IsNullOrWhiteSpace(text) ? "" : "\n" + text[^Math.Min(text.Length, 1200)..]));
            var match = Regex.Match(text, @"完成：(\d+) 个结构");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var count) || count < 1)
                throw new InvalidOperationException("ASE 未报告成功生成任何团簇：" + text);

            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
            return new ClusterResult(destination, text, count);
        }
        finally
        {
            if (File.Exists(script)) File.Delete(script);
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }
}
