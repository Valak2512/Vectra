using System.Windows.Input;
using DualTune.App.Engine;
using DualTune.Core;

namespace DualTune.App.ViewModels;

/// <summary>Réglages et affichage d'une gâchette (onglet Gâchettes).</summary>
public sealed class TriggerViewModel : ViewModelBase
{
    private readonly Func<Profile> _get;
    private readonly Action<Func<Profile, Profile>> _update;
    private readonly bool _isL2;
    private double _rawValue;
    private double _outputValue;

    public TriggerViewModel(Func<Profile> get, Action<Func<Profile, Profile>> update, bool isL2)
    {
        _get = get;
        _update = update;
        _isL2 = isL2;
        ResetCommand = new RelayCommand(() => Edit(_ => new TriggerSettings()));
    }

    public string Title => _isL2 ? "L2" : "R2";

    private TriggerSettings Settings => _isL2 ? _get().L2 : _get().R2;

    public float InnerDeadzone { get => Settings.InnerDeadzone; set => Edit(s => s with { InnerDeadzone = value }); }
    public float OuterDeadzone { get => Settings.OuterDeadzone; set => Edit(s => s with { OuterDeadzone = value }); }
    public float AntiDeadzone { get => Settings.AntiDeadzone; set => Edit(s => s with { AntiDeadzone = value }); }

    /// <summary>Résumé du bandeau de section : zone morte · zone extérieure · anti-deadzone.</summary>
    public string Summary => $"{Pct(InnerDeadzone)} · {Pct(OuterDeadzone)} · {Pct(AntiDeadzone)}";

    private static string Pct(float v) => (v * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";

    /// <summary>Course brute, 0 à 1.</summary>
    public double RawValue { get => _rawValue; private set => Set(ref _rawValue, value); }

    /// <summary>Valeur envoyée au jeu, 0 à 1.</summary>
    public double OutputValue { get => _outputValue; private set => Set(ref _outputValue, value); }

    public ICommand ResetCommand { get; }

    public void OnTick(EngineSnapshot snap)
    {
        RawValue = _isL2 ? snap.Raw.L2 : snap.Raw.R2;
        OutputValue = (_isL2 ? snap.Output.LT : snap.Output.RT) / 255.0;
    }

    public void Refresh() => RaiseAll();

    private void Edit(Func<TriggerSettings, TriggerSettings> f)
    {
        _update(p => _isL2 ? p with { L2 = f(p.L2) } : p with { R2 = f(p.R2) });
        RaiseAll();
    }
}
