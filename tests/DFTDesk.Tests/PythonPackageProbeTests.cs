using System;
using System.Threading.Tasks;

namespace DFTDesk.Tests;

public sealed class PythonPackageProbeTests
{
    private static string Python => Environment.GetEnvironmentVariable("DFTDESK_TEST_ASE_PYTHON") ?? "python";

    [Theory]
    [InlineData("ase", "ase")]
    [InlineData("pymatgen.io.lobster", "pymatgen")]
    [InlineData("numpy", "numpy")]
    [InlineData("matplotlib", "matplotlib")]
    [InlineData("openpyxl", "openpyxl")]
    public async Task EachPackageCanBeCheckedIndependently(string module, string distribution)
    {
        var version = await PythonPackageProbe.ProbeAsync(
            Python, module, distribution, TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    [Fact]
    public async Task MissingModuleReportsOnlyRequestedDependency()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PythonPackageProbe.ProbeAsync(
                Python, "dftdesk_missing_module_probe", "dftdesk_missing_module_probe",
                TestContext.Current.CancellationToken));
        Assert.Contains("dftdesk_missing_module_probe", error.Message);
    }
}
