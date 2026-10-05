using System.Windows.Input;
using DualTune.App.Engine;
using DualTune.App.Localization;
using DualTune.Core;

namespace DualTune.App.ViewModels;

public enum CurveTarget { LeftStick, RightStick, L2, R2 }

/// <summary>Onglet Courbes : édite la courbe de réponse de la cible choisie.</summary>
public sealed class CurvesViewModel : ViewModelBase
{
    private readonly Func<Profile> _get;
    private readonly Action<Func<Profile, Profile>> _update;
    private CurveTarget _target = CurveTarget.LeftStick;
    private float _currentInput;

    public CurvesViewModel(Func<Profile> get, Action<Func<Profile, Profile>> update)
    {
        _get = get;
        _update = update;
        ApplyPresetCommand = new RelayCommand(p =>
        {
            if (p is string name && ResponseCurve.Presets.TryGetValue(name, out var c)) Curve = c;
        });
    }

    /// <summary>Préréglages affichés (nom interne + libellé traduit).</summary>
    public static IReadOnlyList<LocalizedItem<string>> Presets { get; } =
        ResponseCurve.Presets.Keys.Select(n => new LocalizedItem<string>(n, "preset." + n)).ToList();

    public ICommand ApplyPresetCommand { get; }

    public CurveTarget Target
    {
        get => _target;
        set
        {
            if (!Set(ref _target, value)) return;
            Raise(nameof(Curve));
            Raise(nameof(IsLeft));
            Raise(nameof(IsRight));
            Raise(nameof(IsL2));
            Raise(nameof(IsR2));
        }
    }

    // Propriétés « bouton radio » pour les sélecteurs inclinés.
    public bool IsLeft { get => _target == CurveTarget.LeftStick; set { if (value) Target = CurveTarget.LeftStick; } }
    public bool IsRight { get => _target == CurveTarget.RightStick; set { if (value) Target = CurveTarget.RightStick; } }
    public bool IsL2 { get => _target == CurveTarget.L2; set { if (value) Target = CurveTarget.L2; } }
    public bool IsR2 { get => _target == CurveTarget.R2; set { if (value) Target = CurveTarget.R2; } }

    public ResponseCurve Curve
    {
        get
        {
            var p = _get();
            return _target switch
            {
                CurveTarget.LeftStick => p.LeftStick.Curve,
                CurveTarget.RightStick => p.RightStick.Curve,
                CurveTarget.L2 => p.L2.Curve,
                _ => p.R2.Curve,
            };
        }
        set
        {
            if (Equals(Curve, value)) return;
            var t = _target;
            _update(p => t switch
            {
                CurveTarget.LeftStick => p with { LeftStick = p.LeftStick with { Curve = value } },
                CurveTarget.RightStick => p with { RightStick = p.RightStick with { Curve = value } },
                CurveTarget.L2 => p with { L2 = p.L2 with { Curve = value } },
                _ => p with { R2 = p.R2 with { Curve = value } },
            });
            Raise();
        }
    }

    /// <summary>Valeur brute actuelle de la cible (magnitude du stick ou course de gâchette), dans [0,1].</summary>
    public float CurrentInput { get => _currentInput; private set => Set(ref _currentInput, value); }

    public void OnTick(EngineSnapshot snap)
    {
        static float Mag(StickState s) => MathF.Min(1f, MathF.Sqrt(s.X * s.X + s.Y * s.Y));
        CurrentInput = _target switch
        {
            CurveTarget.LeftStick => Mag(snap.Raw.Left),
            CurveTarget.RightStick => Mag(snap.Raw.Right),
            CurveTarget.L2 => snap.Raw.L2,
            _ => snap.Raw.R2,
        };
    }

    public void Refresh() => Raise(nameof(Curve));
}
