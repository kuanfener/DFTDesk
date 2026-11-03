using Filee.App.Services.Triggers;
using Filee.Core.Settings;
using Filee.Platform.Windows;
using SharpHook;
using SharpHook.Data;

namespace DFTDesk;

/// <summary>Detects only Explorer Ctrl + left mouse drag for DFTDesk's conversion wheel.</summary>
internal sealed class GlobalTriggerHost : IDisposable
{
    private readonly GestureDetector _detector = new();
    private readonly WindowsPlatformServices _platform = new();
    private QueuedGlobalHook? _hook;
    private Thread? _thread;

    public event Action<int, int>? DragStarted;
    public event Action<int, int>? DragEnded;
    public event Action<string>? Failure;

    public IReadOnlyList<string> SelectedFiles() => _platform.GetFileManagerSelection();

    public GlobalTriggerHost()
    {
        _detector.SystemDragThreshold = _platform.SystemDragThreshold;
        _detector.Configure(TriggerGesture.Defaults().Where(g =>
            g.Kind == TriggerKind.Drag &&
            g.Modifiers == ModifierKeys.Ctrl &&
            g.Button == TriggerMouseButton.Left));
        _detector.ScopeFilter = (_, x, y) => _platform.IsFileManagerAt(x, y);
        _detector.DragStarted += (_, x, y) => DragStarted?.Invoke(x, y);
        _detector.DragEnded += (x, y) => DragEnded?.Invoke(x, y);
    }

    public void Start()
    {
        if (_hook is not null) return;
        try
        {
            _hook = new QueuedGlobalHook();
            _hook.MousePressed += (_, e) =>
                _detector.MouseDown(Button(e.Data.Button), e.Data.X, e.Data.Y, Modifiers(e.RawEvent.Mask));
            _hook.MouseReleased += (_, e) =>
                _detector.MouseUp(Button(e.Data.Button), e.Data.X, e.Data.Y);
            _hook.MouseDragged += (_, e) => Move(e.Data.X, e.Data.Y, e.RawEvent.Mask);
            _hook.MouseMoved += (_, e) => Move(e.Data.X, e.Data.Y, e.RawEvent.Mask);
            var hook = _hook;
            _thread = new Thread(() =>
            {
                try { hook.Run(); }
                catch (Exception ex) { Failure?.Invoke(ex.GetBaseException().Message); }
            }) { IsBackground = true, Name = "Crystal converter input hook", Priority = ThreadPriority.Highest };
            _thread.Start();
        }
        catch (Exception ex)
        {
            Failure?.Invoke(ex.Message);
        }
    }

    private void Move(int x, int y, EventMask mask) => _detector.MouseMove(x, y, Modifiers(mask),
        mask.HasFlag(EventMask.Button1) ? TriggerMouseButton.Left : TriggerMouseButton.None);

    private static TriggerMouseButton Button(MouseButton button) => button switch
    {
        MouseButton.Button1 => TriggerMouseButton.Left,
        MouseButton.Button2 => TriggerMouseButton.Right,
        _ => TriggerMouseButton.None,
    };

    private static ModifierKeys Modifiers(EventMask mask)
    {
        var result = ModifierKeys.None;
        if (mask.HasCtrl()) result |= ModifierKeys.Ctrl;
        if (mask.HasAlt()) result |= ModifierKeys.Alt;
        if (mask.HasShift()) result |= ModifierKeys.Shift;
        if (mask.HasMeta()) result |= ModifierKeys.Meta;
        return result;
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
