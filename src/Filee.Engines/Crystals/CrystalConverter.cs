// Periodic atomic structures: VASP POSCAR/CONTCAR <-> crystallographic CIF.
// No external executables or Python installation are needed.

using Filee.Core.Conversion;

namespace Filee.Engines.Crystals;

public sealed class CrystalConverter : IConverter
{
    public string Id => "crystal";
    public string DisplayName => "Crystal structures (built-in)";
    public int MaxParallelism => 0;
    public IReadOnlyList<ConversionEdge> Edges { get; } =
    [
        new("poscar", "cif"),
        new("cif", "poscar"),
    ];

    public EngineStatus GetStatus() => EngineStatus.Available("VASP POSCAR / CONTCAR ↔ CIF; full-occupancy sites");

    public Task<IReadOnlyList<string>> ConvertAsync(ConversionStep step, IProgress<double>? progress,
        CancellationToken cancellationToken) => Task.Run<IReadOnlyList<string>>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var structure = step.From switch
        {
            "poscar" => CrystalFormat.ReadPoscar(File.ReadAllText(step.InputPath)),
            "cif" => CrystalFormat.ReadCif(File.ReadAllText(step.InputPath)),
            _ => throw new NotSupportedException($"Unsupported crystal structure format: {step.From}"),
        };
        progress?.Report(0.6);
        cancellationToken.ThrowIfCancellationRequested();
        var content = step.To switch
        {
            "poscar" => CrystalFormat.WritePoscar(structure),
            "cif" => CrystalFormat.WriteCif(structure),
            _ => throw new NotSupportedException($"Unsupported crystal structure format: {step.To}"),
        };
        if (step.Output.Allocate(step.To == "poscar" ? "vasp" : "cif") is not { } path)
            return [];
        cancellationToken.ThrowIfCancellationRequested();
        File.WriteAllText(path, content);
        progress?.Report(1);
        return [path];
    }, cancellationToken);
}
