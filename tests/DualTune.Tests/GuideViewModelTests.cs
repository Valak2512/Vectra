using DualTune.App.Guide;
using DualTune.Core;
using DualTune.Device;

namespace DualTune.Tests;

public class GuideViewModelTests
{
    private sealed class FakeProbe : ISystemProbe
    {
        public bool ViGEm { get; set; }
        public bool HidHide { get; set; }
        public bool Winget { get; set; } = true;
        public List<string> Conflicts = new();
        public TaskCompletionSource<int>? Pending;
        public int InstallResult { get; set; }
        public Action? OnInstall;
        public List<string> Installed = new();

        public bool IsViGEmInstalled() => ViGEm;
        public bool IsHidHideInstalled() => HidHide;
        public IReadOnlyList<string> RunningConflicts() => Conflicts;
        public bool IsWingetAvailable() => Winget;

        public Task<int> WingetInstallAsync(string packageId)
        {
            Installed.Add(packageId);
            if (Pending is not null) return Pending.Task;
            OnInstall?.Invoke();
            return Task.FromResult(InstallResult);
        }
    }

    private readonly FakeProbe _probe = new();
    private GuideContext _ctx = new(ConnectionKind.None, ControllerState.Neutral, VirtualPadReady: false, HidingActive: false);
    private DateTime _now = new(2026, 10, 4, 12, 0, 0);
    private int _done;
    private int _restarts;

    private readonly InstallGate _gate = new();

    private GuideViewModel Make() => new(_probe, () => _ctx, () => _now, () => _done++, () => _restarts++, _gate);

    private void Tick(GuideViewModel vm, GuideContext ctx)
    {
        _ctx = ctx;
        vm.OnTick(ctx);
    }

    [Fact]
    public void Steps_SevenInOrder() =>
        Assert.Equal(new[] { "BIENVENUE", "PILOTE VIGEMBUS", "PILOTE HIDHIDE", "LOGICIELS EN CONFLIT", "MANETTE", "TEST", "TERMINÉ" },
            Make().Steps.Select(s => s.Title));

    [Fact]
    public void Refresh_SetsDriverAndConflictStatuses()
    {
        _probe.ViGEm = true;
        _probe.Conflicts.Add("DS4Windows");
        var vm = Make();
        vm.Refresh();
        Assert.Equal(StepStatus.Ok, vm.Steps[GuideViewModel.ViGEmStep].Status);
        Assert.Equal(StepStatus.Todo, vm.Steps[GuideViewModel.HidHideStep].Status);
        Assert.Equal(StepStatus.Todo, vm.Steps[GuideViewModel.ConflictsStep].Status);
        Assert.Equal("DS4Windows", vm.ConflictsText);
        _probe.Conflicts.Clear();
        _now = _now.AddSeconds(2);
        vm.Refresh();
        Assert.Equal(StepStatus.Ok, vm.Steps[GuideViewModel.ConflictsStep].Status);
    }

    [Fact]
    public void NextLabel_PasserWhenNotOk()
    {
        var vm = Make();
        vm.Refresh();
        Assert.Equal("SUIVANT", vm.NextLabel); // bienvenue
        vm.NextCommand.Execute(null);
        Assert.Equal(GuideViewModel.ViGEmStep, vm.Current);
        Assert.Equal("PASSER", vm.NextLabel);
    }

    [Fact]
    public void Test_RequiresAllFourAndRemembers()
    {
        var vm = Make();
        var ok = new GuideContext(ConnectionKind.Usb, ControllerState.Neutral, true, true);
        Tick(vm, ok with { Raw = ControllerState.Neutral with { Buttons = Buttons.Cross } });
        Tick(vm, ok);
        Assert.True(vm.SawCross);
        Assert.False(vm.SawStick);
        vm.Refresh();
        Assert.Equal(StepStatus.Todo, vm.Steps[GuideViewModel.TestStep].Status);

        Tick(vm, ok with { Raw = ControllerState.Neutral with { Left = new(0.95f, 0f) } });
        Tick(vm, ok); // stick relâché : reste mémorisé
        _now = _now.AddSeconds(2);
        vm.Refresh();
        Assert.Equal(StepStatus.Ok, vm.Steps[GuideViewModel.TestStep].Status);

        Tick(vm, ok with { HidingActive = false });
        _now = _now.AddSeconds(2);
        vm.Refresh();
        Assert.Equal(StepStatus.Todo, vm.Steps[GuideViewModel.TestStep].Status);
    }

