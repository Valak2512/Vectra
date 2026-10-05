using DualTune.App.Controls;

namespace DualTune.Tests;

public class SegmentSliderMathTests
{
    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(100, 100, 0.5)]
    [InlineData(-5, 100, 0)]
    [InlineData(105, 100, 0.5)]
    [InlineData(10, 0, 0)]
    public void ValueFromPosition_EdgesAndZeroWidth(double x, double width, double expected)
    {
        double v = SegmentSlider.ValueFromPosition(x, width, 0, 0.5);
        Assert.False(double.IsNaN(v));
        Assert.Equal(expected, v, 6);
    }

    [Fact]
    public void ValueFromPosition_Midpoint() =>
        Assert.Equal(0.25, SegmentSlider.ValueFromPosition(50, 100, 0, 0.5), 6);

    [Theory]
    [InlineData(false, false, false)] // survol pendant un défilement de page : la molette doit faire défiler
    [InlineData(false, true, true)]   // jauge sélectionnée (cliquée / tabulée) : la molette règle
    [InlineData(true, true, false)]   // lecture seule : jamais
    public void HandlesWheel_OnlyWhenFocusedAndEditable(bool readOnly, bool focused, bool expected) =>
        Assert.Equal(expected, SegmentSlider.HandlesWheel(readOnly, focused));

    [Theory]
    [InlineData(0, 30, 3)]        // accrochage : 3° par PageUp (plus grand que le pas des flèches 0,5°)
    [InlineData(0.5, 2, 0.15)]    // sensibilité
    [InlineData(-0.2, 0.2, 0.04)] // centre
    public void PageStep_IsTenthOfRange(double min, double max, double expected) =>
        Assert.Equal(expected, SegmentSlider.PageStep(min, max), 6);

    [Fact]
    public void LitSegments_Proportional() =>
        Assert.Equal(5.0, SegmentSlider.LitSegments(0.125, 0, 0.5, 20), 6);

    [Fact]
    public void LitSegments_Clamps()
    {
        Assert.Equal(20.0, SegmentSlider.LitSegments(9, 0, 0.5, 20), 6);
        Assert.Equal(0.0, SegmentSlider.LitSegments(-1, 0, 0.5, 20), 6);
        Assert.Equal(0.0, SegmentSlider.LitSegments(1, 1, 1, 20), 6); // plage nulle
    }
}
