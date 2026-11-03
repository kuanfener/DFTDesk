using DFTDesk;

namespace DFTDesk.Tests;

public class ClusterGenerationTests
{
    [Fact]
    public async Task Generates_general_clusters_as_cartesian_poscar_and_cif()
    {
        var python = Environment.GetEnvironmentVariable("DFTDESK_TEST_ASE_PYTHON");
        if (string.IsNullOrWhiteSpace(python)) return;

        var root = Path.Combine(Path.GetTempPath(), "dftdesk-cluster-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var request = new ClusterRequest(false, "Pt", "4,13", "all", "", "12", root);
            var result = await ClusterGeneration.GenerateAsync(request, python);
            Assert.Equal(2, result.StructureCount);
            Assert.True(Directory.Exists(result.Directory));
            Assert.True(File.Exists(Path.Combine(result.Directory, "POSCAR_Pt4_Tetrahedron")));
            Assert.True(File.Exists(Path.Combine(result.Directory, "POSCAR_Pt13_Icosahedron")));
            Assert.True(File.Exists(Path.Combine(result.Directory, "Pt4_Tetrahedron.cif")));
            Assert.True(File.Exists(Path.Combine(result.Directory, "Pt13_Icosahedron.cif")));
            Assert.Contains("Cartesian", File.ReadAllText(Path.Combine(result.Directory, "POSCAR_Pt4_Tetrahedron")));
            Assert.Equal(4, Directory.GetFiles(result.Directory).Length);

            // Every run produces a new folder and never replaces earlier results.
            var again = await ClusterGeneration.GenerateAsync(request, python);
            Assert.NotEqual(result.Directory, again.Directory);
            Assert.Equal(2, again.StructureCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Generates_fcc_facet_and_direct_mode()
    {
        var python = Environment.GetEnvironmentVariable("DFTDESK_TEST_ASE_PYTHON");
        if (string.IsNullOrWhiteSpace(python)) return;

        var root = Path.Combine(Path.GetTempPath(), "dftdesk-facet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var request = new ClusterRequest(true, "Pt", "13", "111,ico", "", "12", root, Cartesian: false);
            var result = await ClusterGeneration.GenerateAsync(request, python);
            Assert.Equal(2, result.StructureCount);
            var facet = Path.Combine(result.Directory, "POSCAR_Pt13_FCC111");
            var ico = Path.Combine(result.Directory, "POSCAR_Pt13_Icosahedron");
            Assert.True(File.Exists(facet));
            Assert.True(File.Exists(ico));
            Assert.Contains("Direct", File.ReadAllText(facet));
            Assert.Contains("Direct", File.ReadAllText(ico));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Generates_only_selected_cluster_formats()
    {
        var python = Environment.GetEnvironmentVariable("DFTDESK_TEST_ASE_PYTHON");
        if (string.IsNullOrWhiteSpace(python)) return;
        var root = Path.Combine(Path.GetTempPath(), "dftdesk-formats-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var poscarOnly = new ClusterRequest(false, "Pt", "4", "all", "", "12", root,
                ExportPoscar: true, ExportCif: false);
            var poscar = await ClusterGeneration.GenerateAsync(poscarOnly, python);
            Assert.Single(Directory.GetFiles(poscar.Directory));
            Assert.StartsWith("POSCAR_", Path.GetFileName(Directory.GetFiles(poscar.Directory)[0]));

            var cifOnly = new ClusterRequest(false, "Pt", "4", "all", "", "12", root,
                ExportPoscar: false, ExportCif: true);
            var cif = await ClusterGeneration.GenerateAsync(cifOnly, python);
            Assert.Single(Directory.GetFiles(cif.Directory));
            Assert.EndsWith(".cif", Directory.GetFiles(cif.Directory)[0]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Invalid_facet_does_not_publish_any_partial_result()
    {
        var python = Environment.GetEnvironmentVariable("DFTDESK_TEST_ASE_PYTHON");
        if (string.IsNullOrWhiteSpace(python)) return;
        var root = Path.Combine(Path.GetTempPath(), "dftdesk-invalid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var request = new ClusterRequest(true, "Pt", "4", "ico", "", "12", root);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => ClusterGeneration.GenerateAsync(request, python));
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
