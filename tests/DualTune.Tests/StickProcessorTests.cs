using DualTune.Core;

namespace DualTune.Tests;

public class StickProcessorTests
{
    // Défauts de la spec, sauf zone extérieure à 0 pour simplifier les valeurs attendues.
    private static readonly StickSettings Base = new() { OuterDeadzone = 0f };

    private static StickState P(StickSettings s, float x, float y) => StickProcessor.Process(new StickState(x, y), s);

    private static void Near(float expected, float actual) => Assert.InRange(actual, expected - 1e-3f, expected + 1e-3f);

    private static float Mag(StickState s) => MathF.Sqrt(s.X * s.X + s.Y * s.Y);

    [Fact]
    public void Round_InsideDeadzone_IsZero()
    {
        Assert.Equal(StickState.Zero, P(Base with { InnerDeadzone = 0.1f }, 0.06f, 0.06f));
    }

    [Fact]
    public void Square_InsideSquare_IsZero()
    {
        Assert.Equal(StickState.Zero, P(Base with { InnerShape = InnerShape.Square, InnerDeadzone = 0.1f }, 0.09f, 0.09f));
        Assert.NotEqual(StickState.Zero, P(Base with { InnerShape = InnerShape.Round, InnerDeadzone = 0.1f }, 0.09f, 0.09f));
    }

    [Fact]
    public void Square_JustOutsideOnAxis_IsSmallNonZero()
    {
        var r = P(Base with { InnerShape = InnerShape.Square, InnerDeadzone = 0.1f }, 0.11f, 0f);
        Assert.InRange(r.X, 1e-6f, 0.02f);
        Assert.Equal(0f, r.Y, 5);
    }

    [Theory]
    [InlineData(InnerShape.Round)]
    [InlineData(InnerShape.Square)]
    [InlineData(InnerShape.Hybrid)]
    public void AllShapes_FullDeflection_ReachesOne_InEightDirections(InnerShape shape)
    {
        var s = Base with { InnerShape = shape, InnerDeadzone = 0.2f };
        for (int k = 0; k < 8; k++)
        {
            float a = k * MathF.PI / 4;
            Near(1f, Mag(P(s, MathF.Cos(a), MathF.Sin(a))));
        }
    }

    [Fact]
    public void Cross_SnapsSmallPerpendicularComponent()
    {
        var r = P(Base with { InnerShape = InnerShape.Cross, AxialDeadzone = 0.1f }, 0.8f, 0.05f);
        Assert.Equal(0f, r.Y, 5);
        Assert.True(r.X > 0.7f);
    }

    [Fact]
    public void Hybrid_RoundCenter_ThenAxialSnap()
    {
        var s = Base with { InnerShape = InnerShape.Hybrid, InnerDeadzone = 0.1f, AxialDeadzone = 0.1f };
        Assert.Equal(StickState.Zero, P(s, 0.06f, 0.06f));
        Assert.Equal(0f, P(s, 0.8f, 0.08f).Y, 5);
    }

    [Fact]
    public void OuterSquare_FullDiagonal_GivesOneOne()
    {
        var r = P(Base with { OuterShape = OuterShape.Square }, 0.7071f, 0.7071f);
        Near(1f, r.X);
        Near(1f, r.Y);
    }

    [Fact]
    public void OuterDeadzone_ReachesOneEarly()
    {
        Near(1f, P(Base with { OuterDeadzone = 0.1f }, 0.9f, 0f).X);
    }

    [Fact]
    public void AntiDeadzone_LiftsSmallOutput()
    {
        var s = Base with { InnerDeadzone = 0f, AntiDeadzone = 0.2f };
        Near(0.208f, P(s, 0.01f, 0f).X);
        Assert.Equal(StickState.Zero, P(s, 0f, 0f));
    }

    [Fact]
    public void Curve_IsApplied()
    {
        var curve = new ResponseCurve(new[] { new CurvePoint(0.5f, 0.25f) });
        Near(0.25f, P(Base with { InnerDeadzone = 0f, Curve = curve }, 0.5f, 0f).X);
    }

