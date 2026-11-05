namespace DFTDesk;

/// <summary>Pure gesture rules shared by the Windows hook and its unit tests.</summary>
internal static class WheelGestureRules
{
    private const int ClickTolerancePixels = 6;

    public static bool IsActivation(int button, bool ctrl, bool alt, bool shift, bool meta) =>
        button == 3 && ctrl && !alt && !shift && !meta;

    public static bool IsStationaryClick(int downX, int downY, int upX, int upY) =>
        Math.Abs(upX - downX) <= ClickTolerancePixels &&
        Math.Abs(upY - downY) <= ClickTolerancePixels;
}
