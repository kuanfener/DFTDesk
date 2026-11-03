using DFTDesk;

namespace DFTDesk.Tests;

public class CrystalFileTests
{
    [Theory]
    [InlineData("POSCAR", "Poscar")]
    [InlineData("contcar", "Poscar")]
    [InlineData("sample.vasp", "Poscar")]
    [InlineData("sample.poscar", "Poscar")]
    [InlineData("sample.CIF", "Cif")]
    public void Recognizes_supported_structure_files(string path, string expected)
        => Assert.Equal(expected, CrystalFiles.Detect(path)?.ToString());

    [Theory]
    [InlineData("example.pdb")]
    [InlineData("notes.txt")]
    [InlineData("OUTCAR")]
    public void Rejects_unrecognized_file_types(string path)
        => Assert.Null(CrystalFiles.Detect(path));
}
