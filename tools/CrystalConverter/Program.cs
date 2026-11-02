using Avalonia;
using Avalonia.Themes.Fluent;
using Avalonia.Win32.Interoperability;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Filee.App.Services;
using System.Windows.Forms;

namespace Filee.CrystalConverter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Host Filee's original Avalonia donut in this standalone WinForms shell.
        // The message filter forwards keyboard input to the detached popup.
        System.Windows.Forms.Application.AddMessageFilter(new WinFormsAvaloniaMessageFilter());
        AppBuilder.Configure<CrystalAvaloniaApp>()
            .UsePlatformDetect()
            .SetupWithoutStarting();
        System.Windows.Forms.Application.Run(new MainForm());
    }
}

internal sealed class CrystalAvaloniaApp : Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        var root = new Uri("avares://Filee.CrystalConverter/");
        Styles.Add(new StyleInclude(root)
        {
            Source = new Uri("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml"),
        });
        Resources.MergedDictionaries.Add(new ResourceInclude(root)
        {
            Source = new Uri("avares://Filee.CrystalConverter/Styles/Icons.axaml"),
        });
        Resources["AppFlowDirection"] = Avalonia.Media.FlowDirection.LeftToRight;
        Resources["common.on"] = "开";
        Resources["common.off"] = "关";
        Resources["ColorViewTabStripHeight"] = 0d;
        Resources["ColorViewContentBackgroundBrush"] = Brushes.Transparent;
        Resources["ColorViewContentBorderBrush"] = Brushes.Transparent;
        Resources["ColorViewTabBorderBrush"] = Brushes.Transparent;
        Styles.Add(new StyleInclude(root)
        {
            Source = new Uri("avares://Filee.CrystalConverter/Styles/Soft.axaml"),
        });
        Styles.Add(new StyleInclude(root)
        {
            Source = new Uri("avares://Filee.CrystalConverter/DonutStyles.axaml"),
        });
        SoftPalette.ApplyAccent(Resources, Color.Parse("#7F77DD"));
        SoftPalette.ApplyCornerRadius(Resources, 16);
    }
}