    [Fact]
    public void CableTip_AfterTwoDropsWithin60s()
    {
        var vm = Make();
        var usb = new GuideContext(ConnectionKind.Usb, ControllerState.Neutral, true, true);
        var none = usb with { Connection = ConnectionKind.None };
        Tick(vm, usb); Tick(vm, none);
        _now = _now.AddSeconds(30);
        Tick(vm, usb); Tick(vm, none);
        Assert.True(vm.ShowCableTip);
    }

    [Fact]
    public void CableTip_NotWhenDropsFarApart()
    {
        var vm = Make();
        var usb = new GuideContext(ConnectionKind.Usb, ControllerState.Neutral, true, true);
        var none = usb with { Connection = ConnectionKind.None };
        Tick(vm, usb); Tick(vm, none);
        _now = _now.AddSeconds(90);
        Tick(vm, usb); Tick(vm, none);
        Assert.False(vm.ShowCableTip);
    }

    [Fact]
    public async Task Install_Success_RefreshesStep()
    {
        _probe.OnInstall = () => _probe.ViGEm = true;
        var vm = Make();
        await vm.InstallAsync(GuideViewModel.ViGEmStep);
        Assert.Equal(new[] { "ViGEm.ViGEmBus" }, _probe.Installed);
        Assert.Equal(StepStatus.Ok, vm.Steps[GuideViewModel.ViGEmStep].Status);
        Assert.False(vm.IsInstalling);
        Assert.Null(vm.InstallError);
    }

    [Fact]
    public async Task Install_Failure_ShowsError()
    {
        _probe.Pending = new TaskCompletionSource<int>();
        var vm = Make();
        var task = vm.InstallAsync(GuideViewModel.HidHideStep);
        Assert.True(vm.IsInstalling);
        _probe.Pending.SetResult(1);
        await task;
        Assert.False(vm.IsInstalling);
        Assert.NotNull(vm.InstallError);
        Assert.Equal(new[] { "Nefarius.HidHide" }, _probe.Installed);
    }

    [Fact]
    public async Task Install_NoWinget_CannotInstall()
    {
        _probe.Winget = false;
        var vm = Make();
        await vm.Ready;
        Assert.False(vm.CanInstall);
        await vm.InstallAsync(GuideViewModel.ViGEmStep);
        Assert.Empty(_probe.Installed);
    }

    [Fact]
    public void Finish_And_Close_CallOnDoneOnce()
    {
        _probe.ViGEm = _probe.HidHide = true;
        var vm = Make();
        vm.FinishCommand.Execute(null);
        vm.Close();
        Assert.Equal(1, _done);
    }

    [Fact]
    public void Back_Next_Bounds()
    {
        var vm = Make();
        vm.BackCommand.Execute(null);
        Assert.Equal(0, vm.Current);
        for (int i = 0; i < 20; i++) vm.NextCommand.Execute(null);
        Assert.Equal(6, vm.Current);
        Assert.True(vm.IsLast);
        Assert.True(vm.Steps[6].IsCurrent);
        Assert.False(vm.Steps[0].IsCurrent);
    }

    // ---- Corrections de la relecture finale ----

    [Fact]
    public void DriverInstalledButNotLoaded_IsWarnAndNeedsRestart()
    {
        _probe.ViGEm = true;
        _probe.HidHide = true;
        _ctx = _ctx with { ViGEmLoaded = false, HidHideLoaded = true };
        var vm = Make();
        vm.Refresh();
        Assert.Equal(StepStatus.Warn, vm.Steps[GuideViewModel.ViGEmStep].Status);
        Assert.Equal(StepStatus.Ok, vm.Steps[GuideViewModel.HidHideStep].Status);
        Assert.True(vm.RestartNeeded);
    }

    [Fact]
    public void Restart_DoesNotMarkDone()
    {
        _probe.ViGEm = true;
        _ctx = _ctx with { ViGEmLoaded = false };
        var vm = Make();
        vm.RestartCommand.Execute(null);
        vm.Close(); // la fenêtre se ferme pendant le redémarrage
        Assert.Equal(1, _restarts);
        Assert.Equal(0, _done);
    }

