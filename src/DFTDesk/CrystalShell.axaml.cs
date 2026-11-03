using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Filee.App.Controls;
using Filee.App.Services;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Avalonia.Media.Imaging;
using DrawingColor = System.Drawing.Color;
using AvColor = Avalonia.Media.Color;

namespace DFTDesk;

/// <summary>
/// The standalone app's Filee Avalonia shell. Original Filee styles, icon
/// geometry and the DonutMenu source are linked, so the Windows Forms conversion
/// backend does not have to hand-draw any Filee visual.
/// </summary>
internal sealed partial class CrystalShell : UserControl
{
    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DFTDesk", "theme.json");
    private static readonly string EngineSettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DFTDesk", "engine.json");
    private static readonly string[] Swatches =
        ["#7F77DD", "#1D9E75", "#D85A30", "#378ADD", "#D4537E", "#BA7517", "#639922", "#5F5E5A"];

    private Control[] _pages = [];
    // Non-interactive preview uses the same renderer and segment geometry as the real Explorer wheel.
    private DonutMenu _themeWheel = null!;
    private readonly List<(Button Button, AvColor Color)> _swatchButtons = [];
    private AvColor _accent = AvColor.Parse("#7F77DD");
    private double _radius = 16;
    private double _opacity = 96;
    private int _windowWidth = 1080;
    private int _windowHeight = 760;
    private bool _reduced;
    private bool _collapsed;
    private bool _initializing = true;
    // ASE is the only engine. Older engine.json settings retain AseCartesian.
    private bool _aseCartesian = true;

    public event Action? BrowseFilesRequested;
    public event Action? BrowseFolderRequested;
    public event Action? ClearFilesRequested;
    public event Action<string[]>? RemoveFilesRequested;
    public event Action<string[]>? FilesDropped;
    public event Action<string[]>? HomeFilesDropped;
    public event Action<ExportFormat>? FormatChosen;
    public event Action? CancelRequested;
    public event Action<string>? OutputDirectoryChanged;
    public event Action<bool>? CartesianChanged;
    public event Action<DrawingColor, double>? ThemeChanged;
    public event Action? AseCheckRequested;
    public event Action? DosEngineCheckRequested;
    public event Action? DosBrowseRequested;
    public event Action? DosAnalyzeRequested;
    public event Action? DosExportRequested;
    public event Action? DosCancelRequested;
    public event Action? CohpEngineCheckRequested;
    public event Action? CohpBrowseRequested;
    public event Action? CohpAnalyzeRequested;
    public event Action? CohpExportRequested;
    public event Action? CohpCancelRequested;
    public event Action? ClusterBrowseRequested;
    public event Action? ClusterGenerateRequested;
    public event Action? ClusterCancelRequested;
    public event Action<int, int>? WindowSizeRequested;

