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
            var directText = File.ReadAllText(back);
            Assert.Equal(3, CountAtoms(back));
            Assert.InRange(GetCellVolume(back), 187.0, 190.0);
            Assert.Contains("Direct", directText);
            Assert.Contains("Si", directText);
            Assert.Contains("O", directText);

            var cartesian = await AseConversion.ConvertOneAsync(cif, folder, ExportFormat.Poscar,
                cartesian: true, python);
            Assert.Contains("Cartesian", File.ReadAllText(cartesian));
            Assert.Equal(3, CountAtoms(cartesian));
            Assert.Empty(Directory.EnumerateFiles(folder).Where(p =>
                Path.GetFileName(p).StartsWith(".dftdesk-", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    // Test-only POSCAR checks. The DFTDesk executable contains no C# structure parser.
    private static int CountAtoms(string path) => File.ReadAllLines(path)[6]
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Sum(int.Parse);

    private static double GetCellVolume(string path)
    {
        var lines = File.ReadAllLines(path);
        static double[] Vector(string line) => line
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var a = Vector(lines[2]);
        var b = Vector(lines[3]);
        var c = Vector(lines[4]);
        var factor = double.Parse(lines[1], System.Globalization.CultureInfo.InvariantCulture);
        return Math.Abs(a[0] * (b[1] * c[2] - b[2] * c[1])
            - a[1] * (b[0] * c[2] - b[2] * c[0])
            + a[2] * (b[0] * c[1] - b[1] * c[0])) * factor * factor * factor;
    }
}
