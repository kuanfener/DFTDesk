using Filee.Core.Formats;
using Filee.Core.Presets;
using Filee.Core.Profiles;
using Filee.Core.Settings;

namespace Filee.Core.Tests;

public class CrystalProfileTests
{
    [Fact]
    public void New_install_selects_crystal_donut_for_POSCAR_CONTCAR_and_CIF()
    {
        var profiles = BuiltInData.CreateProfiles();
        foreach (var file in new[] { "POSCAR", "CONTCAR", "Fe.vasp", "Fe.poscar", "Fe.cif" })
            Assert.Equal("crystal", ProfileSelector.Select(profiles, [file])?.Id);
        Assert.Equal("models", ProfileSelector.Select(profiles, ["sample.stl"])?.Id);
        Assert.Equal(FormatGrade.Specialist, FormatFields.GradeOf(FormatRegistry.Get("cif")));
    }

    [Fact]
    public void Upgrade_adds_crystal_presets_and_profile_without_moving_custom_claims()
    {
        var presets = BuiltInData.CreatePresets().Where(p => p.Id is not ("to-cif" or "to-poscar")).ToList();
        var profiles = BuiltInData.CreateProfiles().Where(p => p.Id != "crystal").ToList();
        var models = profiles.Single(p => p.Id == "models");
        models.Extensions.AddRange(["vasp", "poscar", "cif"]); // simulate an older generic-model upgrade
        Assert.True(SettingsMigrations.ApplyToLibrary(58, presets, profiles));
        Assert.Contains(presets, p => p.Id == "to-cif");
        Assert.Contains(presets, p => p.Id == "to-poscar");
        Assert.Equal("crystal", ProfileSelector.Select(profiles, ["CONTCAR"])?.Id);
        Assert.DoesNotContain("cif", models.Extensions);
        Assert.False(SettingsMigrations.ApplyToLibrary(AppSettings.CurrentSchemaVersion, presets, profiles));
    }
}