    public CrystalShell()
    {
        AvaloniaXamlLoader.Load(this);
        _pages =
        [
            Part<Control>("HomePage"), Part<Control>("ConversionPage"),
            Part<Control>("ClusterPage"), Part<Control>("DosPage"),
            Part<Control>("CohpPage"), Part<Control>("ThemePage"), Part<Control>("WorkEnginePage"),
            Part<Control>("AboutPage")
        ];
        var pageContainer = Part<Grid>("PageContainer");
        foreach (var page in _pages)
            pageContainer.Children.Remove(page);
        Part<ContentControl>("PageHost").Content = null;

        _themeWheel = DftDonutFactory.Create(60, ToDrawing(_accent));
        Part<ContentControl>("ThemeDonutPreviewHost").Content = _themeWheel;
        // The preview surface handles hover while the embedded donut stays non-interactive.
        var previewSurface = Part<Border>("ThemeDonutPreviewSurface");
        previewSurface.PointerMoved += (_, e) =>
        {
            var hit = _themeWheel.HitTest(e.GetPosition(_themeWheel));
            _themeWheel.SetExternalHighlight(hit.Kind == DonutHitKind.Segment ? hit.Index : -1);
        };
        previewSurface.PointerExited += (_, _) => _themeWheel.SetExternalHighlight(-1);

        var zone = Part<Border>("DropZone");
        DragDrop.SetAllowDrop(zone, true);
        DragDrop.AddDragEnterHandler(zone, (_, e) =>
        {
            var files = DftDonutFactory.Files(e.DataTransfer);
            e.DragEffects = files.Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
            zone.Classes.Set("over", files.Length > 0);
            e.Handled = true;
        });
        DragDrop.AddDragOverHandler(zone, (_, e) =>
        {
            e.DragEffects = DftDonutFactory.Files(e.DataTransfer).Length > 0
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        });
        DragDrop.AddDragLeaveHandler(zone, (_, _) => zone.Classes.Set("over", false));
        DragDrop.AddDropHandler(zone, (_, e) =>
        {
            zone.Classes.Set("over", false);
            var files = DftDonutFactory.Files(e.DataTransfer);
            if (files.Length > 0)
            {
                e.DragEffects = DragDropEffects.Copy;
                HomeFilesDropped?.Invoke(files);
            }
            e.Handled = true;
        });
        Part<TextBox>("OutputDirectory").PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                OutputDirectoryChanged?.Invoke(Part<TextBox>("OutputDirectory").Text ?? "");
        };
        Part<CheckBox>("CartesianOption").PropertyChanged += (_, e) =>
        {
            if (e.Property != ToggleButton.IsCheckedProperty) return;
            var cartesian = Part<CheckBox>("CartesianOption").IsChecked == true;
            if (!_initializing)
            {
                _aseCartesian = cartesian;
                SaveEngineSettings();
            }
            CartesianChanged?.Invoke(cartesian);
        };
        Part<CheckBox>("ReduceMotionOption").PropertyChanged += (_, e) =>
        {
            if (e.Property == ToggleButton.IsCheckedProperty && !_initializing)
            {
                _reduced = Part<CheckBox>("ReduceMotionOption").IsChecked == true;
                ApplyTheme();
            }
        };
        BuildSwatches();
        LoadSettings();
        LoadEngineSettings();
        _initializing = false;
        ApplyTheme();
        SetPage(0);
        AttachedToVisualTree += (_, _) =>
        {
            var window = TopLevel.GetTopLevel(this);
            window?.Classes.Set("reduced-motion", _reduced);
        };
    }

    private string _savedMode = "System";
    private T Part<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"Missing DFTDesk control: {name}");

    public string OutputPath => Part<TextBox>("OutputDirectory").Text ?? "";
    public bool Cartesian => Part<CheckBox>("CartesianOption").IsChecked == true;
    public DrawingColor CurrentAccent => ToDrawing(_accent);
    public double CurrentOpacity => _opacity / 100;
    public (int Width, int Height) StartupWindowSize => (_windowWidth, _windowHeight);
    public string PythonExecutablePath => Part<TextBox>("AsePythonExecutable").Text?.Trim() is { Length: > 0 } executable
        ? executable : "python";
    public void SetAseStatus(string text)
    {
        Part<TextBlock>("AseHealthText").Text = text;
        Part<TextBlock>("HomeAseStatus").Text = text;
    }

    public void SetDosEngineStatus(string text) =>
        Part<TextBlock>("DosEngineHealthText").Text = text;

    public void SetDosInput(string path)
    {
        Part<TextBlock>("DosInputFile").Text = path;
        ClearDosPreview();
        Part<ListBox>("DosElementList").ItemsSource = Array.Empty<string>();
        Part<TextBlock>("DosMetadataStatus").Text = "正在读取 DOS 元数据…";
    }

    public void SetDosMetadata(DosData data)
    {
        var list = Part<ListBox>("DosElementList");
        list.ItemsSource = data.Elements;
        var preferred = new[] { "Pt", "Fe", "Co", "Ni" };
        var chosen = data.Elements.Where(preferred.Contains).ToArray();
        if (chosen.Length == 0 && data.Elements.Length > 0)
            chosen = [data.Elements[0]];
        foreach (var element in chosen)
            list.SelectedItems?.Add(element);
        var spin = data.SpinPolarized ? "自旋极化" : "非自旋极化";
        Part<TextBlock>("DosMetadataStatus").Text =
            $"{data.Info}；{spin}；Ef = {data.Fermi:0.0000} eV；可用能量 [{data.EnergyMin:0.00}, {data.EnergyMax:0.00}] eV。" +
            (data.HasPdos ? $"可用元素：{string.Join(", ", data.Elements)}。" :
                "当前只能绘制 TDOS；若是 DOSCAR，请将同次计算的 POSCAR 放在其目录内。");
    }

    public DosRequest ReadDosRequest(string source)
    {
        static double Parse(string? value, string label)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
                !double.IsFinite(parsed))
                throw new ArgumentException($"{label}需输入有效数字，例如 -8 或 4。");
            return parsed;
        }
        var orbitals = new List<string>();
        if (Part<CheckBox>("DosOrbitalS").IsChecked == true) orbitals.Add("s");
        if (Part<CheckBox>("DosOrbitalP").IsChecked == true) orbitals.Add("p");
        if (Part<CheckBox>("DosOrbitalD").IsChecked == true) orbitals.Add("d");
        var spin = Part<ComboBox>("DosSpinMode").SelectedIndex switch
        {
            1 => "up",
            2 => "down",
            _ => "both",
        };
        return new DosRequest(source,
            Part<ListBox>("DosElementList").SelectedItems?.OfType<string>().ToArray() ?? [],
            orbitals.ToArray(), spin,
            Parse(Part<TextBox>("DosEnergyMin").Text, "能量下限"),
            Parse(Part<TextBox>("DosEnergyMax").Text, "能量上限"));
    }

    public void SetDosBusy(bool busy)
    {
        Part<Button>("DosAnalyzeButton").IsEnabled = !busy;
        Part<Button>("DosCancelButton").IsEnabled = busy;
        if (busy) Part<Button>("DosExportButton").IsEnabled = false;
    }

    public void SetDosExportReady(bool ready) =>
        Part<Button>("DosExportButton").IsEnabled = ready;

    public void SetDosStatus(string text) =>
        Part<TextBlock>("DosStatus").Text = text;

    public void SetDosPreview(DosData result)
    {
        var image = Part<Image>("DosPlotImage");
        var bitmap = new Bitmap(result.Png);
        var old = image.Source as Bitmap;
        image.Source = bitmap;
        old?.Dispose();
        image.IsVisible = true;
        Part<TextBlock>("DosPlotPlaceholder").IsVisible = false;
        Part<TextBox>("DosSummary").Text = result.Summary;
    }

    public void ClearDosPreview()
    {
        var image = Part<Image>("DosPlotImage");
        var old = image.Source as Bitmap;
        image.Source = null;
        old?.Dispose();
        image.IsVisible = false;
        Part<TextBlock>("DosPlotPlaceholder").IsVisible = true;
        Part<TextBox>("DosSummary").Text = "选择元素、轨道与能量窗口后，点击分析并绘图。";
        SetDosExportReady(false);
    }


    public void SetCohpEngineStatus(string text) =>
        Part<TextBlock>("CohpEngineHealthText").Text = text;

    public void SetCohpInput(string path)
    {
        Part<TextBlock>("CohpInputFile").Text = path;
        Part<TextBlock>("CohpMetadataStatus").Text = "正在读取 LOBSTER 键列表…";
        Part<ListBox>("CohpBondList").ItemsSource = Array.Empty<CohpBond>();
        ClearCohpPreview();
    }

    public void SetCohpMetadata(CohpData info)
    {
        var list = Part<ListBox>("CohpBondList");
        list.ItemsSource = info.Bonds;
        if (info.Bonds.Length > 0)
            list.SelectedItems?.Add(info.Bonds[0]);
        Part<TextBlock>("CohpMetadataStatus").Text =
            $"识别 {info.Bonds.Length} 个键；" +
            (info.SpinPolarized ? "自旋极化；" : "非自旋极化；") +
            (info.HasCurves
                ? $"COHP 能量范围 [{info.EnergyMin:0.00}, {info.EnergyMax:0.00}] eV（相对 Ef）。"
                : "目前仅有 ICOHP(Ef) 键列表，绘图需要同目录的 COHPCAR。");
    }

    public CohpRequest ReadCohpRequest(string input)
    {
        static double Parse(string? value, string label)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
                !double.IsFinite(number))
                throw new ArgumentException($"{label}需要有效数字，例如 -8 或 4。");
            return number;
        }
        var labels = Part<ListBox>("CohpBondList").SelectedItems?
            .OfType<CohpBond>().Select(bond => bond.Label).ToArray() ?? [];
        if (labels.Length == 0)
            throw new ArgumentException("请至少选择一个键。");
        var spin = Part<ComboBox>("CohpSpinMode").SelectedIndex switch
        {
            1 => "up",
            2 => "down",
            _ => "both",
        };
        return new CohpRequest(input, labels, spin,
            Parse(Part<TextBox>("CohpEnergyMin").Text, "能量下限"),
            Parse(Part<TextBox>("CohpEnergyMax").Text, "能量上限"),
            Part<CheckBox>("CohpNegative").IsChecked == true);
    }

    public void SetCohpBusy(bool busy)
    {
        Part<Button>("CohpAnalyzeButton").IsEnabled = !busy;
        Part<Button>("CohpCancelButton").IsEnabled = busy;
        if (busy) Part<Button>("CohpExportButton").IsEnabled = false;
    }

    public void SetCohpExportReady(bool ready) =>
        Part<Button>("CohpExportButton").IsEnabled = ready;

    public void SetCohpStatus(string status) =>
        Part<TextBlock>("CohpStatus").Text = status;

    public void SetCohpPreview(CohpData result)
    {
        ClearCohpPreview();
        var image = Part<Image>("CohpPlotImage");
        if (result.HasCurves)
        {
            image.Source = new Bitmap(result.Png);
            image.IsVisible = true;
            Part<TextBlock>("CohpPlotPlaceholder").IsVisible = false;
        }
        else
        {
            Part<TextBlock>("CohpPlotPlaceholder").Text =
                "仅有 ICOHPLIST：已生成 ICOHP(Ef) 表格，无法生成 COHP 曲线。";
        }
        Part<TextBox>("CohpSummary").Text = result.Summary;
    }

    public void ClearCohpPreview()
    {
        var image = Part<Image>("CohpPlotImage");
        var old = image.Source as Bitmap;
        image.Source = null;
        old?.Dispose();
        image.IsVisible = false;
        var placeholder = Part<TextBlock>("CohpPlotPlaceholder");
        placeholder.IsVisible = true;
        placeholder.Text = "分析完成后在此显示 COHP/ICOHP 曲线；仅 ICOHPLIST 时提供数据表。";
        Part<TextBox>("CohpSummary").Text = "请选择键、自旋和能量窗口后点击分析。";
        SetCohpExportReady(false);
    }

    public ClusterRequest ReadClusterRequest() => new(
        Faceted: Part<ComboBox>("ClusterMode").SelectedIndex == 1,
        Element: Part<TextBox>("ClusterElement").Text ?? "",
        Numbers: Part<TextBox>("ClusterNumbers").Text ?? "",
        Facets: Part<TextBox>("ClusterFacets").Text ?? "all",
        LatticeConstant: Part<TextBox>("ClusterLatticeConstant").Text ?? "",
        Vacuum: Part<TextBox>("ClusterVacuum").Text ?? "12",
        OutputDirectory: Part<TextBox>("ClusterOutputDirectory").Text ?? "",
        Cartesian: Part<CheckBox>("ClusterCartesian").IsChecked == true);

    public string ClusterOutputPath
    {
        get => Part<TextBox>("ClusterOutputDirectory").Text ?? "";
        set => Part<TextBox>("ClusterOutputDirectory").Text = value;
    }

    public void SetClusterBusy(bool busy)
    {
        Part<Button>("ClusterGenerateButton").IsEnabled = !busy;
        Part<Button>("ClusterCancelButton").IsEnabled = busy;
    }

    public void SetClusterStatus(string status) => Part<TextBlock>("ClusterStatus").Text = status;
    public void SetClusterLog(string log) => Part<TextBox>("ClusterLog").Text = log;

    public void SetFiles(IEnumerable<string> files)
    {
        var items = files.ToArray();
        Part<ListBox>("FileList").ItemsSource = items;
        var imported = items.Length > 0;
        Part<StackPanel>("EmptyImportPanel").IsVisible = !imported;
        Part<StackPanel>("ImportedFilesPanel").IsVisible = imported;
        Part<TextBlock>("ImportedFilesCount").Text = $"已导入 {items.Length} 个结构文件";
    }

    public void SetStatus(string status) => Part<TextBlock>("BatchStatus").Text = status;

    public void SetProgress(double fraction)
    {
        Part<ProgressBar>("BatchProgress").Value = Math.Clamp(fraction * 100, 0, 100);
    }
    public void SetBusy(bool busy) => Part<Button>("CancelConversionButton").IsEnabled = busy;

    public void AppendRecord(string entry)
    {
        Part<TextBlock>("HistoryEmpty").IsVisible = false;
        var stack = Part<StackPanel>("HistoryRecords");
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(new TextBlock { Text = entry, TextWrapping = TextWrapping.Wrap });
        var card = new Border { Child = body, Padding = new Thickness(14, 10) };
        card.Classes.Add("card");
        stack.Children.Insert(0, card);
        while (stack.Children.Count > 30)
            stack.Children.RemoveAt(stack.Children.Count - 1);
    }

    public void SetThemeFromConverter(DrawingColor color, double opacity)
    {
        _accent = AvColor.FromRgb(color.R, color.G, color.B);
        _opacity = opacity * 100;
        ApplyTheme();
    }

    private void SetPage(int index)
    {
        if (index < 0 || index >= _pages.Length) return;
        var host = Part<ContentControl>("PageHost");
        if (ReferenceEquals(host.Content, _pages[index])) return;
        _pages[index].IsVisible = true;
        host.Content = _pages[index];
    }

    private void BuildSwatches()
    {
        var panel = Part<WrapPanel>("AccentSwatches");
        foreach (var hex in Swatches)
        {
            var color = AvColor.Parse(hex);
            var button = new Button
            {
                Width = 39, Height = 39, Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 5, 4),
                Content = new Border
                {
                    Width = 33, Height = 33, CornerRadius = new CornerRadius(999),
                    Background = new SolidColorBrush(color),
                },
            };
            button.Classes.Add("theme-swatch");
            button.Click += (_, _) => { _accent = color; ApplyTheme(); };
            panel.Children.Add(button);
            _swatchButtons.Add((button, color));
        }
    }

    private void ApplyTheme()
    {
        var app = Application.Current;
        if (app is null) return;
        app.RequestedThemeVariant = _savedMode switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
        SoftPalette.ApplyCornerRadius(app.Resources, _radius);
        // Keep one DFTDesk Soft palette; ignore any legacy MaterialYou theme.json flag.
        app.Resources.ThemeDictionaries.Remove(ThemeVariant.Light);
        app.Resources.ThemeDictionaries.Remove(ThemeVariant.Dark);
        SoftPalette.ApplyAccent(app.Resources, _accent);
        Motion.Configure(_reduced, () => false);
        TopLevel.GetTopLevel(this)?.Classes.Set("reduced-motion", _reduced);
        DftDonutFactory.ApplyAccent(_themeWheel, ToDrawing(_accent));
        _themeWheel.SliceOpacity = _opacity / 100; // live preview, same value as the Explorer wheel
        Part<TextBlock>("CornerValue").Text = _radius.ToString("0") + " px";
        Part<TextBlock>("OpacityValue").Text = _opacity.ToString("0") + "%";
        Part<TextBox>("AccentHex").Text = $"#{_accent.R:X2}{_accent.G:X2}{_accent.B:X2}";
        foreach (var swatch in _swatchButtons)
            swatch.Button.Classes.Set("selected", swatch.Color == _accent);
        Part<Button>("AppearanceSystemButton").Classes.Set("selected", _savedMode == "System");
        Part<Button>("AppearanceLightButton").Classes.Set("selected", _savedMode == "Light");
        Part<Button>("AppearanceDarkButton").Classes.Set("selected", _savedMode == "Dark");
        ThemeChanged?.Invoke(ToDrawing(_accent), _opacity / 100);
    }

    private static DrawingColor ToDrawing(AvColor color) =>
        DrawingColor.FromArgb(color.R, color.G, color.B);

    private sealed class EngineOptions
    {
        public string PythonExecutable { get; set; } = "python";
        // An old engine.json may also contain Name and NativeCartesian; ignore both.
        public bool AseCartesian { get; set; } = true;
    }

    private void LoadEngineSettings()
    {
        try
        {
            if (File.Exists(EngineSettingsFile))
            {
                var settings = JsonSerializer.Deserialize<EngineOptions>(File.ReadAllText(EngineSettingsFile));
                if (settings is not null)
                {
                    _aseCartesian = settings.AseCartesian;
                    Part<TextBox>("AsePythonExecutable").Text =
                        string.IsNullOrWhiteSpace(settings.PythonExecutable) ? "python" : settings.PythonExecutable;
                }
            }
        }
        catch (Exception) { /* Malformed settings fall back to Python and Cartesian coordinates. */ }

        Part<CheckBox>("CartesianOption").IsChecked = _aseCartesian;
    }

    private void SaveEngineSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(EngineSettingsFile)!);
            File.WriteAllText(EngineSettingsFile, JsonSerializer.Serialize(new EngineOptions
            {
                PythonExecutable = PythonExecutablePath,
                AseCartesian = _aseCartesian,
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void OnAsePathLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        SaveEngineSettings();
    }

    private void OnCheckDosEngine(object? sender, RoutedEventArgs e) => DosEngineCheckRequested?.Invoke();
    private void OnCheckCohpEngine(object? sender, RoutedEventArgs e) => CohpEngineCheckRequested?.Invoke();
    private void OnDosBrowse(object? sender, RoutedEventArgs e) => DosBrowseRequested?.Invoke();
    private void OnDosAnalyze(object? sender, RoutedEventArgs e) => DosAnalyzeRequested?.Invoke();
    private void OnDosExport(object? sender, RoutedEventArgs e) => DosExportRequested?.Invoke();
    private void OnDosCancel(object? sender, RoutedEventArgs e) => DosCancelRequested?.Invoke();
    private void OnCohpBrowse(object? sender, RoutedEventArgs e) => CohpBrowseRequested?.Invoke();
    private void OnCohpAnalyze(object? sender, RoutedEventArgs e) => CohpAnalyzeRequested?.Invoke();
    private void OnCohpExport(object? sender, RoutedEventArgs e) => CohpExportRequested?.Invoke();
    private void OnCohpCancel(object? sender, RoutedEventArgs e) => CohpCancelRequested?.Invoke();

    private void OnCheckAse(object? sender, RoutedEventArgs e)
    {
        SaveEngineSettings();
        AseCheckRequested?.Invoke();
    }

    private sealed class ThemeOptions
    {
        public string Accent { get; set; } = "#7F77DD";
        public string Mode { get; set; } = "System";
        public double CornerRadius { get; set; } = 16;
        public double Opacity { get; set; } = 96;
        public bool ReduceMotion { get; set; }
        public int WindowWidth { get; set; } = 1080;
        public int WindowHeight { get; set; } = 760;
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return;
            var saved = JsonSerializer.Deserialize<ThemeOptions>(File.ReadAllText(SettingsFile));
            if (saved is null) return;
            if (AvColor.TryParse(saved.Accent, out var accent)) _accent = accent;
            _savedMode = saved.Mode;
            _radius = Math.Clamp(saved.CornerRadius, 0, 28);
            _opacity = Math.Clamp(saved.Opacity, 30, 100);
            _reduced = saved.ReduceMotion;
            _windowWidth = Math.Clamp(saved.WindowWidth, 820, 3840);
            _windowHeight = Math.Clamp(saved.WindowHeight, 560, 2160);
            Part<TextBox>("WindowWidthInput").Text = _windowWidth.ToString();
            Part<TextBox>("WindowHeightInput").Text = _windowHeight.ToString();
            Part<ComboBox>("WindowSizePreset").SelectedIndex = (_windowWidth, _windowHeight) switch
            {
                (1080, 760) => 0,
                (1440, 900) => 1,
                _ => 2,
            };
            Part<Slider>("CornerSlider").Value = _radius;
            Part<Slider>("OpacitySlider").Value = _opacity;
            Part<CheckBox>("ReduceMotionOption").IsChecked = _reduced;
        }
        catch (Exception) { /* A corrupt optional theme file falls back to DFTDesk Soft. */ }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(new ThemeOptions
            {
                Accent = $"#{_accent.R:X2}{_accent.G:X2}{_accent.B:X2}",
                Mode = _savedMode,
                CornerRadius = _radius,
                Opacity = _opacity,
                ReduceMotion = _reduced,
                WindowWidth = _windowWidth,
                WindowHeight = _windowHeight,
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void OnNavigationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_pages.Length > 0)
            SetPage(Part<ListBox>("Navigation").SelectedIndex);
    }
    private void OnToggleNavigation(object? sender, RoutedEventArgs e)
    {
        _collapsed = !_collapsed;
        Part<Border>("NavPane").Width = _collapsed ? 64 : 230;
        Part<Border>("NavPane").Classes.Set("collapsed", _collapsed);
        Part<DockPanel>("NavDock").Margin = new Thickness(_collapsed ? 10 : 14, 18);
        Part<StackPanel>("BrandText").IsVisible = !_collapsed;
        Part<Grid>("NavFooterButtons").IsVisible = !_collapsed;
        Part<TextBlock>("VersionText").IsVisible = !_collapsed;
        Part<StackPanel>("CollapsedFooter").IsVisible = _collapsed;
        foreach (var item in Part<ListBox>("Navigation").Items.OfType<ListBoxItem>())
            if (item.Content is Border border && border.Child is StackPanel panel)
                foreach (var label in panel.Children.OfType<TextBlock>())
                    label.IsVisible = !_collapsed;
    }
    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { }
    }
    private void OnGitHub(object? sender, RoutedEventArgs e) => OpenUrl("https://github.com/kuanfener/DFTDesk");
    private void OnOpenAseWebsite(object? sender, RoutedEventArgs e) => OpenUrl("https://ase-lib.org/");
    private void OnSponsor(object? sender, RoutedEventArgs e)
    {
        var dialog = new DonationDialog();
        if (TopLevel.GetTopLevel(this) is Window owner)
            _ = dialog.ShowDialog(owner);
        else
            dialog.Show();
    }
    private void OnGoConversion(object? sender, RoutedEventArgs e) =>
        Part<ListBox>("Navigation").SelectedIndex = 1;
    private void OnGoCluster(object? sender, RoutedEventArgs e) =>
        Part<ListBox>("Navigation").SelectedIndex = 2;
    private void OnGoDos(object? sender, RoutedEventArgs e) =>
        Part<ListBox>("Navigation").SelectedIndex = 3;
    private void OnGoEngine(object? sender, RoutedEventArgs e) =>
        Part<ListBox>("Navigation").SelectedIndex = 6;

    private void OnClusterModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        Part<StackPanel>("FacetOptionsPanel").IsVisible =
            Part<ComboBox>("ClusterMode").SelectedIndex == 1;
    }

    private void OnClusterBrowse(object? sender, RoutedEventArgs e) => ClusterBrowseRequested?.Invoke();
    private void OnGenerateCluster(object? sender, RoutedEventArgs e) => ClusterGenerateRequested?.Invoke();
    private void OnCancelCluster(object? sender, RoutedEventArgs e) => ClusterCancelRequested?.Invoke();

    private void OnBrowseFiles(object? sender, RoutedEventArgs e) => BrowseFilesRequested?.Invoke();
    private void OnBrowseFolder(object? sender, RoutedEventArgs e) => BrowseFolderRequested?.Invoke();
    private void OnClearFiles(object? sender, RoutedEventArgs e) => ClearFilesRequested?.Invoke();
    private void OnRemoveSelected(object? sender, RoutedEventArgs e) =>
        RemoveFilesRequested?.Invoke(Part<ListBox>("FileList").SelectedItems?
            .OfType<string>().ToArray() ?? []);
    private void OnClearHistory(object? sender, RoutedEventArgs e)
    {
        Part<StackPanel>("HistoryRecords").Children.Clear();
        Part<TextBlock>("HistoryEmpty").IsVisible = true;
    }
    private void OnExportCif(object? sender, RoutedEventArgs e) => FormatChosen?.Invoke(ExportFormat.Cif);
    private void OnExportPoscar(object? sender, RoutedEventArgs e) => FormatChosen?.Invoke(ExportFormat.Poscar);
    private void OnCancelConversion(object? sender, RoutedEventArgs e) => CancelRequested?.Invoke();
    private void SetAppearance(string mode)
    {
        if (_initializing) return;
        _savedMode = mode;
        ApplyTheme();
    }
    private void OnAppearanceSystem(object? sender, RoutedEventArgs e) => SetAppearance("System");
    private void OnAppearanceLight(object? sender, RoutedEventArgs e) => SetAppearance("Light");
    private void OnAppearanceDark(object? sender, RoutedEventArgs e) => SetAppearance("Dark");

    private void OnAccentHexCommit(object? sender, RoutedEventArgs e)
    {
        if (AvColor.TryParse(Part<TextBox>("AccentHex").Text, out var color))
        {
            _accent = color;
            ApplyTheme();
        }
    }
    private void OnCornerChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_initializing) return;
        _radius = e.NewValue;
        ApplyTheme();
    }
    private void OnOpacityChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_initializing) return;
        _opacity = e.NewValue;
        ApplyTheme();
    }
    private void OnMotionChanged(object? sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _reduced = Part<CheckBox>("ReduceMotionOption").IsChecked == true;
        ApplyTheme();
    }
    private void OnWindowPresetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        switch (Part<ComboBox>("WindowSizePreset").SelectedIndex)
        {
            case 0:
                Part<TextBox>("WindowWidthInput").Text = "1080";
                Part<TextBox>("WindowHeightInput").Text = "760";
                break;
            case 1:
                Part<TextBox>("WindowWidthInput").Text = "1440";
                Part<TextBox>("WindowHeightInput").Text = "900";
                break;
        }
    }

    private void OnApplyWindowSize(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(Part<TextBox>("WindowWidthInput").Text, out var width) ||
            !int.TryParse(Part<TextBox>("WindowHeightInput").Text, out var height) ||
            width is < 820 or > 3840 || height is < 560 or > 2160)
        {
            Part<TextBlock>("WindowSizeStatus").Text = "请输入宽 820–3840、高 560–2160 的整数。";
            return;
        }
        _windowWidth = width;
        _windowHeight = height;
        Part<TextBlock>("WindowSizeStatus").Text = $"窗口已调整为 {width} × {height}。";
        WindowSizeRequested?.Invoke(width, height);
        SaveSettings();
    }
    private void OnThemeSave(object? sender, RoutedEventArgs e) => SaveSettings();
    private void OnThemeCancel(object? sender, RoutedEventArgs e)
    {
        LoadSettings();
        ApplyTheme();
        WindowSizeRequested?.Invoke(_windowWidth, _windowHeight);
    }

}
