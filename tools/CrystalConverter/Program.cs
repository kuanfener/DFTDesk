using Avalonia;
using Avalonia.Themes.Fluent;
using Avalonia.Win32.Interoperability;
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
    public override void Initialize() => Styles.Add(new FluentTheme());
}
