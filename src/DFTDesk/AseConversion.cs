using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DFTDesk;

/// <summary>
/// ASE is the sole structure conversion backend. Python/ASE are supplied by the user, not embedded in
/// DFTDesk.exe. Python is invoked with ArgumentList, never via a shell.
/// </summary>
internal static class AseConversion
{
    // Keep the bridge self-contained so the single-file EXE needs no sidecar Python script.
    private const string Bridge = """
import sys
import ase
from ase.io import read, write

def main():
    if len(sys.argv) == 2 and sys.argv[1] == "check":
        print("ASE " + ase.__version__)
        return
    if len(sys.argv) != 7 or sys.argv[1] != "convert":
        raise ValueError("Invalid DFTDesk ASE bridge arguments.")
    _, _, source, destination, input_format, output_format, coordinate_mode = sys.argv
    if input_format not in ("cif", "vasp") or output_format not in ("cif", "vasp"):
        raise ValueError("Unsupported ASE crystal format.")
    atoms = read(source, format=input_format, index=0)
    if len(atoms) == 0 or abs(atoms.cell.volume) < 1e-10:
        raise ValueError("The structure has no atoms or has a degenerate cell.")
    # ASE can choose a representative atom for disordered CIF sites. Refuse silent
    # loss of fractional occupancy in POSCAR, which has no occupancy field.
    if input_format == "cif" and output_format == "vasp":
        for atom_occ in atoms.info.get("occupancy", {}).values():
            for occ in atom_occ.values():
                if abs(float(occ) - 1.0) > 1e-6:
                    raise ValueError("Partial CIF occupancies cannot be written faithfully to POSCAR.")
    if output_format == "vasp":
        write(destination, atoms, format="vasp", direct=(coordinate_mode == "direct"),
              # Keep the incoming atom/element sequence, including the order of element blocks.
              sort=False, vasp5=True)
    else:
        write(destination, atoms, format="cif", wrap=True)

if __name__ == "__main__":
    main()
""";

    public static async Task<string> ProbeAsync(string pythonExecutable, CancellationToken cancellationToken = default)
    {
        try
        {
            var output = await ExecuteAsync(pythonExecutable, ["check"], cancellationToken, TimeSpan.FromSeconds(15));
            if (!output.StartsWith("ASE ", StringComparison.Ordinal))
                throw new InvalidOperationException("Python 已启动，但 ASE 没有返回有效版本信息。");
            return output;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No module named", StringComparison.OrdinalIgnoreCase) ||
                                                   ex.Message.Contains("ModuleNotFoundError", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("当前 Python 环境没有安装 ASE。请在对应环境执行 python -m pip install ase。", ex);
        }
    }

    public static async Task<string> ConvertOneAsync(string input, string? outputDirectory, ExportFormat target,
        bool cartesian, string pythonExecutable, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = CrystalFiles.Detect(input)
            ?? throw new FormatException($"无法识别文件格式：{Path.GetFileName(input)}");
        if (source == target)
            throw new InvalidOperationException($"{Path.GetFileName(input)} 已是目标格式。");
        if (!File.Exists(input))
            throw new FileNotFoundException("输入文件不存在。", input);

        var directory = outputDirectory ?? Path.GetDirectoryName(Path.GetFullPath(input))!;
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"输出文件夹不存在：{directory}");
        var temp = Path.Combine(directory, ".dftdesk-" + Guid.NewGuid().ToString("N") + ".tmp");
        var targetName = Path.GetFileNameWithoutExtension(input);
        var targetExtension = target == ExportFormat.Cif ? ".cif" : ".vasp";
        try
        {
            await ExecuteAsync(pythonExecutable,
                ["convert", Path.GetFullPath(input), temp,
                    source == ExportFormat.Cif ? "cif" : "vasp",
                    target == ExportFormat.Cif ? "cif" : "vasp",
                    cartesian ? "cartesian" : "direct"],
                cancellationToken, TimeSpan.FromMinutes(5));
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(temp) || new FileInfo(temp).Length == 0)
                throw new IOException("ASE 没有生成有效的输出文件。");

            for (var i = 0; i < 100_000; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var suffix = i == 0 ? "" : $" ({i + 1})";
                var destination = Path.Combine(directory, targetName + suffix + targetExtension);
                try
                {
                    // File.Move without overwrite guarantees that an existing result
                    // or input is never replaced, even under concurrent batch runs.
                    File.Move(temp, destination);
                    return destination;
                }
                catch (IOException) when (File.Exists(destination))
                {
                    // Try the next available file name.
                }
            }
            throw new IOException("输出目录已有太多同名文件。");
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    private static async Task<string> ExecuteAsync(string executable, string[] args,
        CancellationToken cancellationToken, TimeSpan timeout)
    {
        executable = string.IsNullOrWhiteSpace(executable) ? "python" : executable.Trim().Trim('"');
        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(Bridge);
        foreach (var arg in args) start.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start())
                throw new InvalidOperationException("无法启动 Python 进程。");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"无法启动 Python：{executable}。请在「工作引擎」中指定 python.exe 的完整路径。", ex);
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
            throw new TimeoutException("ASE 执行超时。请检查 Python 环境或输入文件。");
        }

        var outputText = (await stdout).Trim();
        var errorText = (await stderr).Trim();
        if (process.ExitCode != 0)
        {
            var detail = errorText.Length > 1200 ? errorText[^1200..] : errorText;
            throw new InvalidOperationException($"ASE 转换失败（退出码 {process.ExitCode}）：{detail}");
        }
        return outputText;
    }
}
