using System.Windows.Input;
using DualTune.App.Engine;
using DualTune.App.Localization;
using DualTune.Core;

namespace DualTune.App.ViewModels;

/// <summary>Onglet Boutons : un remapping par bouton physique et un voyant en direct.</summary>
public sealed class ButtonsViewModel : ViewModelBase
{
    /// <summary>Boutons physiques dans l'ordre d'affichage ; libellés traduits « button.&lt;nom&gt; ».</summary>
    private static readonly Buttons[] Order =
    {
        Buttons.Cross, Buttons.Circle, Buttons.Square, Buttons.Triangle,
        Buttons.L1, Buttons.R1, Buttons.L2Click, Buttons.R2Click, Buttons.L3, Buttons.R3,
        Buttons.Create, Buttons.Options, Buttons.PS, Buttons.Touchpad, Buttons.Mute,
        Buttons.DpadUp, Buttons.DpadDown, Buttons.DpadLeft, Buttons.DpadRight,
        Buttons.LeftPaddle, Buttons.RightPaddle, Buttons.LeftFn, Buttons.RightFn,
    };

    private readonly Func<Profile> _get;
    private readonly Action<Func<Profile, Profile>> _update;

    public ButtonsViewModel(Func<Profile> get, Action<Func<Profile, Profile>> update)
    {
        _get = get;
        _update = update;
        Rows = Order.Select(b => new ButtonRow(b, SetTarget)).ToList();
        ButtonRow R(Buttons b) => Rows.Single(r => r.Source == b);
        Groups = new[]
        {
            new ButtonGroup("buttons.groupFace", new[] { R(Buttons.Cross), R(Buttons.Circle), R(Buttons.Square), R(Buttons.Triangle) }),
            new ButtonGroup("buttons.groupShoulders", new[] { R(Buttons.L1), R(Buttons.R1), R(Buttons.L2Click), R(Buttons.R2Click), R(Buttons.L3), R(Buttons.R3) }),
            new ButtonGroup("buttons.groupDpad", new[] { R(Buttons.DpadUp), R(Buttons.DpadDown), R(Buttons.DpadLeft), R(Buttons.DpadRight) }),
            new ButtonGroup("buttons.groupSystem", new[] { R(Buttons.Create), R(Buttons.Options), R(Buttons.PS), R(Buttons.Touchpad), R(Buttons.Mute) }),
            new ButtonGroup("buttons.groupEdge", new[] { R(Buttons.LeftPaddle), R(Buttons.RightPaddle), R(Buttons.LeftFn), R(Buttons.RightFn) }),
        };
        ResetCommand = new RelayCommand(() =>
        {
            _update(p => p with { ButtonMap = new Dictionary<Buttons, XboxTarget>(ButtonMapper.Default) });
            Refresh();
        });
        Refresh();
    }

    /// <summary>Sorties Xbox proposées dans les listes (libellés traduits « target.&lt;nom&gt; »).</summary>
    public static IReadOnlyList<LocalizedItem<XboxTarget>> Targets { get; } =
        Enum.GetValues<XboxTarget>().Select(t => new LocalizedItem<XboxTarget>(t, "target." + t)).ToList();

    public IReadOnlyList<ButtonRow> Rows { get; }

    /// <summary>Les mêmes rangées, regroupées par famille pour l'affichage.</summary>
    public IReadOnlyList<ButtonGroup> Groups { get; }

    public ICommand ResetCommand { get; }

    public void OnTick(EngineSnapshot snap)
    {
        foreach (var r in Rows) r.IsPressed = (snap.Raw.Buttons & r.Source) != 0;
    }

    public void Refresh()
    {
        var map = _get().ButtonMap;
        foreach (var r in Rows) r.SetTargetSilently(map.GetValueOrDefault(r.Source, XboxTarget.None));
    }

    private void SetTarget(Buttons source, XboxTarget target) =>
        _update(p => p with { ButtonMap = new Dictionary<Buttons, XboxTarget>(p.ButtonMap) { [source] = target } });
}

/// <summary>Famille de boutons ; le titre suit la langue.</summary>
public sealed class ButtonGroup : ViewModelBase
{
    private readonly string _titleKey;

    public ButtonGroup(string titleKey, IReadOnlyList<ButtonRow> rows)
    {
        _titleKey = titleKey;
        Rows = rows;
        Loc.Instance.LanguageChanged += () => Raise(nameof(Title));
    }

    public string Title => Loc.T(_titleKey);
    public IReadOnlyList<ButtonRow> Rows { get; }
}

public sealed class ButtonRow : ViewModelBase
{
    private readonly Action<Buttons, XboxTarget> _onChange;
    private XboxTarget _target;
    private bool _isPressed;

    public ButtonRow(Buttons source, Action<Buttons, XboxTarget> onChange)
    {
        Source = source;
        _onChange = onChange;
        Loc.Instance.LanguageChanged += () => Raise(nameof(Label));
    }

    public Buttons Source { get; }
    public string Label => Loc.T("button." + Source);

    public XboxTarget Target
    {
        get => _target;
        set
        {
            if (Set(ref _target, value)) _onChange(Source, value);
        }
    }

    public bool IsPressed { get => _isPressed; set => Set(ref _isPressed, value); }

    internal void SetTargetSilently(XboxTarget t)
    {
        _target = t;
        Raise(nameof(Target));
    }
}
