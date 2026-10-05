using DualTune.App.Engine;
using DualTune.App.ViewModels;
using DualTune.Core;

namespace DualTune.Tests;

public class StickViewModelTests
{
    private Profile _profile = Profile.CreateDefault();

    private StickViewModel Make(bool left) =>
        new(() => _profile, f => _profile = f(_profile), left);

    [Fact]
    public void Setting_InnerShape_UpdatesOnlyThatStick()
    {
        var vm = Make(left: true);
        vm.InnerShape = InnerShape.Square;
        vm.InnerDeadzone = 0.2f;
        Assert.Equal(InnerShape.Square, _profile.LeftStick.InnerShape);
        Assert.Equal(0.2f, _profile.LeftStick.InnerDeadzone);
        Assert.Equal(InnerShape.Round, _profile.RightStick.InnerShape);
    }

    [Fact]
    public void AxisSnapAngle_WritesSetting()
    {
        var vm = Make(left: true);
        vm.AxisSnapAngle = 12f;
        Assert.Equal(12f, _profile.LeftStick.AxisSnapAngle);
    }

    [Fact]
    public void RightStick_WritesRightSettings()
    {
        var vm = Make(left: false);
        vm.OuterShape = OuterShape.Square;
        Assert.Equal(OuterShape.Square, _profile.RightStick.OuterShape);
        Assert.Equal(OuterShape.Circle, _profile.LeftStick.OuterShape);
    }

    [Fact]
    public void ShowAxial_TrueForCrossAndHybrid()
    {
        var vm = Make(left: true);
        Assert.False(vm.ShowAxial);
        vm.InnerShape = InnerShape.Cross;
        Assert.True(vm.ShowAxial);
        vm.InnerShape = InnerShape.Hybrid;
        Assert.True(vm.ShowAxial);
    }

    [Fact]
    public void Calibrate_UsesCurrentRaw_Clamped()
    {
        var vm = Make(left: true);
        vm.OnTick(EngineSnapshot.Empty with { Raw = ControllerState.Neutral with { Left = new(0.05f, -0.5f) } });
        vm.CalibrateCommand.Execute(null);
        Assert.Equal(0.05f, _profile.LeftStick.CenterOffsetX, 4);
        Assert.Equal(-0.2f, _profile.LeftStick.CenterOffsetY, 4);
    }

    [Fact]
    public void OnTick_ExposesRawAndProcessed()
    {
        var vm = Make(left: false);
        vm.OnTick(EngineSnapshot.Empty with
        {
            Raw = ControllerState.Neutral with { Right = new(0.5f, 0.5f) },
            RightProcessed = new(0.4f, 0.4f),
        });
        Assert.Equal(new StickState(0.5f, 0.5f), vm.Raw);
        Assert.Equal(new StickState(0.4f, 0.4f), vm.Processed);
    }
}
