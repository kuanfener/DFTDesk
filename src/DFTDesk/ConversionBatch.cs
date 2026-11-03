using System.Text;

namespace DFTDesk;

internal enum ExportFormat { Cif, Poscar }

internal static class ConversionBatch
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

    public static string ConvertOne(string input, string? outputDirectory, ExportFormat target, bool cartesian)
    {
        var source = Detect(input) ?? throw new FormatException($"无法识别文件格式：{Path.GetFileName(input)}");
        if (source == target)
            throw new InvalidOperationException($"{Path.GetFileName(input)} 已是目标格式。");
        var structure = source == ExportFormat.Cif
            ? CrystalFormat.ReadCif(File.ReadAllText(input))
            : CrystalFormat.ReadPoscar(File.ReadAllText(input));
        var content = target == ExportFormat.Cif
            ? CrystalFormat.WriteCif(structure)
            : CrystalFormat.WritePoscar(structure, cartesian);
        var directory = outputDirectory ?? Path.GetDirectoryName(Path.GetFullPath(input))!;
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"输出文件夹不存在：{directory}");
        var stem = Path.GetFileNameWithoutExtension(input);
        var extension = target == ExportFormat.Cif ? ".cif" : ".vasp";
        // Never overwrite a source or a previous conversion, including another file in this batch.
        for (var i = 0; i < 100_000; i++)
        {
            var suffix = i == 0 ? "" : $" ({i + 1})";
            var output = Path.Combine(directory, stem + suffix + extension);
            try
            {
                using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                try
                {
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                    writer.Write(content);
                }
                catch
                {
                    stream.Dispose();
                    File.Delete(output);
                    throw;
                }
                return output;
            }
            catch (IOException) when (File.Exists(output))
            {
                // An output name already exists: try the next suffix without modifying it.
            }
        }
        throw new IOException("输出目录已有太多同名文件。");
    }
}