    [Fact]
    public void Close_MarksDone_OnlyWhenDriversReady()
    {
        var vm = Make(); // pilotes absents
        vm.Close();
        Assert.Equal(0, _done);

        _probe.ViGEm = _probe.HidHide = true;
        var ok = Make();
        ok.Close();
        Assert.Equal(1, _done);
    }

    [Fact]
    public void Finish_AlwaysMarksDone()
    {
        var vm = Make(); // même sans pilotes : l'utilisateur a choisi de terminer
        vm.FinishCommand.Execute(null);
        Assert.Equal(1, _done);
    }

    [Fact]
    public async Task Install_NonZeroButInstalled_IsSuccess()
    {
        _probe.InstallResult = unchecked((int)0x8A15002B); // « déjà installé »
        _probe.OnInstall = () => _probe.ViGEm = true;
        var vm = Make();
        await vm.Ready;
        await vm.InstallAsync(GuideViewModel.ViGEmStep);
        Assert.Null(vm.InstallError);
    }

    [Fact]
    public async Task ShowInstall_HiddenWhenStepOk()
    {
        var vm = Make();
        await vm.Ready;
        Assert.True(vm.ShowInstallViGEm);
        _probe.ViGEm = true;
        _now = _now.AddSeconds(2);
        vm.Refresh();
        Assert.False(vm.ShowInstallViGEm);
        Assert.True(vm.ShowInstallHidHide);
    }

    [Fact]
    public async Task Install_SecondCallWhilePending_IsNoop()
    {
        _probe.Pending = new TaskCompletionSource<int>();
        var vm = Make();
        await vm.Ready;
        var first = vm.InstallAsync(GuideViewModel.ViGEmStep);
        await vm.InstallAsync(GuideViewModel.HidHideStep);
        Assert.Single(_probe.Installed);
        _probe.Pending.SetResult(0);
        await first;
    }

    // ---- Petits défauts : installation par page, astuce câble, double installation ----

    [Fact]
    public async Task Installing_ShownOnlyOnItsOwnPage()
    {
        _probe.Pending = new TaskCompletionSource<int>();
        var vm = Make();
        await vm.Ready;
        vm.Current = GuideViewModel.HidHideStep;
        var task = vm.InstallAsync(GuideViewModel.HidHideStep);
        Assert.True(vm.ShowInstalling);
        vm.Current = GuideViewModel.ViGEmStep;
        Assert.False(vm.ShowInstalling);
        _probe.Pending.SetResult(0);
        await task;
    }

    [Fact]
    public async Task InstallError_NotShownOnAnotherPage()
    {
        _probe.Pending = new TaskCompletionSource<int>();
        var vm = Make();
        await vm.Ready;
        vm.Current = GuideViewModel.HidHideStep;
        var task = vm.InstallAsync(GuideViewModel.HidHideStep);
        vm.Current = GuideViewModel.ViGEmStep; // l'utilisateur change de page pendant l'installation
        _probe.Pending.SetResult(1);
        await task;
        Assert.Null(vm.StepError);
        vm.Current = GuideViewModel.HidHideStep;
        Assert.NotNull(vm.StepError);
    }

    [Fact]
    public async Task ReopenedGuide_CannotStartSecondInstall()
    {
        _probe.Pending = new TaskCompletionSource<int>();
        var first = Make();
        await first.Ready;
        var task = first.InstallAsync(GuideViewModel.ViGEmStep);
        first.Close(); // fenêtre fermée pendant l'installation, puis rouverte
        var second = Make();
        await second.Ready;
        Assert.True(second.IsInstalling);
        await second.InstallAsync(GuideViewModel.HidHideStep);
        Assert.Single(_probe.Installed);
        _probe.Pending.SetResult(0);
        await task;
        Assert.False(second.IsInstalling);
    }

    [Fact]
    public void CableTip_HidesAfterAStableMinute()
    {
        var vm = Make();
        var usb = new GuideContext(ConnectionKind.Usb, ControllerState.Neutral, true, true);
        var none = usb with { Connection = ConnectionKind.None };
        Tick(vm, usb); Tick(vm, none);
        _now = _now.AddSeconds(10);
        Tick(vm, usb); Tick(vm, none);
        Tick(vm, usb);
        Assert.True(vm.ShowCableTip);
        _now = _now.AddSeconds(61);
        Tick(vm, usb);
        Assert.False(vm.ShowCableTip);
    }
}
