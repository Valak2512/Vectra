using DualTune.Core;

namespace DualTune.Tests;

public class TriggerProcessorTests
{
    private static readonly TriggerSettings NoDz = new() { InnerDeadzone = 0f, OuterDeadzone = 0f };

    [Fact] public void BelowInner_IsZero() => Assert.Equal(0, TriggerProcessor.Process(0.01f, new TriggerSettings()));

    [Fact] public void AtOuterEdge_IsMax() => Assert.Equal(255, TriggerProcessor.Process(0.98f, new TriggerSettings()));

    [Fact] public void Midpoint_Scales() => Assert.Equal(128, TriggerProcessor.Process(0.5f, NoDz));

    [Fact]
    public void AntiDeadzone_Applies() =>
        Assert.Equal(53, TriggerProcessor.Process(0.01f, NoDz with { AntiDeadzone = 0.2f }));

    [Fact]
    public void Zero_StaysZero_WithAntiDeadzone() =>
        Assert.Equal(0, TriggerProcessor.Process(0f, NoDz with { AntiDeadzone = 0.2f }));
}
