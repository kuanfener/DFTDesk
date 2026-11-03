using DFTDesk;

namespace DFTDesk.Tests;

public class CrystalFileTests
{
    [Theory]
    [InlineData("POSCAR", ExportFormat.Poscar)]
    [InlineData("contcar", ExportFormat.Poscar)]
    [InlineData("sample.vasp", ExportFormat.Poscar)]
    [InlineData("sample.poscar", ExportFormat.Poscar)]
    [InlineData("sample.CIF", ExportFormat.Cif)]
    public void Recognizes_supported_structure_files(string path, ExportFormat expected)
        => Assert.Equal(expected, CrystalFiles.Detect(path));

    [Theory]
    [InlineData("example.pdb")]
    [InlineData("notes.txt")]
    [InlineData("OUTCAR")]
    public void Rejects_unrecognized_file_types(string path)
        => Assert.Null(CrystalFiles.Detect(path));
}
