using DualTune.Output;

namespace DualTune.Tests;

public sealed class HidHideControllerTests : IDisposable
{
    private const string Dev = @"HID\VID_054C&PID_0DF2&MI_03\7&1A2B3C&0&0000";
    private const string Exe = @"C:\DualTune\DualTune.App.exe";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DualTuneTests", Guid.NewGuid().ToString("N"));
    private string Marker => Path.Combine(_dir, "hidden.txt");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private sealed class FakeDriver : IHidHideDriver
    {
        public List<string> Calls { get; } = new();
        public bool FailAppReg { get; init; }
        public bool IsInstalled { get; init; } = true;

        public bool IsActive
        {
            get => Calls.Contains("cloak-on");
            set => Calls.Add(value ? "cloak-on" : "cloak-off");
        }

        public void AddApplicationPath(string path)
        {
            if (FailAppReg) throw new UnauthorizedAccessException("refusé");
            Calls.Add($"app-reg {path}");
        }

        /// <summary>Instances déjà masquées par un autre logiciel de manette.</summary>
        public HashSet<string> AlreadyBlocked { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool AddBlockedInstanceId(string id)
        {
            if (AlreadyBlocked.Contains(id)) return false;
            Calls.Add($"dev-hide {id}");
            return true;
        }

        public void RemoveBlockedInstanceId(string id) => Calls.Add($"dev-unhide {id}");
    }

    [Fact]
    public void RegisterApp_AddsExeToAllowList()
    {
        var drv = new FakeDriver();
        Assert.True(new HidHideController(drv, _dir).RegisterApp(Exe));
        Assert.Equal(new[] { $"app-reg {Exe}" }, drv.Calls);
    }

    [Fact]
    public void RegisterApp_Failure_SetsError()
    {
        var c = new HidHideController(new FakeDriver { FailAppReg = true }, _dir);
        Assert.False(c.RegisterApp(Exe));
        Assert.NotNull(c.Error);
    }

    [Fact]
    public void Hide_DeviceAlreadyBlockedByAnotherApp_IsNotUnhiddenOnExit()
    {
        var drv = new FakeDriver();
        drv.AlreadyBlocked.Add(Dev.ToLowerInvariant());
        var c = new HidHideController(drv, _dir);
        Assert.True(c.Hide(Dev, Exe));
        c.Unhide();
        Assert.DoesNotContain(drv.Calls, x => x.StartsWith("dev-unhide"));
        Assert.False(File.Exists(Marker));
    }

    [Fact]
    public void Hide_RunsAppRegThenDevHideThenCloakOn()
    {
        var drv = new FakeDriver();
        Assert.True(new HidHideController(drv, _dir).Hide(Dev, Exe));
        Assert.Equal(new[] { $"app-reg {Exe}", $"dev-hide {Dev}", "cloak-on" }, drv.Calls);
        Assert.Equal(Dev, File.ReadAllText(Marker));
    }

    [Fact]
    public void Hide_WhenAppRegFails_DoesNotHideDevice()
    {
        var drv = new FakeDriver { FailAppReg = true };
        var c = new HidHideController(drv, _dir);
        Assert.False(c.Hide(Dev, Exe));
        Assert.DoesNotContain(drv.Calls, x => x.StartsWith("dev-hide"));
        Assert.NotNull(c.Error);
        Assert.False(File.Exists(Marker));
    }

    [Fact]
    public void Unhide_RunsDevUnhide_AndDeletesMarker()
    {
        var drv = new FakeDriver();
        var c = new HidHideController(drv, _dir);
        c.Hide(Dev, Exe);
        c.Unhide();
        Assert.Contains($"dev-unhide {Dev}", drv.Calls);
        Assert.False(File.Exists(Marker));
    }

    [Fact]
    public void RecoverFromCrash_UnhidesMarkedDevice()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Marker, Dev);
        var drv = new FakeDriver();
        new HidHideController(drv, _dir).RecoverFromCrash();
        Assert.Equal(new[] { $"dev-unhide {Dev}" }, drv.Calls);
        Assert.False(File.Exists(Marker));
    }

    [Fact]
    public void NoDriver_IsUnavailable()
    {
        var c = new HidHideController(null, _dir);
        Assert.False(c.IsAvailable);
        Assert.False(c.Hide(Dev, Exe));
    }

    [Fact]
    public void DriverNotInstalled_IsUnavailable()
    {
        var drv = new FakeDriver { IsInstalled = false };
        var c = new HidHideController(drv, _dir);
        Assert.False(c.IsAvailable);
        Assert.False(c.Hide(Dev, Exe));
        Assert.Empty(drv.Calls);
    }

    [Fact]
    public void IsHiding_TrueOnlyAfterSuccessfulHide()
    {
        var drv = new FakeDriver();
        var c = new HidHideController(drv, _dir);
        Assert.False(c.IsHiding);
        c.Hide(Dev, Exe);
        Assert.True(c.IsHiding);
        c.Unhide();
        Assert.False(c.IsHiding);
    }

    [Fact]
    public void IsHiding_FalseWhenAppRegFails()
    {
        var c = new HidHideController(new FakeDriver { FailAppReg = true }, _dir);
        c.Hide(Dev, Exe);
        Assert.False(c.IsHiding);
    }
}
