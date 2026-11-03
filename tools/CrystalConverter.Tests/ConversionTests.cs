using Filee.CrystalConverter;

namespace Filee.CrystalConverter.Tests;

public class ConversionTests
{
    private const string Poscar = """
        Si O crystal
        1.0
        4.1 0 0
        0.7 4.8 0
        0.4 0.8 5.2
        Si O
        1 2
        Selective dynamics
        Direct
        0.125 0.25 0.375 T F T
        0.9 0.1 0.2 F F F
        0.4 0.6 0.8 T T T
        """;

    private const string InversionCif = """
        data_iron
        _cell_length_a 5.2(2)
        _cell_length_b 6.1
        _cell_length_c 7.3
        _cell_angle_alpha 90
        _cell_angle_beta 102
        _cell_angle_gamma 90
        _space_group_IT_number 2
        loop_
        _space_group_symop_operation_xyz
        'x, y, z'
        '-x, -y, -z'
        loop_
        _atom_site_label
        _atom_site_type_symbol
        _atom_site_fract_x
        _atom_site_fract_y
        _atom_site_fract_z
        _atom_site_occupancy
        Fe1 Fe 0.12 0.23 0.34 1
        """;

    [Fact]
    public void Format_detection_understands_vasp_and_extensionless_names()
    {
        Assert.Equal(ExportFormat.Poscar, ConversionBatch.Detect("POSCAR"));
        Assert.Equal(ExportFormat.Poscar, ConversionBatch.Detect("contcar"));
        Assert.Equal(ExportFormat.Poscar, ConversionBatch.Detect("sample.vasp"));
        Assert.Equal(ExportFormat.Poscar, ConversionBatch.Detect("sample.poscar"));
        Assert.Equal(ExportFormat.Cif, ConversionBatch.Detect("sample.cif"));
        Assert.Null(ConversionBatch.Detect("sample.pdb"));
    }

    [Fact]
    public void Triclinic_round_trip_preserves_species_cell_and_coordinates()
    {
        var initial = CrystalFormat.ReadPoscar(Poscar);
        var result = CrystalFormat.ReadPoscar(CrystalFormat.WritePoscar(
            CrystalFormat.ReadCif(CrystalFormat.WriteCif(initial)), cartesian: false));
        Assert.Equal(new[] { "Si", "O", "O" }, result.Sites.Select(s => s.Element));
        Assert.Equal(initial.Volume, result.Volume, 8);
        for (var i = 0; i < result.Sites.Count; i++)
        {
            Assert.Equal(initial.Sites[i].Fractional.X, result.Sites[i].Fractional.X, 10);
            Assert.Equal(initial.Sites[i].Fractional.Y, result.Sites[i].Fractional.Y, 10);
            Assert.Equal(initial.Sites[i].Fractional.Z, result.Sites[i].Fractional.Z, 10);
        }
    }

    [Fact]
    public void Cartesian_poscar_export_and_negative_volume_scale_work()
    {
        var source = CrystalFormat.ReadPoscar(Poscar);
        var cartesian = CrystalFormat.WritePoscar(source, cartesian: true);
        Assert.Contains("Cartesian", cartesian);
        var parsed = CrystalFormat.ReadPoscar(cartesian);
        Assert.Equal(source.Sites[0].Fractional.X, parsed.Sites[0].Fractional.X, 10);

        var negative = """
            Negative volume
            -64
            2 0 0
            0 2 0
            0 0 2
            Fe
            1
            Cartesian
            0.5 1.0 1.5
            """;
        Assert.Equal(64, CrystalFormat.ReadPoscar(negative).Volume, 8);
    }

