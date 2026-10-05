using DualTune.App.Engine;
using DualTune.App.ViewModels;
using DualTune.Core;

namespace DualTune.Tests;

public class TriggersAndButtonsViewModelTests
{
    private Profile _profile = Profile.CreateDefault();

    private Func<Profile> Get => () => _profile;
    private Action<Func<Profile, Profile>> Update => f => _profile = f(_profile);

    [Fact]
    public void Trigger_SetInner_UpdatesThatTriggerOnly()
    {
        var vm = new TriggerViewModel(Get, Update, isL2: true);
        vm.InnerDeadzone = 0.1f;
        vm.AntiDeadzone = 0.2f;
        Assert.Equal(0.1f, _profile.L2.InnerDeadzone);
        Assert.Equal(0.2f, _profile.L2.AntiDeadzone);
        Assert.Equal(new TriggerSettings(), _profile.R2);
    }

    [Fact]
    public void TriggerViewModel_Summary_Format()
    {
        var vm = new TriggerViewModel(Get, Update, isL2: true);
        Assert.Equal("2% · 2% · 0%", vm.Summary);
        vm.AntiDeadzone = 0.15f;
        Assert.Equal("2% · 2% · 15%", vm.Summary);
    }

    [Fact]
    public void Trigger_OnTick_ShowsRawAndOutput()
    {
        var vm = new TriggerViewModel(Get, Update, isL2: false);
        vm.OnTick(EngineSnapshot.Empty with
        {
            Raw = ControllerState.Neutral with { R2 = 0.5f },
            Output = XboxState.Neutral with { RT = 200 },
        });
        Assert.Equal(0.5, vm.RawValue, 3);
        Assert.Equal(200 / 255.0, vm.OutputValue, 3);
    }

    [Fact]
    public void Buttons_RowsCoverAllButtons()
    {
        var vm = new ButtonsViewModel(Get, Update);
        var all = Enum.GetValues<Buttons>().Where(b => b != Buttons.None).ToList();
        Assert.Equal(all.Count, vm.Rows.Count);
        Assert.All(vm.Rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Label)));
    }

    [Fact]
    public void Groups_CoverEveryRowOnce()
    {
        var vm = new ButtonsViewModel(Get, Update);
        var grouped = vm.Groups.SelectMany(g => g.Rows).ToList();
        Assert.Equal(vm.Rows.Count, grouped.Count);
        Assert.All(vm.Rows, r => Assert.Contains(grouped, x => ReferenceEquals(x, r)));
    }

    [Fact]
    public void Groups_TitlesInOrder() =>
        Assert.Equal(new[] { "FACE", "ÉPAULES ET GÂCHETTES", "CROIX DIRECTIONNELLE", "SYSTÈME", "EDGE" },
            new ButtonsViewModel(Get, Update).Groups.Select(g => g.Title));

    [Fact]
    public void Buttons_SetTarget_UpdatesMap()
    {
        var vm = new ButtonsViewModel(Get, Update);
        vm.Rows.Single(r => r.Source == Buttons.RightPaddle).Target = XboxTarget.A;
        Assert.Equal(XboxTarget.A, _profile.ButtonMap[Buttons.RightPaddle]);
        Assert.Equal(XboxTarget.A, _profile.ButtonMap[Buttons.Cross]);
    }

    [Fact]
    public void Buttons_OnTick_SetsPressed()
    {
        var vm = new ButtonsViewModel(Get, Update);
        vm.OnTick(EngineSnapshot.Empty with { Raw = ControllerState.Neutral with { Buttons = Buttons.LeftPaddle } });
        Assert.True(vm.Rows.Single(r => r.Source == Buttons.LeftPaddle).IsPressed);
        Assert.False(vm.Rows.Single(r => r.Source == Buttons.Cross).IsPressed);
    }

    [Fact]
    public void Buttons_Reset_RestoresDefault()
    {
        var vm = new ButtonsViewModel(Get, Update);
        vm.Rows.Single(r => r.Source == Buttons.Cross).Target = XboxTarget.Y;
        vm.ResetCommand.Execute(null);
        Assert.Equal(XboxTarget.A, _profile.ButtonMap[Buttons.Cross]);
        Assert.Equal(XboxTarget.A, vm.Rows.Single(r => r.Source == Buttons.Cross).Target);
    }

    [Fact]
    public void Buttons_Refresh_ReadsMapFromNewProfile()
    {
        var vm = new ButtonsViewModel(Get, Update);
        _profile = _profile with { ButtonMap = new Dictionary<Buttons, XboxTarget> { [Buttons.Cross] = XboxTarget.B } };
        vm.Refresh();
        Assert.Equal(XboxTarget.B, vm.Rows.Single(r => r.Source == Buttons.Cross).Target);
        Assert.Equal(XboxTarget.None, vm.Rows.Single(r => r.Source == Buttons.Circle).Target);
    }
}
