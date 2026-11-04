using Microsoft.Win32;

namespace DFTDesk;

/// <summary>
/// Per-user Windows login startup; never requires administrator rights and is
/// only changed by the explicit Save Settings action on the Theme page.
/// </summary>
internal static class WindowsStartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DFTDesk";

    public static string BuildCommand(string executablePath) => "\"" + executablePath + "\" --tray";

    public static void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("开机自启仅支持 Windows。");

        if (!enabled)
        {
            using var existing = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            existing?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable) ||
            !Path.GetFileName(executable).Equals("DFTDesk.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("请从 DFTDesk.exe 启动软件后再启用开机自启。");

        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("无法打开当前用户的开机启动注册表项。");
        key.SetValue(ValueName, BuildCommand(executable), RegistryValueKind.String);
    }
}
