using DualTune.Core;

namespace DualTune.Tests;

public class ButtonMapperTests
{
    [Fact]
    public void Default_CrossIsA_PaddlesAreNone()
    {
        Assert.Equal(XboxTarget.A, ButtonMapper.Default[Buttons.Cross]);
        Assert.Equal(XboxTarget.None, ButtonMapper.Default[Buttons.LeftPaddle]);
        Assert.Equal(XboxTarget.None, ButtonMapper.Default[Buttons.RightPaddle]);
    }

    [Fact]
    public void Default_TouchpadIsBack() => Assert.Equal(XboxTarget.Back, ButtonMapper.Default[Buttons.Touchpad]);

    [Fact]
    public void Map_CombinesMultipleButtons()
    {
        var (buttons, lt, rt) = ButtonMapper.Map(Buttons.Cross | Buttons.Circle, ButtonMapper.Default);
        Assert.Equal(XboxButtons.A | XboxButtons.B, buttons);
        Assert.False(lt);
        Assert.False(rt);
    }

    [Fact]
    public void Map_TwoInputsSameTarget()
    {
        var map = new Dictionary<Buttons, XboxTarget>(ButtonMapper.Default) { [Buttons.LeftPaddle] = XboxTarget.A };
        Assert.Equal(XboxButtons.A, ButtonMapper.Map(Buttons.Cross | Buttons.LeftPaddle, map).buttons);
    }

    [Fact]
    public void Map_ToLT_SetsForceFlag()
    {
        var map = new Dictionary<Buttons, XboxTarget> { [Buttons.RightPaddle] = XboxTarget.LT };
        var (buttons, lt, rt) = ButtonMapper.Map(Buttons.RightPaddle, map);
        Assert.Equal(XboxButtons.None, buttons);
        Assert.True(lt);
        Assert.False(rt);
    }

    [Fact]
    public void Map_MissingEntry_IsNone()
    {
        var map = new Dictionary<Buttons, XboxTarget>();
        Assert.Equal(XboxButtons.None, ButtonMapper.Map(Buttons.Cross, map).buttons);
    }
}
