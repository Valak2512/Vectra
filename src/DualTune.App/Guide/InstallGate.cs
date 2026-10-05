namespace DualTune.App.Guide;

/// <summary>
/// Installation de pilote en cours, partagée par toutes les fenêtres du guide : refermer puis rouvrir le guide
/// pendant une installation ne permet pas d'en lancer une seconde.
/// </summary>
public sealed class InstallGate
{
    /// <summary>Étape du guide dont le pilote s'installe, ou null.</summary>
    public int? Step { get; private set; }

    public event Action? Changed;

    public bool TryEnter(int step)
    {
        if (Step is not null) return false;
        Step = step;
        Changed?.Invoke();
        return true;
    }

    public void Exit()
    {
        Step = null;
        Changed?.Invoke();
    }
}
