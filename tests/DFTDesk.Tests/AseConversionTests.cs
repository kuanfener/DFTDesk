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

    // P1 sites are deliberately Zn -> O -> C, not alphabetical C -> O -> Zn.
    private const string NonAlphabeticalCif = """
        data_explicit_order
        _cell_length_a 5.0
        _cell_length_b 5.0
        _cell_length_c 6.0
        _cell_angle_alpha 90
        _cell_angle_beta 90
        _cell_angle_gamma 90
        _space_group_name_H-M_alt 'P 1'
        loop_
          _atom_site_label
          _atom_site_type_symbol
          _atom_site_fract_x
          _atom_site_fract_y
          _atom_site_fract_z
          Zn1 Zn 0.10 0.20 0.30
          O1  O  0.40 0.50 0.60
          C1  C  0.70 0.80 0.90
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
            Assert.Equal(new[] { "Si", "O", "O" }, ReadPoscarAtomOrder(back));
            Assert.Equal(new[] { "Si", "O", "O" }, ReadCifAtomOrder(cif));
            AssertPoscarDirectX(back, 0.125, 0.50, 0.85);

            var cartesian = await AseConversion.ConvertOneAsync(cif, folder, ExportFormat.Poscar,
                cartesian: true, python);
            Assert.Contains("Cartesian", File.ReadAllText(cartesian));
            Assert.Equal(3, CountAtoms(cartesian));
            Assert.Equal(new[] { "Si", "O", "O" }, ReadPoscarAtomOrder(cartesian));

            // CIF -> POSCAR must keep the original non-alphabetical site order too.
            var sourceCif = Path.Combine(folder, "ordered.cif");
            File.WriteAllText(sourceCif, NonAlphabeticalCif);
            var converted = await AseConversion.ConvertOneAsync(sourceCif, folder,
                ExportFormat.Poscar, cartesian: false, python);
            Assert.Equal(new[] { "Zn", "O", "C" }, ReadPoscarAtomOrder(converted));
            AssertPoscarDirectX(converted, 0.10, 0.40, 0.70);
            Assert.Empty(Directory.EnumerateFiles(folder).Where(p =>
                Path.GetFileName(p).StartsWith(".dftdesk-", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static string[] ReadPoscarAtomOrder(string path)
    {
        var lines = File.ReadAllLines(path);
        var symbols = lines[5].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var counts = lines[6].Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        Assert.Equal(symbols.Length, counts.Length);
        return symbols.Zip(counts, (symbol, count) => Enumerable.Repeat(symbol, count))
            .SelectMany(group => group).ToArray();
    }

    private static double[] ReadPoscarDirectX(string path)
    {
        var lines = File.ReadAllLines(path);
        var index = Array.FindIndex(lines, line => line.Trim().Equals("Direct", StringComparison.OrdinalIgnoreCase));
        Assert.True(index >= 0);
        return lines.Skip(index + 1).Take(CountAtoms(path))
            .Select(line => double.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0],
                System.Globalization.CultureInfo.InvariantCulture)).ToArray();
    }

    private static string[] ReadCifAtomOrder(string path)
    {
        var lines = File.ReadAllLines(path);
        var typeHeaderIndex = Array.FindIndex(lines, line => line.Trim() == "_atom_site_type_symbol");
        Assert.True(typeHeaderIndex >= 0);
        var firstHeader = typeHeaderIndex;
        while (firstHeader > 0 && lines[firstHeader - 1].TrimStart().StartsWith("_atom_site_", StringComparison.Ordinal))
            firstHeader--;
        var endHeader = typeHeaderIndex + 1;
        while (endHeader < lines.Length && lines[endHeader].TrimStart().StartsWith("_atom_site_", StringComparison.Ordinal))
            endHeader++;
        var typeColumn = typeHeaderIndex - firstHeader;
        return lines.Skip(endHeader)
            .TakeWhile(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[typeColumn])
            .ToArray();
    }

    private static void AssertPoscarDirectX(string path, params double[] expected)
    {
        var actual = ReadPoscarDirectX(path);
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
            Assert.InRange(actual[i], expected[i] - 1e-5, expected[i] + 1e-5);
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
