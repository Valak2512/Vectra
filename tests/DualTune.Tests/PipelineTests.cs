using DualTune.Core;

namespace DualTune.Tests;

public class PipelineTests
{
    [Fact]
    public void Pipeline_DefaultProfile_CrossPressed_GivesA()
    {
        var raw = ControllerState.Neutral with { Buttons = Buttons.Cross };
        Assert.Equal(XboxButtons.A, Pipeline.Process(raw, Profile.CreateDefault()).Buttons);
    }

    [Fact]
    public void Pipeline_PaddleMappedToRT_ForcesRT255()
    {
        var p = Profile.CreateDefault();
        p = p with { ButtonMap = new Dictionary<Buttons, XboxTarget>(p.ButtonMap) { [Buttons.RightPaddle] = XboxTarget.RT } };
        var raw = ControllerState.Neutral with { Buttons = Buttons.RightPaddle, R2 = 0f };
        Assert.Equal(255, Pipeline.Process(raw, p).RT);
    }

    [Fact]
    public void Pipeline_UsesSeparateStickSettings()
    {
        var p = Profile.CreateDefault() with
        {
            LeftStick = new StickSettings { InnerShape = InnerShape.Square, InnerDeadzone = 0.2f },
            RightStick = new StickSettings { InnerShape = InnerShape.Round, InnerDeadzone = 0f },
        };
        var raw = ControllerState.Neutral with { Left = new(0.1f, 0.1f), Right = new(0.1f, 0.1f) };
        var o = Pipeline.Process(raw, p);
        Assert.Equal(0, o.LX);
        Assert.Equal(0, o.LY);
        Assert.NotEqual(0, o.RX);
        Assert.NotEqual(0, o.RY);
    }
}
