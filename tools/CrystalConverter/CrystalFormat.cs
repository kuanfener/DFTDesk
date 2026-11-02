// Crystal structures use double precision internally, periodic fractional coordinates and Ångström units.
// CIF symmetry operations are expanded before writing POSCAR. POSCAR -> CIF uses P 1;
// Selective dynamics, oxidation states, disorder and space-group metadata cannot round-trip.

using System.Globalization;
using System.Text;

namespace Filee.CrystalConverter;

internal readonly record struct CrystalVector(double X, double Y, double Z)
{
    public static CrystalVector operator +(CrystalVector a, CrystalVector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static CrystalVector operator *(CrystalVector a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public static double Dot(CrystalVector a, CrystalVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static CrystalVector Cross(CrystalVector a, CrystalVector b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}

internal sealed record CrystalSite(string Element, CrystalVector Fractional);

internal sealed record CrystalStructure(string Title, CrystalVector A, CrystalVector B, CrystalVector C,
    IReadOnlyList<CrystalSite> Sites)
{
    public double Volume => CrystalVector.Dot(A, CrystalVector.Cross(B, C));

    public CrystalVector ToFractional(CrystalVector cartesian)
    {
        var v = Volume;
        if (Math.Abs(v) < 1e-10)
            throw new FormatException("The unit cell has zero volume.");
        return new(CrystalVector.Dot(cartesian, CrystalVector.Cross(B, C)) / v,
            CrystalVector.Dot(cartesian, CrystalVector.Cross(C, A)) / v,
            CrystalVector.Dot(cartesian, CrystalVector.Cross(A, B)) / v);
    }

    public CrystalVector ToCartesian(CrystalVector fractional) =>
        A * fractional.X + B * fractional.Y + C * fractional.Z;
}

internal static class CrystalFormat
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly HashSet<string> Elements = new((
        "H He Li Be B C N O F Ne Na Mg Al Si P S Cl Ar K Ca Sc Ti V Cr Mn Fe Co Ni Cu Zn Ga Ge As Se Br Kr " +
        "Rb Sr Y Zr Nb Mo Tc Ru Rh Pd Ag Cd In Sn Sb Te I Xe Cs Ba La Ce Pr Nd Pm Sm Eu Gd Tb Dy Ho Er Tm Yb Lu " +
        "Hf Ta W Re Os Ir Pt Au Hg Tl Pb Bi Po At Rn Fr Ra Ac Th Pa U Np Pu Am Cm Bk Cf Es Fm Md No Lr Rf Db Sg " +
        "Bh Hs Mt Ds Rg Cn Nh Fl Mc Lv Ts Og").Split(' '), StringComparer.Ordinal);

    private static double Number(string value)
    {
        value = value.Trim();
        var esd = value.IndexOf('(');
        if (esd >= 0) value = value[..esd];
        if (!double.TryParse(value, NumberStyles.Float, Invariant, out var number) || !double.IsFinite(number))
            throw new FormatException($"Invalid crystallographic number: '{value}'.");
        return number;
    }

    // VESTA-style exports: 10 decimals for cell geometry, 8 for atomic positions.
    // Keep the full double precision internally; this rounding is only for the exported text.
    private static string PoscarNumber(double value, int decimals)
    {
        // Suppress numerical noise such as 7.8e-16 from orthogonal CIF cells,
        // including the otherwise surprising negative zero in fixed-point output.
        if (Math.Abs(value) < 0.5 * Math.Pow(10, -decimals)) value = 0;
        return value.ToString($"F{decimals}", Invariant);
    }

    private static string PoscarVector(CrystalVector vector, int decimals) =>
        string.Join("  ", new[] { vector.X, vector.Y, vector.Z }
            .Select(v => PoscarNumber(v, decimals).PadLeft(decimals + 6)));
    private static string[] Fields(string line) => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static CrystalVector Vector(string line)
    {
        var f = Fields(line);
        if (f.Length < 3) throw new FormatException("Expected three vector components.");
        return new(Number(f[0]), Number(f[1]), Number(f[2]));
    }

    public static CrystalStructure ReadPoscar(string content)
    {
        var lines = content.Replace("\r", "").Split('\n');
        var index = 1;
        string Next()
        {
            while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index])) index++;
            if (index >= lines.Length) throw new FormatException("Incomplete POSCAR file.");
            return lines[index++].Trim();
        }

        var title = lines.Length > 0 ? lines[0].Trim() : "POSCAR";
        var scales = Fields(Next()).Select(Number).ToArray();
        if (scales.Length is not (1 or 3) || scales.Any(s => s == 0))
            throw new FormatException("POSCAR needs one nonzero or three positive scale factors.");
        var a = Vector(Next());
        var b = Vector(Next());
        var c = Vector(Next());
        static CrystalVector Scale(CrystalVector v, CrystalVector f) => new(v.X * f.X, v.Y * f.Y, v.Z * f.Z);
        CrystalVector factors;
        if (scales.Length == 3)
        {
            if (scales.Any(s => s <= 0)) throw new FormatException("Three POSCAR scale factors must be positive.");
            factors = new(scales[0], scales[1], scales[2]);
        }
        else
        {
            var factor = scales[0];
            if (factor < 0)
            {
                var volume = Math.Abs(CrystalVector.Dot(a, CrystalVector.Cross(b, c)));
                if (volume < 1e-10) throw new FormatException("POSCAR has a zero-volume cell.");
                factor = Math.Cbrt(-factor / volume);
            }
            factors = new(factor, factor, factor);
        }
        a = Scale(a, factors);
        b = Scale(b, factors);
        c = Scale(c, factors);

        var elements = Fields(Next());
        if (elements.Length == 0 || elements.All(s => int.TryParse(s, out _)))
            throw new FormatException("VASP 4 POSCAR lacks element names; add the VASP 5 element line.");
        elements = elements.Select(Element).ToArray();
        var counts = Fields(Next());
        if (counts.Length != elements.Length || counts.Any(s => !int.TryParse(s, NumberStyles.None, Invariant, out _)))
            throw new FormatException("POSCAR element names and atom counts do not match.");
        var amounts = counts.Select(s => int.Parse(s, Invariant)).ToArray();
        if (amounts.Any(n => n < 0) || amounts.Sum() == 0 || amounts.Sum() > 1_000_000)
            throw new FormatException("POSCAR atom count is invalid or too large.");
        var mode = Next();
        if (mode.StartsWith("S", StringComparison.OrdinalIgnoreCase)) mode = Next();
        var cartesian = mode.StartsWith("C", StringComparison.OrdinalIgnoreCase)
                        || mode.StartsWith("K", StringComparison.OrdinalIgnoreCase);
        var structure = new CrystalStructure(title, a, b, c, []);
        if (Math.Abs(structure.Volume) < 1e-10) throw new FormatException("POSCAR has a zero-volume cell.");
        var sites = new List<CrystalSite>();
        for (var species = 0; species < elements.Length; species++)
            for (var j = 0; j < amounts[species]; j++)
            {
                var coordinate = Vector(Next());
                if (cartesian) coordinate = structure.ToFractional(Scale(coordinate, factors));
                sites.Add(new(elements[species], coordinate));
            }
        return structure with { Sites = sites };
    }

    public static string WritePoscar(CrystalStructure structure, bool cartesian = false)
    {
        Validate(structure);
        var elements = structure.Sites.Select(s => s.Element).Distinct().ToArray();
        var sb = new StringBuilder();
        sb.AppendLine(string.IsNullOrWhiteSpace(structure.Title) ? "Converted crystal structure" : structure.Title.Replace('\n', ' '));
        sb.AppendLine("1.0");
        foreach (var v in new[] { structure.A, structure.B, structure.C })
            sb.AppendLine(PoscarVector(v, 10));
        sb.AppendLine(string.Join(" ", elements));
        sb.AppendLine(string.Join(" ", elements.Select(e => structure.Sites.Count(s => s.Element == e).ToString(Invariant))));
        sb.AppendLine(cartesian ? "Cartesian" : "Direct");
        foreach (var element in elements)
            foreach (var site in structure.Sites.Where(s => s.Element == element))
            {
                var fractional = Wrap(site.Fractional);
                var v = cartesian ? structure.ToCartesian(fractional) : fractional;
                sb.AppendLine(PoscarVector(v, 8));
            }
        return sb.ToString();
    }

    public static string WriteCif(CrystalStructure structure)
    {
        Validate(structure);
        static double Angle(CrystalVector u, CrystalVector v) =>
            Math.Acos(Math.Clamp(CrystalVector.Dot(u, v) / (u.Length * v.Length), -1, 1)) * 180 / Math.PI;
        var sb = new StringBuilder();
        sb.AppendLine("data_filee");
        sb.AppendLine("_space_group_name_H-M_alt 'P 1'");
        sb.AppendLine("_space_group_IT_number 1");
        sb.AppendLine($"_cell_length_a {PoscarNumber(structure.A.Length, 10)}");
        sb.AppendLine($"_cell_length_b {PoscarNumber(structure.B.Length, 10)}");
        sb.AppendLine($"_cell_length_c {PoscarNumber(structure.C.Length, 10)}");
        sb.AppendLine($"_cell_angle_alpha {PoscarNumber(Angle(structure.B, structure.C), 10)}");
        sb.AppendLine($"_cell_angle_beta {PoscarNumber(Angle(structure.A, structure.C), 10)}");
        sb.AppendLine($"_cell_angle_gamma {PoscarNumber(Angle(structure.A, structure.B), 10)}");
        sb.AppendLine("loop_");
        sb.AppendLine("_space_group_symop_operation_xyz");
        sb.AppendLine("'x,y,z'");
        sb.AppendLine("loop_");
        sb.AppendLine("_atom_site_label");
        sb.AppendLine("_atom_site_type_symbol");
        sb.AppendLine("_atom_site_fract_x");
        sb.AppendLine("_atom_site_fract_y");
        sb.AppendLine("_atom_site_fract_z");
        sb.AppendLine("_atom_site_occupancy");
        var labels = new Dictionary<string, int>();
        foreach (var site in structure.Sites)
        {
            labels.TryGetValue(site.Element, out var index);
            labels[site.Element] = ++index;
            var v = Wrap(site.Fractional);
            sb.AppendLine($"{site.Element}{index} {site.Element} {PoscarNumber(v.X, 8)} {PoscarNumber(v.Y, 8)} {PoscarNumber(v.Z, 8)} 1");
        }
        return sb.ToString();
    }

    private static void Validate(CrystalStructure structure)
    {
        if (structure.Sites.Count == 0 || Math.Abs(structure.Volume) < 1e-10 ||
            new[] { structure.A, structure.B, structure.C }.Any(v => v.Length < 1e-8))
            throw new FormatException("A crystal must have atoms and a non-degenerate unit cell.");
        if (structure.Sites.Any(s => !double.IsFinite(s.Fractional.X) || !double.IsFinite(s.Fractional.Y) ||
                                     !double.IsFinite(s.Fractional.Z)))
            throw new FormatException("Crystal coordinates must be finite.");
    }

    private static CrystalVector Wrap(CrystalVector v) => new(W(v.X), W(v.Y), W(v.Z));
    private static double W(double v)
    {
        v -= Math.Floor(v);
        return v > 1 - 1e-12 ? 0 : v;
    }

    private static string Element(string text)
    {
        var prefix = new string(text.TakeWhile(char.IsLetter).Take(2).ToArray());
        if (prefix.Length == 0) throw new FormatException($"Missing element name in '{text}'.");
        var canonical = char.ToUpperInvariant(prefix[0]) + prefix[1..].ToLowerInvariant();
        if (!Elements.Contains(canonical) && prefix.Length == 2)
            canonical = char.ToUpperInvariant(prefix[0]).ToString();
        if (!Elements.Contains(canonical)) throw new FormatException($"Unrecognised element '{text}'.");
        return canonical;
    }

    public static CrystalStructure ReadCif(string content)
    {
        var tokens = Tokenize(content);
        var scalars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var loops = new List<(string[] Tags, List<string[]> Rows)>();
        var title = "CIF crystal";
        for (var i = 0; i < tokens.Count;)
        {
            var word = tokens[i].Value;
            if (!tokens[i].Quoted && word.StartsWith("data_", StringComparison.OrdinalIgnoreCase))
            {
                if (loops.Any(l => l.Tags.Contains("_atom_site_fract_x") || l.Tags.Contains("_atom_site_cartn_x")))
                    break; // first structure-containing data block
                title = word.Length > 5 ? word[5..] : "CIF crystal";
                i++;
                continue;
            }
            if (!tokens[i].Quoted && word.Equals("loop_", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                var tags = new List<string>();
                while (i < tokens.Count && !tokens[i].Quoted && tokens[i].Value.StartsWith('_'))
                    tags.Add(Tag(tokens[i++].Value));
                if (tags.Count == 0) throw new FormatException("CIF loop has no field names.");
                var rows = new List<string[]>();
                while (i < tokens.Count && !IsCifControl(tokens[i]))
                {
                    if (i + tags.Count > tokens.Count || tokens.Skip(i).Take(tags.Count).Any(IsCifControl))
                        throw new FormatException("Incomplete CIF loop row.");
                    rows.Add(tokens.Skip(i).Take(tags.Count).Select(t => t.Value).ToArray());
                    i += tags.Count;
                }
                loops.Add((tags.ToArray(), rows));
                continue;
            }
            if (!tokens[i].Quoted && word.StartsWith('_'))
            {
                if (i + 1 >= tokens.Count || IsCifControl(tokens[i + 1]))
                    throw new FormatException($"CIF field {word} has no value.");
                scalars[Tag(word)] = tokens[i + 1].Value;
                i += 2;
                continue;
            }
            i++;
        }

        string Value(string key) => scalars.TryGetValue(key, out var v) ? v :
            throw new FormatException($"CIF is missing {key}.");
        var a = Number(Value("_cell_length_a"));
        var b = Number(Value("_cell_length_b"));
        var c = Number(Value("_cell_length_c"));
        var alpha = Number(Value("_cell_angle_alpha")) * Math.PI / 180;
        var beta = Number(Value("_cell_angle_beta")) * Math.PI / 180;
        var gamma = Number(Value("_cell_angle_gamma")) * Math.PI / 180;
        if (a <= 0 || b <= 0 || c <= 0 || Math.Sin(gamma) <= 1e-10)
            throw new FormatException("Invalid CIF cell lengths or angles.");
        var ca = Math.Cos(alpha);
        var cb = Math.Cos(beta);
        var cg = Math.Cos(gamma);
        var sg = Math.Sin(gamma);
        var cx = c * cb;
        var cy = c * (ca - cb * cg) / sg;
        var cz2 = c * c - cx * cx - cy * cy;
        if (cz2 <= 1e-12) throw new FormatException("Invalid CIF unit cell geometry.");
        var structure = new CrystalStructure(title, new(a, 0, 0), new(b * cg, b * sg, 0),
            new(cx, cy, Math.Sqrt(cz2)), []);

        var operations = new List<string>();
        foreach (var (tags, rows) in loops)
        {
            var col = Array.FindIndex(tags, t => t is "_space_group_symop_operation_xyz" or "_symmetry_equiv_pos_as_xyz");
            if (col >= 0) operations.AddRange(rows.Select(row => row[col]));
        }
        foreach (var key in new[] { "_space_group_symop_operation_xyz", "_symmetry_equiv_pos_as_xyz" })
            if (scalars.TryGetValue(key, out var op)) operations.Add(op);
        if (operations.Count == 0)
        {
            var number = scalars.GetValueOrDefault("_space_group_it_number") ??
                         scalars.GetValueOrDefault("_symmetry_int_tables_number");
            if (number is not null && Number(number) != 1)
                throw new FormatException("Non-P1 CIF has no symmetry operators, so its full atomic cell is unknown.");
            var hm = scalars.GetValueOrDefault("_space_group_name_h-m_alt") ??
                     scalars.GetValueOrDefault("_symmetry_space_group_name_h-m");
            if (hm is not null && !hm.Replace(" ", "").Equals("P1", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("CIF names a non-P1 space group but gives no symmetry operators.");
            operations.Add("x,y,z");
        }

        var sites = new List<CrystalSite>();
        var seen = new HashSet<(string Element, long X, long Y, long Z)>();
        foreach (var (tags, rows) in loops)
        {
            var x = Array.IndexOf(tags, "_atom_site_fract_x");
            var y = Array.IndexOf(tags, "_atom_site_fract_y");
            var z = Array.IndexOf(tags, "_atom_site_fract_z");
            var cartesian = false;
            if (x < 0 || y < 0 || z < 0)
            {
                x = Array.IndexOf(tags, "_atom_site_cartn_x");
                y = Array.IndexOf(tags, "_atom_site_cartn_y");
                z = Array.IndexOf(tags, "_atom_site_cartn_z");
                cartesian = x >= 0 && y >= 0 && z >= 0;
            }
            if (x < 0 || y < 0 || z < 0) continue;
            var elementColumn = Array.IndexOf(tags, "_atom_site_type_symbol");
            if (elementColumn < 0) elementColumn = Array.IndexOf(tags, "_atom_site_label");
            if (elementColumn < 0) throw new FormatException("CIF atom sites have no element symbols or labels.");
            var occupancy = Array.IndexOf(tags, "_atom_site_occupancy");
            foreach (var row in rows)
            {
                if (occupancy >= 0 && (row[occupancy] is "." or "?" || Math.Abs(Number(row[occupancy]) - 1) > 1e-5))
                    throw new FormatException("Disordered or partially occupied CIF sites cannot be represented by POSCAR.");
                var element = Element(row[elementColumn]);
                var original = new CrystalVector(Number(row[x]), Number(row[y]), Number(row[z]));
                if (cartesian) original = structure.ToFractional(original);
                foreach (var operation in operations)
                {
                    var coordinate = Wrap(ApplySymmetry(operation, original));
                    const long grid = 10_000_000;
                    var key = (element, (long)Math.Round(coordinate.X * grid) % grid,
                        (long)Math.Round(coordinate.Y * grid) % grid,
                        (long)Math.Round(coordinate.Z * grid) % grid);
                    if (seen.Add(key)) sites.Add(new(element, coordinate));
                    if (sites.Count > 1_000_000)
                        throw new FormatException("CIF contains too many expanded atomic sites.");
                }
            }
        }
        if (sites.Count == 0) throw new FormatException("CIF has no atom-site coordinates.");
        return structure with { Sites = sites };
    }

    private static CrystalVector ApplySymmetry(string operation, CrystalVector v)
    {
        var axes = operation.Replace(" ", "").Split(',');
        if (axes.Length != 3) throw new FormatException($"Invalid CIF symmetry operation: {operation}");
        double Axis(string expression)
        {
            double result = 0;
            var position = 0;
            while (position < expression.Length)
            {
                var sign = 1;
                if (expression[position] is '+' or '-')
                    sign = expression[position++] == '-' ? -1 : 1;
                var start = position;
                while (position < expression.Length && expression[position] is not ('+' or '-')) position++;
                var term = expression[start..position];
                if (term.Length == 0) throw new FormatException($"Invalid CIF symmetry expression: {expression}");
                var axis = term.IndexOfAny(['x', 'y', 'z', 'X', 'Y', 'Z']);
                if (axis >= 0)
                {
                    var coefficient = term.Remove(axis, 1).TrimEnd('*');
                    var factor = coefficient.Length == 0 ? 1 : Fraction(coefficient);
                    result += sign * factor * (char.ToLowerInvariant(term[axis]) switch
                    {
                        'x' => v.X,
                        'y' => v.Y,
                        'z' => v.Z,
                        _ => throw new FormatException("Unknown symmetry axis."),
                    });
                }
                else result += sign * Fraction(term);
            }
            if (expression.Length == 0) throw new FormatException("Empty CIF symmetry coordinate.");
            return result;
        }
        return new(Axis(axes[0]), Axis(axes[1]), Axis(axes[2]));
    }

    private static double Fraction(string text)
    {
        var parts = text.Split('/');
        if (parts.Length == 1) return Number(parts[0]);
        if (parts.Length != 2 || Number(parts[1]) == 0)
            throw new FormatException($"Invalid CIF fraction: {text}");
        return Number(parts[0]) / Number(parts[1]);
    }

    private static string Tag(string raw) => raw.ToLowerInvariant().Replace('.', '_');
    private readonly record struct CifToken(string Value, bool Quoted);
    private static bool IsCifControl(CifToken token) => !token.Quoted &&
        (token.Value.StartsWith('_') || token.Value.Equals("loop_", StringComparison.OrdinalIgnoreCase) ||
         token.Value.Equals("stop_", StringComparison.OrdinalIgnoreCase) ||
         token.Value.StartsWith("data_", StringComparison.OrdinalIgnoreCase) ||
         token.Value.StartsWith("save_", StringComparison.OrdinalIgnoreCase));

    private static List<CifToken> Tokenize(string text)
    {
        var tokens = new List<CifToken>();
        var atLineStart = true;
        for (var i = 0; i < text.Length;)
        {
            var c = text[i];
            if (c == '\r') { i++; continue; }
            if (c == '\n') { i++; atLineStart = true; continue; }
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '#')
            {
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }
            if (c == ';' && atLineStart)
            {
                i++;
                var start = i;
                while (i < text.Length && !(text[i] == '\n' && i + 1 < text.Length && text[i + 1] == ';')) i++;
                if (i >= text.Length) throw new FormatException("Unterminated CIF multiline field.");
                tokens.Add(new(text[start..i].Trim(), true));
                i += 2;
                atLineStart = false;
                continue;
            }
            atLineStart = false;
            if (c is '\'' or '"')
            {
                var quote = c;
                var start = ++i;
                while (i < text.Length && !(text[i] == quote && (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1])))) i++;
                if (i >= text.Length) throw new FormatException("Unterminated quoted CIF value.");
                tokens.Add(new(text[start..i], true));
                i++;
            }
            else
            {
                var start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '#') i++;
                tokens.Add(new(text[start..i], false));
            }
        }
        return tokens;
    }
}
