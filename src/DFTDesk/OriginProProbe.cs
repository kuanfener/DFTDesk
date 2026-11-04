using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DFTDesk;

/// <summary>
/// Origin's Python package can be importable while its Windows COM automation
/// server is unavailable. Probe these two layers separately and never open a
/// user's .opju or change their Origin project during health checks.
/// </summary>
internal static class OriginProProbe
{
    private const string ConnectionScript =
        "import originpro as op\n" +
        "try:\n" +
        "    if not op.oext:\n" +
        "        raise RuntimeError('需要 Windows 的 Origin 外部 Python 自动化接口')\n" +
        "    op.set_show(False)\n" +
        "    location = op.path('e')\n" +
        "    if not location:\n" +
        "        raise RuntimeError('Origin 安装目录不可用')\n" +
        "    print('DFTDESK_ORIGIN_OK|' + str(location))\n" +
        "finally:\n" +
        "    if op.oext:\n" +
        "        op.exit()\n";

    public static async Task<string> ProbeAsync(string python,
        CancellationToken cancellationToken = default)
    {
        string version;
        try
        {
            version = await PythonPackageProbe.ProbeAsync(
                python, "originpro", "originpro", cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "OriginPro Python 包不可用；请在工作引擎所选 Python 环境运行 " +
                "python -m pip install originpro。 " + ex.Message, ex);
        }

        try
        {
            var location = await ProbeConnectionAsync(python, cancellationToken);
            return $"originpro {version} · Origin 自动化连接成功（{location}）";
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"originpro {version} 已安装，但 Origin 自动化连接失败。请确认本机已安装并激活 " +
                "Origin 2021+（支持 Origin 2024），且当前没有未关闭的 Origin 模态弹窗。 " +
                ex.Message, ex);
        }
    }

    private static async Task<string> ProbeConnectionAsync(string python,
        CancellationToken cancellationToken)
    {
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
        start.ArgumentList.Add(ConnectionScript);
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new InvalidOperationException("无法启动 Python 连接检测。");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("无法启动所选 Python 环境。", ex);
        }

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
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
            throw new TimeoutException("Origin 连接检测超过 60 秒。");
        }

        var output = (await stdout).Trim();
        var errors = (await stderr).Trim();
        if (process.ExitCode != 0)
        {
            var detail = errors.Length > 1000 ? errors[^1000..] : errors;
            throw new InvalidOperationException("Origin COM 接口返回错误：" + detail);
        }

        const string marker = "DFTDESK_ORIGIN_OK|";
        var successfulLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in successfulLine)
        {
            var entry = line.Trim();
            if (entry.StartsWith(marker, StringComparison.Ordinal))
            {
                var folder = entry[marker.Length..].Trim();
                if (!string.IsNullOrEmpty(folder)) return folder;
            }
        }
        throw new InvalidOperationException("Origin 未返回有效连接确认。");
    }
}
