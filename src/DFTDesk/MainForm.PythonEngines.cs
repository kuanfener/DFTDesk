using System;
using System.Threading.Tasks;

namespace DFTDesk;

internal sealed partial class MainForm
{
    private async Task CheckPackageAsync(string package, string module)
    {
        _shell.SetPythonPackageStatus(package, $"{package}：正在检测…");
        try
        {
            var version = await PythonPackageProbe.ProbeAsync(
                _shell.PythonExecutablePath, module, package);
            _shell.SetPythonPackageStatus(package, $"{package}：可用 · {version}");
        }
        catch (Exception ex)
        {
            _shell.SetPythonPackageStatus(package, $"{package}：不可用 · {ex.Message}");
        }
    }
}
