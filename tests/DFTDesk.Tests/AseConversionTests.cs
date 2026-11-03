using DFTDesk;

namespace DFTDesk.Tests;

public class AseConversionTests
{
    private const string Poscar = """
        DFTDesk ASE integration
        1.0
        5.4 0.0 0.0
        0.2 5.7 0.0
        0.3 0.4 6.1
        Si O
        1 2
        Direct
        0.125 0.25 0.375
        0.50 0.60 0.70
        0.85 0.05 0.15
        """;

    [Fact]
    public async Task Missing_python_reports_error_and_leaves_no_partial_output()
    {
        var folder = Path.Combine(Path.GetTempPath(), "dftdesk-ase-unavailable-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var source = Path.Combine(folder, "POSCAR");
            File.WriteAllText(source, Poscar);
            var badPython = Path.Combine(folder, "missing-python-for-test.exe");
            var probe = await Assert.ThrowsAsync<InvalidOperationException>(
                () => AseConversion.ProbeAsync(badPython));
            Assert.Contains("无法启动 Python", probe.Message);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => AseConversion.ConvertOneAsync(source, folder, ExportFormat.Cif,
                    cartesian: false, badPython));
            Assert.Equal(Poscar, File.ReadAllText(source));
            Assert.Empty(Directory.EnumerateFiles(folder).Where(p => Path.GetExtension(p) == ".cif"));
            Assert.Empty(Directory.EnumerateFiles(folder).Where(p =>
                Path.GetFileName(p).StartsWith(".dftdesk-", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Installed_ase_engine_converts_and_preserves_unique_names()
    {
        var python = Environment.GetEnvironmentVariable("DFTDESK_TEST_ASE_PYTHON");
        // The ordinary test suite remains runnable without Python/ASE.
        // Windows CI configures this variable and installs ASE to run the real integration path.
        if (string.IsNullOrWhiteSpace(python)) return;

        var status = await AseConversion.ProbeAsync(python);
        Assert.StartsWith("ASE ", status);
        var folder = Path.Combine(Path.GetTempPath(), "dftdesk-ase-integrated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var source = Path.Combine(folder, "POSCAR");
            File.WriteAllText(source, Poscar);
            var cif = await AseConversion.ConvertOneAsync(source, folder, ExportFormat.Cif,
                cartesian: false, python);
            var second = await AseConversion.ConvertOneAsync(source, folder, ExportFormat.Cif,
                cartesian: false, python);
            Assert.NotEqual(cif, second);
            Assert.Equal(Poscar, File.ReadAllText(source));
            Assert.True(File.Exists(cif));
            Assert.EndsWith(".cif", cif, StringComparison.OrdinalIgnoreCase);
            var back = await AseConversion.ConvertOneAsync(cif, folder, ExportFormat.Poscar,
                cartesian: false, python);
            var restored = CrystalFormat.ReadPoscar(File.ReadAllText(back));
            Assert.Equal(3, restored.Sites.Count);
            Assert.InRange(restored.Volume, 187.0, 190.0);
            Assert.Contains("Direct", File.ReadAllText(back));

            var cartesian = await AseConversion.ConvertOneAsync(cif, folder, ExportFormat.Poscar,
                cartesian: true, python);
            Assert.Contains("Cartesian", File.ReadAllText(cartesian));
            Assert.Equal(3, CrystalFormat.ReadPoscar(File.ReadAllText(cartesian)).Sites.Count);
            Assert.Empty(Directory.EnumerateFiles(folder).Where(p =>
                Path.GetFileName(p).StartsWith(".dftdesk-", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
