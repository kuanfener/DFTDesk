using DFTDesk;
using SharpHook.Data;

namespace DFTDesk.Tests;

public class GlobalTriggerHostTests
{
    [Fact]
    public void Only_ctrl_middle_click_activates_wheel()
    {
        Assert.True(GlobalTriggerHost.IsWheelGesture(MouseButton.Button3, EventMask.LeftCtrl));
        Assert.True(GlobalTriggerHost.IsWheelGesture(MouseButton.Button3, EventMask.RightCtrl));
        Assert.False(GlobalTriggerHost.IsWheelGesture(MouseButton.Button1, EventMask.LeftCtrl));
        Assert.False(GlobalTriggerHost.IsWheelGesture(MouseButton.Button3, EventMask.None));
        Assert.False(GlobalTriggerHost.IsWheelGesture(MouseButton.Button3, EventMask.LeftCtrl | EventMask.LeftShift));
        Assert.False(GlobalTriggerHost.IsWheelGesture(MouseButton.Button3, EventMask.LeftCtrl | EventMask.LeftAlt));
    }

    [Fact]
    public void Click_must_not_be_a_drag()
    {
        Assert.True(GlobalTriggerHost.IsStationaryClick(100, 200, 100, 200));
        Assert.True(GlobalTriggerHost.IsStationaryClick(100, 200, 106, 194));
        Assert.False(GlobalTriggerHost.IsStationaryClick(100, 200, 107, 200));
        Assert.False(GlobalTriggerHost.IsStationaryClick(100, 200, 100, 207));
    }
}
