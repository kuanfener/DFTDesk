using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DFTDesk;

/// <summary>
/// Independently checks one installed Python package in the selected interpreter.
/// Does not load or probe any other DFTDesk engine package.
/// </summary>
internal static class PythonPackageProbe
{
    private const string ProbeScript =
        "import importlib,importlib.metadata,sys; " +
        "importlib.import_module(sys.argv[1]); " +
        "print(importlib.metadata.version(sys.argv[2]))";

    public static async Task<string> ProbeAsync(
        string python, string module, string distribution, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(module) || string.IsNullOrWhiteSpace(distribution))
            throw new ArgumentException("Python 模块名称不能为空。");
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
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(ProbeScript);
        start.ArgumentList.Add(module);
        start.ArgumentList.Add(distribution);
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("无法启动 Python 环境。");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("无法启动 Python；请检查上方 python.exe 路径。", ex);
        }
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
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
            if (token.IsCancellationRequested) throw;
            throw new TimeoutException(distribution + " 的检测超过 45 秒。");
        }
        var result = (await stdout).Trim();
        var errors = (await stderr).Trim();
        if (process.ExitCode != 0)
        {
            var detail = errors.Length > 800 ? errors[^800..] : errors;
            throw new InvalidOperationException(distribution + " 不可用。请在选定环境运行 python -m pip install " +
                                                distribution + "。\n" + detail);
        }
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException(distribution + " 未返回有效的版本号。");
        return result;
    }
}
