using Filee.Core.Conversion;
using Filee.Core.Formats;
using Filee.Core.Presets;
using Filee.Engines.Crystals;

namespace Filee.Engines.Tests;

public class CrystalTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private const string Poscar = """
        Si O triclinic
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
    public void Extensionless_POSCAR_and_CONTCAR_are_recognised_without_truncating_names()
    {
        foreach (var name in new[] { "POSCAR", "CONTCAR", "poscar" })
        {
            Assert.Equal("poscar", FormatRegistry.Detect(name)?.Id);
            Assert.Equal(name, FormatRegistry.NameWithoutExtension(name));
        }
        Assert.Equal("poscar", FormatRegistry.Detect("sample.vasp")?.Id);
        Assert.Equal("cif", FormatRegistry.Detect("sample.cif")?.Id);
        Assert.Equal("poscar", FormatRegistry.Detect("sample.poscar")?.Id);
    }

    [Fact]
    public void Triclinic_POSCAR_CIF_POSCAR_keeps_cell_species_and_positions()
    {
        var initial = CrystalFormat.ReadPoscar(Poscar);
        var result = CrystalFormat.ReadPoscar(CrystalFormat.WritePoscar(CrystalFormat.ReadCif(CrystalFormat.WriteCif(initial))));
        Assert.Equal(3, result.Sites.Count);
        Assert.Equal(new[] { "Si", "O", "O" }, result.Sites.Select(s => s.Element).ToArray());
        Assert.Equal(initial.Volume, result.Volume, 8);
        for (var i = 0; i < result.Sites.Count; i++)
        {
            Assert.Equal(initial.Sites[i].Fractional.X, result.Sites[i].Fractional.X, 10);
            Assert.Equal(initial.Sites[i].Fractional.Y, result.Sites[i].Fractional.Y, 10);
            Assert.Equal(initial.Sites[i].Fractional.Z, result.Sites[i].Fractional.Z, 10);
        }
        Assert.Contains("_space_group_IT_number 1", CrystalFormat.WriteCif(initial));
    }

    [Fact]
    public void Negative_volume_scale_and_Cartesian_coordinates_work()
    {
        var poscar = """
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
        var structure = CrystalFormat.ReadPoscar(poscar);
        Assert.Equal(64, structure.Volume, 8);
        Assert.Equal(0.25, structure.Sites[0].Fractional.X, 10);
        Assert.Equal(0.5, structure.Sites[0].Fractional.Y, 10);
        Assert.Equal(0.75, structure.Sites[0].Fractional.Z, 10);
    }

    [Fact]
    public void CIF_symmetry_is_expanded_and_special_positions_are_deduplicated()
    {
        var structure = CrystalFormat.ReadCif(InversionCif);
        Assert.Equal(2, structure.Sites.Count);
        Assert.Equal(5.2, structure.A.Length, 8);
        Assert.Equal(0.88, structure.Sites[1].Fractional.X, 10);
        Assert.Equal(0.77, structure.Sites[1].Fractional.Y, 10);
        Assert.Equal(0.66, structure.Sites[1].Fractional.Z, 10);

        var special = InversionCif.Replace("0.12 0.23 0.34", "0 0 0");
        Assert.Single(CrystalFormat.ReadCif(special).Sites);
    }

    [Fact]
    public void CIF_with_partial_occupancy_is_rejected()
    {
        Assert.Throws<FormatException>(() => CrystalFormat.ReadCif(InversionCif.Replace("0.34 1", "0.34 0.5")));
    }

    [Fact]
    public void Missing_nontrivial_symmetry_is_not_silently_dropped()
    {
        var cif = InversionCif.Replace("loop_\n_space_group_symop_operation_xyz\n'x, y, z'\n'-x, -y, -z'\n", "");
        Assert.Throws<FormatException>(() => CrystalFormat.ReadCif(cif));
    }

    [Fact]
    public async Task Job_queue_converts_extensionless_POSCAR_to_CIF_and_back()
    {
        var folder = fx.NewFolder();
        var input = Path.Combine(folder, "POSCAR");
        await File.WriteAllTextAsync(input, Poscar);
        var cifJob = await fx.ConvertAsync([input], new Preset { TargetFormat = "cif" });
        Assert.True(cifJob.State == JobState.Completed,
            string.Join("; ", cifJob.Files.Select(f => $"{f.ErrorKey}: {f.ErrorDetail}")));
        var cif = Assert.Single(cifJob.Outputs);
        Assert.EndsWith(".cif", cif, StringComparison.OrdinalIgnoreCase);
        var poscarJob = await fx.ConvertAsync([cif], new Preset { TargetFormat = "poscar" });
        Assert.Equal(JobState.Completed, poscarJob.State);
        Assert.EndsWith(".vasp", Assert.Single(poscarJob.Outputs), StringComparison.OrdinalIgnoreCase);
    }
}