    [Fact]
    public void Sensitivity_ClampsAtOne()
    {
        Near(1f, P(Base with { Sensitivity = 2f }, 0.6f, 0f).X);
    }

    [Fact]
    public void Invert_FlipsAxes()
    {
        var r = P(Base with { InnerDeadzone = 0f, InvertX = true, InvertY = true }, 0.5f, 0.25f);
        Assert.True(r.X < 0 && r.Y < 0);
    }

    [Fact]
    public void ZeroInput_NeverNaN_AllShapes()
    {
        foreach (var inner in Enum.GetValues<InnerShape>())
            foreach (var outer in Enum.GetValues<OuterShape>())
            {
                var s = Base with { InnerShape = inner, OuterShape = outer, InnerDeadzone = 0f, AxialDeadzone = 0f };
                Assert.Equal(StickState.Zero, P(s, 0f, 0f));
            }
    }

    [Fact]
    public void CalibrationOffset_ClampsOutput()
    {
        var r = P(Base with { CenterOffsetX = -0.2f }, 1f, 0f);
        Assert.Equal((short)32767, StickProcessor.ToXbox(r.X));
    }

    // ---- Accrochage aux axes (transition douce) ----

    private static readonly StickSettings SnapBase = Base with { InnerDeadzone = 0f };

    private static StickState AtAngle(StickSettings s, float degrees, float magnitude = 1f)
    {
        float a = degrees * MathF.PI / 180f;
        return P(s, magnitude * MathF.Cos(a), magnitude * MathF.Sin(a));
    }

    private static float AngleDeg(StickState s) => MathF.Atan2(s.Y, s.X) * 180f / MathF.PI;

    [Theory]
    [InlineData(8f, 0f)]
    [InlineData(-8f, 0f)]
    [InlineData(98f, 90f)]
    [InlineData(172f, 180f)]
    [InlineData(-82f, -90f)]
    public void AxisSnap_WithinAngle_SnapsToAxis(float input, float expectedAxis)
    {
        var r = AtAngle(SnapBase with { AxisSnapAngle = 10f }, input);
        float diff = MathF.Abs(AngleDeg(r) - expectedAxis);
        Assert.True(diff < 0.01f || MathF.Abs(diff - 360f) < 0.01f, $"angle {AngleDeg(r)}");
        Near(1f, Mag(r)); // la magnitude n'est pas touchée
    }

    [Fact]
    public void AxisSnap_JustOutside_IsContinuous()
    {
        // 10,5° avec 10° d'accrochage → (0,5 × 45/35) ≈ 0,64° : pas de saut.
        Assert.InRange(AngleDeg(AtAngle(SnapBase with { AxisSnapAngle = 10f }, 10.5f)), 0.5f, 0.8f);
    }

    [Fact]
    public void AxisSnap_Diagonal_IsUnchanged()
    {
        Assert.InRange(AngleDeg(AtAngle(SnapBase with { AxisSnapAngle = 20f }, 45f)), 44.99f, 45.01f);
    }

    [Fact]
    public void AxisSnap_Zero_ChangesNothing()
    {
        Assert.InRange(AngleDeg(AtAngle(SnapBase, 8f)), 7.99f, 8.01f);
    }

    [Fact]
    public void AxisSnap_ZeroInput_StaysZero()
    {
        Assert.Equal(StickState.Zero, P(SnapBase with { AxisSnapAngle = 30f }, 0f, 0f));
    }

    [Fact]
    public void ToXbox_Bounds()
    {
        Assert.Equal((short)32767, StickProcessor.ToXbox(1f));
        Assert.Equal((short)-32767, StickProcessor.ToXbox(-1f));
        Assert.Equal((short)0, StickProcessor.ToXbox(0f));
        Assert.Equal((short)32767, StickProcessor.ToXbox(2f));
    }
}
