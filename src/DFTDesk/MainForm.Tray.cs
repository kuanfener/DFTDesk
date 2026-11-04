using System.Drawing;
using System.Windows.Forms;

namespace DFTDesk;

/// <summary>
/// The real desktop window is Avalonia; the existing WinForms bridge owns the
/// notification-area icon so no second message loop or tray framework is needed.
/// </summary>
internal sealed partial class MainForm
{
    private NotifyIcon? _trayIcon;
    private ContextMenuStrip? _trayMenu;
    private Icon? _ownedTrayIcon;

    private bool CanMinimizeToTray => _shell.MinimizeToTrayEnabled && _trayIcon?.Visible == true;

    private void InitializeTray()
    {
        try
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("打开 DFTDesk", null, (_, _) => RestoreFromTray());
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("退出 DFTDesk", null, (_, _) => ExitFromTray());

            var executable = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executable) && File.Exists(executable))
            {
                try { _ownedTrayIcon = Icon.ExtractAssociatedIcon(executable); }
                catch (ArgumentException) { /* Fall back to the system icon. */ }
                catch (System.ComponentModel.Win32Exception) { /* Fall back to the system icon. */ }
            }

            _trayIcon = new NotifyIcon
            {
                Text = "DFTDesk",
                Icon = _ownedTrayIcon ?? SystemIcons.Application,
                ContextMenuStrip = _trayMenu,
                Visible = true,
            };
            _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        }
        catch (Exception ex)
        {
            DisposeTray();
            Log("系统托盘初始化失败，窗口将保留在任务栏：" + ex.Message);
        }
    }

    private void HideToTray()
    {
        if (_avaloniaWindow is null || !CanMinimizeToTray) return;
        _avaloniaWindow.ShowInTaskbar = false;
        _avaloniaWindow.Hide();
    }

    private void RestoreFromTray()
    {
        if (_avaloniaWindow is null) return;
        _avaloniaWindow.ShowInTaskbar = true;
        _avaloniaWindow.Show();
        _avaloniaWindow.WindowState = Avalonia.Controls.WindowState.Normal;
        _avaloniaWindow.Activate();
    }

    private void ExitFromTray()
    {
        // An in-progress export can veto Close while it cancels; restore the
        // window first so progress and any warning remain visible to the user.
        RestoreFromTray();
        _avaloniaWindow.Close();
    }

    private void DisposeTray()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        _trayMenu?.Dispose();
        _trayMenu = null;
        _ownedTrayIcon?.Dispose();
        _ownedTrayIcon = null;
    }
}
