using DualTune.App.Guide;

namespace DualTune.Tests;

public class SystemProbeTests
{
    [Fact]
    public void MatchConflicts_FindsKnownCaseInsensitive() =>
        Assert.Equal(new[] { "DS4Windows" }, SystemProbe.MatchConflicts(new[] { "ds4windows", "explorer" }));

    [Fact]
    public void MatchConflicts_DedupesReWasd() =>
        Assert.Equal(new[] { "reWASD" }, SystemProbe.MatchConflicts(new[] { "reWASDEngine", "reWASDTray" }));

    [Fact]
    public void MatchConflicts_OrderStable() =>
        Assert.Equal(new[] { "DS4Windows", "reWASD", "DSX" },
            SystemProbe.MatchConflicts(new[] { "DSX", "reWASDTray", "DS4Windows" }));
}
