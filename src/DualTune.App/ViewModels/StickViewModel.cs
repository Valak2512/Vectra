using System.Windows.Input;
using DualTune.App.Engine;
using DualTune.App.Localization;
using DualTune.Core;

namespace DualTune.App.ViewModels;

/// <summary>Réglages et affichage d'un stick (onglet Sticks).</summary>
public sealed class StickViewModel : ViewModelBase
{
    private const float MaxOffset = 0.2f;
    private readonly Func<Profile> _get;
    private readonly Action<Func<Profile, Profile>> _update;
    private readonly bool _isLeft;
    private StickState _raw;
    private StickState _processed;

    public StickViewModel(Func<Profile> get, Action<Func<Profile, Profile>> update, bool isLeft)
    {
        _get = get;
        _update = update;
        _isLeft = isLeft;
        CalibrateCommand = new RelayCommand(Calibrate);
        ResetCommand = new RelayCommand(() => Edit(_ => new StickSettings()));
        Loc.Instance.LanguageChanged += RaiseAll; // résumés des sections
    }

    public StickSettings Settings => _isLeft ? _get().LeftStick : _get().RightStick;

    public InnerShape InnerShape { get => Settings.InnerShape; set => Edit(s => s with { InnerShape = value }); }
    public OuterShape OuterShape { get => Settings.OuterShape; set => Edit(s => s with { OuterShape = value }); }
    public float InnerDeadzone { get => Settings.InnerDeadzone; set => Edit(s => s with { InnerDeadzone = value }); }
    public float AxialDeadzone { get => Settings.AxialDeadzone; set => Edit(s => s with { AxialDeadzone = value }); }
    public float OuterDeadzone { get => Settings.OuterDeadzone; set => Edit(s => s with { OuterDeadzone = value }); }
    public float HorizontalSnapAngle { get => Settings.HorizontalSnapAngle; set => Edit(s => s with { HorizontalSnapAngle = value }); }
    public float VerticalSnapAngle { get => Settings.VerticalSnapAngle; set => Edit(s => s with { VerticalSnapAngle = value }); }
    public float AntiDeadzone { get => Settings.AntiDeadzone; set => Edit(s => s with { AntiDeadzone = value }); }
    public float Sensitivity { get => Settings.Sensitivity; set => Edit(s => s with { Sensitivity = value }); }
    public float CenterOffsetX { get => Settings.CenterOffsetX; set => Edit(s => s with { CenterOffsetX = value }); }
    public float CenterOffsetY { get => Settings.CenterOffsetY; set => Edit(s => s with { CenterOffsetY = value }); }
    public bool InvertX { get => Settings.InvertX; set => Edit(s => s with { InvertX = value }); }
    public bool InvertY { get => Settings.InvertY; set => Edit(s => s with { InvertY = value }); }

    public bool ShowAxial => InnerShape is InnerShape.Cross or InnerShape.Hybrid;

    // Propriétés « bouton radio » pour les sélecteurs inclinés.
    public bool IsInnerRound { get => InnerShape == InnerShape.Round; set { if (value) InnerShape = InnerShape.Round; } }
    public bool IsInnerSquare { get => InnerShape == InnerShape.Square; set { if (value) InnerShape = InnerShape.Square; } }
    public bool IsInnerCross { get => InnerShape == InnerShape.Cross; set { if (value) InnerShape = InnerShape.Cross; } }
    public bool IsInnerHybrid { get => InnerShape == InnerShape.Hybrid; set { if (value) InnerShape = InnerShape.Hybrid; } }
    public bool IsOuterCircle { get => OuterShape == OuterShape.Circle; set { if (value) OuterShape = OuterShape.Circle; } }
    public bool IsOuterSquare { get => OuterShape == OuterShape.Square; set { if (value) OuterShape = OuterShape.Square; } }

    // Résumés affichés dans les bandeaux de section.
    public string DeadzoneSummary => $"{InnerLabel(InnerShape)} · {Pct(InnerDeadzone)} · {Loc.T(OuterShape == OuterShape.Square ? "shape.squareOuter" : "shape.circle")}";
    public string ResponseSummary =>
        $"{Pct(Sensitivity)} · H {HorizontalSnapAngle.ToString("0.#", Inv)}° · V {VerticalSnapAngle.ToString("0.#", Inv)}°";
    public string CalibrationSummary => $"X {Signed(CenterOffsetX)} · Y {Signed(CenterOffsetY)}";

    public string InputReadout => $"{Raw.X.ToString("0.00", Inv)} / {Raw.Y.ToString("0.00", Inv)}";
    public string OutputReadout => $"{Processed.X.ToString("0.00", Inv)} / {Processed.Y.ToString("0.00", Inv)}";

    private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
    private static string Pct(float v) => (v * 100).ToString("0", Inv) + "%";
    private static string Signed(float v) => v.ToString("+0.000;-0.000;+0.000", Inv);
    private static string InnerLabel(InnerShape s) => Loc.T(s switch
    {
        InnerShape.Square => "shape.square",
        InnerShape.Cross => "shape.cross",
        InnerShape.Hybrid => "shape.hybrid",
        _ => "shape.round",
    });

    public StickState Raw { get => _raw; private set { if (Set(ref _raw, value)) Raise(nameof(InputReadout)); } }
    public StickState Processed { get => _processed; private set { if (Set(ref _processed, value)) Raise(nameof(OutputReadout)); } }

    public ICommand CalibrateCommand { get; }
    public ICommand ResetCommand { get; }

    public void OnTick(EngineSnapshot snap)
    {
        Raw = _isLeft ? snap.Raw.Left : snap.Raw.Right;
        Processed = _isLeft ? snap.LeftProcessed : snap.RightProcessed;
    }

    /// <summary>À appeler après un changement de profil actif.</summary>
    public void Refresh() => RaiseAll();

    private void Calibrate() => Edit(s => s with
    {
        CenterOffsetX = Math.Clamp(Raw.X, -MaxOffset, MaxOffset),
        CenterOffsetY = Math.Clamp(Raw.Y, -MaxOffset, MaxOffset),
    });

    private void Edit(Func<StickSettings, StickSettings> f)
    {
        _update(p => _isLeft ? p with { LeftStick = f(p.LeftStick) } : p with { RightStick = f(p.RightStick) });
        RaiseAll();
    }
}
