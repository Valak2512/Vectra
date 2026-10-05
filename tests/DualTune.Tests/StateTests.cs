using DualTune.Core;

namespace DualTune.Tests;

public class StateTests
{
    [Fact]
    public void Neutral_IsAllZero()
    {
        Assert.Equal(new StickState(0, 0), ControllerState.Neutral.Left);
        Assert.Equal(Buttons.None, ControllerState.Neutral.Buttons);
        Assert.Equal(default(XboxState), XboxState.Neutral);
    }

    [Fact]
    public void XboxButtons_MatchViGEmValues()
    {
        Assert.Equal(0x1000, (int)XboxButtons.A);
        Assert.Equal(0x0400, (int)XboxButtons.Guide);
    }
}
