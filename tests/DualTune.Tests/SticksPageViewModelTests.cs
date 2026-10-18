using System.ComponentModel;
using DualTune.App.ViewModels;
using DualTune.Core;

namespace DualTune.Tests;

public class SticksPageViewModelTests
{
    private Profile _profile = Profile.CreateDefault();

    private (SticksPageViewModel page, StickViewModel left, StickViewModel right) Make()
    {
        var left = new StickViewModel(() => _profile, f => _profile = f(_profile), isLeft: true);
        var right = new StickViewModel(() => _profile, f => _profile = f(_profile), isLeft: false);
        return (new SticksPageViewModel(left, right), left, right);
    }

    [Fact]
    public void SticksPage_DefaultsToLeft()
    {
        var (page, left, _) = Make();
        Assert.Equal(StickSide.Left, page.SelectedSide);
        Assert.Same(left, page.Current);
    }

    [Fact]
    public void SticksPage_SwitchSide_EditsOnlySelectedStick()
    {
        var (page, _, right) = Make();
        page.IsRight = true;
        Assert.Same(right, page.Current);
        page.Current.InnerDeadzone = 0.2f;
        Assert.Equal(0.2f, _profile.RightStick.InnerDeadzone);
        Assert.Equal(new StickSettings().InnerDeadzone, _profile.LeftStick.InnerDeadzone);
    }

    [Fact]
    public void SticksPage_SwitchSide_RaisesCurrent()
    {
        var (page, _, _) = Make();
        var raised = new List<string?>();
        page.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        page.IsRight = true;
        Assert.Contains(nameof(SticksPageViewModel.Current), raised);
    }

    [Fact]
    public void DeadzoneSummary_Format()
    {
        var (_, left, _) = Make();
        left.InnerShape = InnerShape.Square;
        left.InnerDeadzone = 0.12f;
        left.OuterShape = OuterShape.Square;
        Assert.Equal("CARRÉE · 12% · CARRÉ", left.DeadzoneSummary);
    }

    [Fact]
    public void ResponseSummary_Format()
    {
        var (_, left, _) = Make();
        left.HorizontalSnapAngle = 12f;
        left.VerticalSnapAngle = 4.5f;
        Assert.Equal("100% · H 12° · V 4.5°", left.ResponseSummary);
    }

    [Fact]
    public void CalibrationSummary_Format()
    {
        var (_, left, _) = Make();
        left.CenterOffsetX = 0.012f;
        left.CenterOffsetY = -0.0001f;
        Assert.Equal("X +0.012 · Y +0.000", left.CalibrationSummary);
    }

    [Fact]
    public void ShapeRadioProps_SetShape()
    {
        var (_, left, _) = Make();
        left.IsInnerCross = true;
        Assert.Equal(InnerShape.Cross, _profile.LeftStick.InnerShape);
        Assert.True(left.IsInnerCross);
        Assert.False(left.IsInnerRound);
        left.IsOuterSquare = true;
        Assert.Equal(OuterShape.Square, _profile.LeftStick.OuterShape);
    }

    [Fact]
    public void Summaries_FollowProfileChange()
    {
        var (_, left, _) = Make();
        _profile = _profile with { LeftStick = new StickSettings { InnerShape = InnerShape.Hybrid, InnerDeadzone = 0.3f } };
        var raised = new List<string?>();
        left.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        left.Refresh();
        Assert.Contains(raised, n => string.IsNullOrEmpty(n) || n == nameof(StickViewModel.DeadzoneSummary));
        Assert.StartsWith("HYBRIDE · 30%", left.DeadzoneSummary);
    }
}
