using Filee.Platform.Windows;
using SharpHook;
using SharpHook.Data;

namespace DFTDesk;

/// <summary>
/// Opens DFTDesk's Explorer conversion wheel with Ctrl + middle mouse click.
/// The original Ctrl + left drag is never intercepted, so Explorer can copy files normally.
/// </summary>
internal sealed class GlobalTriggerHost : IDisposable
{
    private const int ClickTolerancePixels = 6;
    private readonly WindowsPlatformServices _platform = new();
    private SimpleGlobalHook? _hook;
    private Thread? _thread;
    private (int X, int Y)? _middlePress;

    public event Action<int, int>? WheelRequested;
    public event Action<string>? Failure;

    public IReadOnlyList<string> SelectedFiles() => _platform.GetFileManagerSelection();

    // Pure predicates are also covered by Windows CI tests without starting a global hook.
    internal static bool IsWheelGesture(MouseButton button, EventMask mask) =>
        button == MouseButton.Button3 &&
        mask.HasCtrl() && !mask.HasAlt() && !mask.HasShift() && !mask.HasMeta();

    internal static bool IsStationaryClick(int downX, int downY, int upX, int upY) =>
        Math.Abs(upX - downX) <= ClickTolerancePixels &&
        Math.Abs(upY - downY) <= ClickTolerancePixels;

    public void Start()
    {
        if (_hook is not null) return;
        try
        {
            // Only synchronous hooks can suppress Ctrl+middle events before Explorer
            // reacts to them. The handlers do only fast checks and queue UI work.
            _hook = new SimpleGlobalHook();
            _hook.MousePressed += (_, e) =>
            {
                if (!IsWheelGesture(e.Data.Button, e.RawEvent.Mask)) return;
                var at = System.Windows.Forms.Cursor.Position;
                if (!_platform.IsFileManagerAt(at.X, at.Y)) return;
                _middlePress = (at.X, at.Y);
                e.SuppressEvent = true;
            };
            _hook.MouseReleased += (_, e) =>
            {
                if (e.Data.Button != MouseButton.Button3 || _middlePress is not { } press)
                    return;
                _middlePress = null;
                e.SuppressEvent = true;
                var at = System.Windows.Forms.Cursor.Position;
                if (IsStationaryClick(press.X, press.Y, at.X, at.Y))
                    WheelRequested?.Invoke(at.X, at.Y);
            };
            var hook = _hook;
            _thread = new Thread(() =>
            {
                try { hook.Run(); }
                catch (Exception ex) { Failure?.Invoke(ex.GetBaseException().Message); }
            }) { IsBackground = true, Name = "DFTDesk Explorer wheel input hook", Priority = ThreadPriority.Highest };
            _thread.Start();
        }
        catch (Exception ex)
        {
            Failure?.Invoke(ex.Message);
        }
    }

    public void Dispose()
    {
        try
        {
            if (_hook is { IsDisposed: false, IsRunning: true }) _hook.Stop();
            if (_thread is { IsAlive: true } thread && !thread.Join(TimeSpan.FromSeconds(5))) return;
            _hook?.Dispose();
        }
        catch (HookException) { /* A failed hook must not prevent the window from closing. */ }
    }
}
