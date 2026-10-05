using DualTune.Core;

namespace DualTune.Tests;

public class ResponseCurveTests
{
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(1f, 1f)]
    public void Linear_Evaluate_IsIdentity(float input, float expected)
    {
        Assert.Equal(expected, ResponseCurve.Linear.Evaluate(input), 4);
    }

    [Fact]
    public void Evaluate_InterpolatesBetweenPoints()
    {
        var c = new ResponseCurve(new[] { new CurvePoint(0, 0), new CurvePoint(0.5f, 0.2f), new CurvePoint(1, 1) });
        Assert.Equal(0.1f, c.Evaluate(0.25f), 4);
        Assert.Equal(0.6f, c.Evaluate(0.75f), 4);
    }

    [Fact]
    public void Evaluate_ClampsInput()
    {
        Assert.Equal(0f, ResponseCurve.Linear.Evaluate(-0.5f), 4);
        Assert.Equal(1f, ResponseCurve.Linear.Evaluate(1.5f), 4);
    }

    [Fact]
    public void AllPresets_PassThroughEndpoints_AndAreMonotonic()
    {
        Assert.Equal(5, ResponseCurve.Presets.Count);
        foreach (var (name, curve) in ResponseCurve.Presets)
        {
            Assert.Equal(0f, curve.Evaluate(0), 4);
            Assert.Equal(1f, curve.Evaluate(1), 4);
            float prev = 0;
            for (int i = 0; i <= 100; i++)
            {
                float v = curve.Evaluate(i / 100f);
                Assert.True(v >= prev - 1e-6f, $"{name} non monotone à {i}");
                prev = v;
            }
        }
    }

    [Fact]
    public void Sanitize_ForcesEndpoints_SortsAndEnforcesMonotonic()
    {
        var pts = ResponseCurve.Sanitize(new[]
        {
            new CurvePoint(0.7f, 0.3f), new CurvePoint(0.3f, 0.6f), new CurvePoint(0.3f, 0.5f),
        });
        Assert.Equal(new CurvePoint(0, 0), pts[0]);
        Assert.Equal(new CurvePoint(1, 1), pts[^1]);
        Assert.Equal(4, pts.Count); // (0,0) (0.3,0.5) (0.7,..) (1,1) — X dupliqué dédoublonné
        for (int i = 1; i < pts.Count; i++)
        {
            Assert.True(pts[i].X > pts[i - 1].X);
            Assert.True(pts[i].Y >= pts[i - 1].Y);
        }
    }
}
