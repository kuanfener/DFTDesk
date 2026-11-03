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
using System.Text.Json;
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
    private DonutMenu _wheel = null!;
    private LayoutTransformControl? _zoomHost;
    private AvColor _accent = AvColor.Parse("#7F77DD");
    private double _radius = 16;
    private double _opacity = 96;
    private double _zoom = 1;
    private bool _material;
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
    public event Action? ClusterBrowseRequested;
    public event Action? ClusterGenerateRequested;
    public event Action? ClusterCancelRequested;

    public CrystalShell()
    {
        AvaloniaXamlLoader.Load(this);
        _pages =
        [
            Part<Control>("HomePage"), Part<Control>("ConversionPage"),
            Part<Control>("ClusterPage"), Part<Control>("ThemePage"),
            Part<Control>("WorkEnginePage"), Part<Control>("AboutPage")
        ];
        var pageContainer = Part<Grid>("PageContainer");
        foreach (var page in _pages)
            pageContainer.Children.Remove(page);
        Part<ContentControl>("PageHost").Content = null;

        _wheel = DftDonutFactory.Create(96, DrawingColor.FromArgb(127, 119, 221));
        _wheel.SegmentInvoked += (_, index) =>
        {
            if (index is 0 or 1)
                FormatChosen?.Invoke(index == 0 ? ExportFormat.Cif : ExportFormat.Poscar);
        };
        DftDonutFactory.AttachDrop(_wheel, (format, files) =>
        {
            FilesDropped?.Invoke(files);
            FormatChosen?.Invoke(format);
        });
        Part<ContentControl>("WheelPreviewHost").Content = _wheel;

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
        zone.PointerPressed += (_, e) =>
        {
            if (e.ClickCount >= 2)
                BrowseFilesRequested?.Invoke();
        };

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
        Part<ColorPicker>("AccentPicker").PropertyChanged += (_, e) =>
        {
            if (_initializing || e.Property != ColorPicker.ColorProperty) return;
            var color = Part<ColorPicker>("AccentPicker").Color;
            if (color == _accent) return;
            _accent = color;
            ApplyTheme();
        };

        var body = (Control)Content!;
        Content = null;
        _zoomHost = new LayoutTransformControl { Child = body };
        Content = _zoomHost;

        BuildSwatches();
        LoadSettings();
        LoadEngineSettings();
        Part<ComboBox>("AppearanceMode").SelectedIndex =
            (_savedMode is "Light") ? 1 : (_savedMode is "Dark") ? 2 : 0;
        _initializing = false;
        ApplyTheme();
        SetPage(0);
        AttachedToVisualTree += (_, _) =>
        {
            var window = TopLevel.GetTopLevel(this);
            window?.Classes.Set("material-you", _material);
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
    public string PythonExecutablePath => Part<TextBox>("AsePythonExecutable").Text?.Trim() is { Length: > 0 } executable
        ? executable : "python";
    public void SetAseStatus(string text)
    {
        Part<TextBlock>("AseHealthText").Text = text;
        Part<TextBlock>("HomeAseStatus").Text = text;
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
        Part<ListBox>("FileList").ItemsSource = files.ToArray();
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
            button.Classes.Add("ghost");
            button.Click += (_, _) => { _accent = color; ApplyTheme(); };
            panel.Children.Add(button);
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
        if (_material)
        {
            app.Resources.ThemeDictionaries[ThemeVariant.Light] =
                MaterialYouPalette.Generate(_accent, isDark: false).ToResourceDictionary();
            app.Resources.ThemeDictionaries[ThemeVariant.Dark] =
                MaterialYouPalette.Generate(_accent, isDark: true).ToResourceDictionary();
            SoftPalette.ClearTopLevelAccent(app.Resources);
        }
        else
        {
            app.Resources.ThemeDictionaries.Remove(ThemeVariant.Light);
            app.Resources.ThemeDictionaries.Remove(ThemeVariant.Dark);
            SoftPalette.ApplyAccent(app.Resources, _accent);
        }
        Motion.Configure(_reduced, () => false);
        TopLevel.GetTopLevel(this)?.Classes.Set("material-you", _material);
        TopLevel.GetTopLevel(this)?.Classes.Set("reduced-motion", _reduced);
        DftDonutFactory.ApplyAccent(_wheel, ToDrawing(_accent));
        _wheel.SliceOpacity = _opacity / 100;
        Part<TextBlock>("CornerValue").Text = _radius.ToString("0");
        Part<TextBlock>("OpacityValue").Text = _opacity.ToString("0") + "%";
        Part<TextBox>("AccentHex").Text = $"#{_accent.R:X2}{_accent.G:X2}{_accent.B:X2}";
        if (Part<ColorPicker>("AccentPicker").Color != _accent)
            Part<ColorPicker>("AccentPicker").Color = _accent;
        Part<Button>("SoftStyleButton").Classes.Set("selected", !_material);
        Part<Button>("MaterialStyleButton").Classes.Set("selected", _material);
        _zoomHost?.LayoutTransform = Math.Abs(_zoom - 1) < .001 ? null : new ScaleTransform(_zoom, _zoom);
        Part<TextBlock>("ZoomValue").Text = (_zoom * 100).ToString("0") + "%";
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

    private void OnCheckAse(object? sender, RoutedEventArgs e)
    {
        SaveEngineSettings();
        AseCheckRequested?.Invoke();
    }

    private sealed class ThemeOptions
    {
        public string Accent { get; set; } = "#7F77DD";
        public string Mode { get; set; } = "System";
        public bool MaterialYou { get; set; }
        public double CornerRadius { get; set; } = 16;
        public double Opacity { get; set; } = 96;
        public bool ReduceMotion { get; set; }
        public double Zoom { get; set; } = 1;
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
            _material = saved.MaterialYou;
            _radius = Math.Clamp(saved.CornerRadius, 0, 28);
            _opacity = Math.Clamp(saved.Opacity, 30, 100);
            _reduced = saved.ReduceMotion;
            _zoom = Math.Clamp(saved.Zoom, .7, 1.8);
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
                MaterialYou = _material,
                CornerRadius = _radius,
                Opacity = _opacity,
                ReduceMotion = _reduced,
                Zoom = _zoom,
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
    private void OnGoEngine(object? sender, RoutedEventArgs e) =>
        Part<ListBox>("Navigation").SelectedIndex = 4;

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
    private void OnStyleSoft(object? sender, RoutedEventArgs e) { _material = false; ApplyTheme(); }
    private void OnStyleMaterial(object? sender, RoutedEventArgs e) { _material = true; ApplyTheme(); }
    private void OnAppearanceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        _savedMode = Part<ComboBox>("AppearanceMode").SelectedIndex switch
        {
            1 => "Light", 2 => "Dark", _ => "System"
        };
        ApplyTheme();
    }
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
    private void OnZoomOut(object? sender, RoutedEventArgs e) { _zoom = Math.Max(.7, _zoom - .1); ApplyTheme(); }
    private void OnZoomIn(object? sender, RoutedEventArgs e) { _zoom = Math.Min(1.8, _zoom + .1); ApplyTheme(); }
    private void OnZoomReset(object? sender, RoutedEventArgs e) { _zoom = 1; ApplyTheme(); }
    private void OnThemeSave(object? sender, RoutedEventArgs e) => SaveSettings();
    private void OnThemeCancel(object? sender, RoutedEventArgs e) { LoadSettings(); ApplyTheme(); }
    private void Choose(string color) { _accent = AvColor.Parse(color); ApplyTheme(); }
    private void OnPresetMono(object? sender, RoutedEventArgs e) => Choose("#1F1E1D");
    private void OnPresetLavender(object? sender, RoutedEventArgs e) => Choose("#7F77DD");
    private void OnPresetPeach(object? sender, RoutedEventArgs e) => Choose("#E07A70");
    private void OnPresetMint(object? sender, RoutedEventArgs e) => Choose("#529B7D");
    private void OnPresetRose(object? sender, RoutedEventArgs e) => Choose("#E86483");
    private void OnPresetSky(object? sender, RoutedEventArgs e) => Choose("#5B8BE3");
    private void OnPresetAmber(object? sender, RoutedEventArgs e) => Choose("#CB852A");
    private void OnPresetSage(object? sender, RoutedEventArgs e) => Choose("#5A8E51");
}
