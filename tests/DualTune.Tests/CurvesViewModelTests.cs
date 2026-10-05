using DualTune.App.Engine;
using DualTune.App.ViewModels;
using DualTune.Core;

namespace DualTune.Tests;

public class CurvesViewModelTests
{
    private Profile _profile = Profile.CreateDefault();

    private CurvesViewModel Make() => new(() => _profile, f => _profile = f(_profile));

    private static readonly ResponseCurve Custom = new(new[] { new CurvePoint(0.5f, 0.2f) });

    [Fact]
    public void SetCurve_LeftStick_WritesLeftOnly()
    {
        var vm = Make();
        vm.Target = CurveTarget.LeftStick;
        vm.Curve = Custom;
        Assert.Equal(Custom, _profile.LeftStick.Curve);
        Assert.Equal(ResponseCurve.Linear, _profile.RightStick.Curve);
    }

    [Fact]
    public void SetCurve_R2_WritesTrigger()
    {
        var vm = Make();
        vm.Target = CurveTarget.R2;
        vm.Curve = Custom;
        Assert.Equal(Custom, _profile.R2.Curve);
        Assert.Equal(ResponseCurve.Linear, _profile.L2.Curve);
    }

    [Fact]
    public void SwitchTarget_ShowsThatTargetsCurve()
    {
        _profile = _profile with { RightStick = _profile.RightStick with { Curve = Custom } };
        var vm = Make();
        Assert.Equal(ResponseCurve.Linear, vm.Curve);
        vm.Target = CurveTarget.RightStick;
        Assert.Equal(Custom, vm.Curve);
    }

    [Fact]
    public void TargetRadioProps_SetTarget()
    {
        var vm = Make();
        Assert.True(vm.IsLeft);
        vm.IsR2 = true;
        Assert.Equal(CurveTarget.R2, vm.Target);
        Assert.False(vm.IsLeft);
        vm.IsRight = true;
        Assert.Equal(CurveTarget.RightStick, vm.Target);
        vm.IsL2 = true;
        Assert.Equal(CurveTarget.L2, vm.Target);
    }

    [Fact]
    public void ApplyPreset_SetsCurve()
    {
        var vm = Make();
        vm.Target = CurveTarget.L2;
        vm.ApplyPresetCommand.Execute("Exponentielle forte");
        Assert.Equal(ResponseCurve.StrongExponential, _profile.L2.Curve);
    }

    [Fact]
    public void CurrentInput_FollowsTarget()
    {
        var vm = Make();
        var snap = EngineSnapshot.Empty with
        {
            Raw = new ControllerState(new(0.3f, 0.4f), new(1f, 1f), 0.25f, 0.75f, Buttons.None),
        };
        vm.OnTick(snap);
        Assert.Equal(0.5f, vm.CurrentInput, 3);
        vm.Target = CurveTarget.RightStick;
        vm.OnTick(snap);
        Assert.Equal(1f, vm.CurrentInput, 3); // magnitude bornée à 1
        vm.Target = CurveTarget.R2;
        vm.OnTick(snap);
        Assert.Equal(0.75f, vm.CurrentInput, 3);
    }
}