    [Fact]
    public void Poscar_export_uses_vesta_style_fixed_precision_and_aligned_columns()
    {
        var source = new CrystalStructure("4", new(14.9375, 0, 0),
            new(7.8057762330853144e-16, 12.7478, 0),
            new(1.1021821192326179e-15, 1.1021821192326179e-15, 18),
            [new("C", new(0.98516000000000004, 0.050560000000000001, 0.43648999999999999))]);
        var text = CrystalFormat.WritePoscar(source, cartesian: false);
        var lines = text.Replace("\r", "").Split('\n');

        Assert.Equal("1.0", lines[1]);
        Assert.Equal("14.9375000000", lines[2].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
        Assert.Equal("0.0000000000", lines[3].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
        Assert.Equal("0.0000000000", lines[4].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
        Assert.Equal("18.0000000000", lines[4].Split(' ', StringSplitOptions.RemoveEmptyEntries)[2]);
        Assert.Equal("Direct", lines[7]);
        Assert.Equal(new[] { "0.98516000", "0.05056000", "0.43649000" },
            lines[8].Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Assert.DoesNotContain("E-", text);
        Assert.DoesNotContain("E+", text);
        Assert.Equal(1, CrystalFormat.ReadPoscar(text).Sites.Count);
    }

    [Fact]
    public void Default_native_poscar_output_is_cartesian_while_direct_remains_available()
    {
        var structure = CrystalFormat.ReadPoscar(Poscar);
        var defaultOutput = CrystalFormat.WritePoscar(structure);
        var lines = defaultOutput.Replace("\r", "").Split('\n');
        Assert.Equal("Cartesian", lines[7]);
        // The first Si fractional position (0.125, 0.25, 0.375) becomes
        // (0.8375, 1.5, 1.95) Å in this non-orthogonal cell.
        Assert.Equal(new[] { "0.83750000", "1.50000000", "1.95000000" },
            lines[8].Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var directOutput = CrystalFormat.WritePoscar(structure, cartesian: false);
        Assert.Equal("Direct", directOutput.Replace("\r", "").Split('\n')[7]);
        Assert.Equal(3, CrystalFormat.ReadPoscar(defaultOutput).Sites.Count);
    }

    [Fact]
    public void Cif_export_uses_fixed_point_cell_and_site_precision()
    {
        var text = CrystalFormat.WriteCif(CrystalFormat.ReadPoscar(Poscar));
        Assert.Contains("_cell_length_a 4.1000000000", text);
        Assert.Contains("_atom_site_fract_x", text);
        Assert.Contains("Si1 Si 0.12500000 0.25000000 0.37500000 1", text);
        Assert.DoesNotContain("E-", text);
        Assert.Equal(3, CrystalFormat.ReadCif(text).Sites.Count);
    }

    [Fact]
    public void Cif_symmetry_expands_and_partial_occupancy_is_rejected()
    {
        var structure = CrystalFormat.ReadCif(InversionCif);
        Assert.Equal(2, structure.Sites.Count);
        Assert.Equal(0.88, structure.Sites[1].Fractional.X, 10);
        var partial = InversionCif.Replace("0.34 1", "0.34 0.5");
        Assert.Throws<FormatException>(() => CrystalFormat.ReadCif(partial));
        var missingOperators = InversionCif.Replace("\r\n", "\n").Replace(
            "loop_\n_space_group_symop_operation_xyz\n'x, y, z'\n'-x, -y, -z'\n", "");
        Assert.Throws<FormatException>(() => CrystalFormat.ReadCif(missingOperators));
    }

    [Fact]
    public void Batch_conversion_creates_unique_outputs_without_overwriting_inputs()
    {
        var folder = Path.Combine(Path.GetTempPath(), "filee-crystal-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var source = Path.Combine(folder, "POSCAR");
            File.WriteAllText(source, Poscar);
            var first = ConversionBatch.ConvertOne(source, folder, ExportFormat.Cif, false);
            var second = ConversionBatch.ConvertOne(source, folder, ExportFormat.Cif, false);
            Assert.NotEqual(first, second);
            Assert.EndsWith("POSCAR.cif", first, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Poscar, File.ReadAllText(source));
            var back = ConversionBatch.ConvertOne(first, folder, ExportFormat.Poscar, true);
            Assert.EndsWith(".vasp", back, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(3, CrystalFormat.ReadPoscar(File.ReadAllText(back)).Sites.Count);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
