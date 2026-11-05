using DFTDesk;

namespace DFTDesk.Tests;

public class GlobalTriggerHostTests
{
    [Fact]
    public void Only_ctrl_middle_click_activates_wheel()
    {
        Assert.True(WheelGestureRules.IsActivation(3, ctrl: true, alt: false, shift: false, meta: false));
        Assert.False(WheelGestureRules.IsActivation(2, ctrl: true, alt: false, shift: false, meta: false));
        Assert.False(WheelGestureRules.IsActivation(1, ctrl: true, alt: false, shift: false, meta: false));
        Assert.False(WheelGestureRules.IsActivation(3, ctrl: false, alt: false, shift: false, meta: false));
        Assert.False(WheelGestureRules.IsActivation(3, ctrl: true, alt: false, shift: true, meta: false));
        Assert.False(WheelGestureRules.IsActivation(3, ctrl: true, alt: true, shift: false, meta: false));
        Assert.False(WheelGestureRules.IsActivation(3, ctrl: true, alt: false, shift: false, meta: true));
    }

    [Fact]
    public void Click_must_not_be_a_drag()
    {
        Assert.True(WheelGestureRules.IsStationaryClick(100, 200, 100, 200));
        Assert.True(WheelGestureRules.IsStationaryClick(100, 200, 106, 194));
        Assert.False(WheelGestureRules.IsStationaryClick(100, 200, 107, 200));
        Assert.False(WheelGestureRules.IsStationaryClick(100, 200, 100, 207));
    }
}
