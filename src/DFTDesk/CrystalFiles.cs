namespace DFTDesk;

/// <summary>Recognizes input file formats; structure parsing and writing are handled solely by ASE.</summary>
internal enum ExportFormat { Cif, Poscar }

internal static class CrystalFiles
{
    public static ExportFormat? Detect(string file)
    {
        var name = Path.GetFileName(file);
        if (name.Equals("POSCAR", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("CONTCAR", StringComparison.OrdinalIgnoreCase))
            return ExportFormat.Poscar;

        return Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".cif" => ExportFormat.Cif,
            ".vasp" or ".poscar" => ExportFormat.Poscar,
            _ => null,
        };
    }
}
