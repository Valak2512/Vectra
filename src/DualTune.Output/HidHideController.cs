using Nefarius.Drivers.HidHide;

namespace DualTune.Output;

public interface IDeviceHider
{
    bool IsAvailable { get; }
    string? Error { get; }
    /// <summary>Une manette est actuellement masquée (par Vectra ou un autre logiciel).</summary>
    bool IsHiding { get; }
    bool RegisterApp(string appExePath);
    bool Hide(string instancePath, string appExePath);
    void Unhide();
    void RecoverFromCrash();
}

/// <summary>Opérations HidHide utilisées par DualTune (abstraction testable du pilote).</summary>
public interface IHidHideDriver
{
    bool IsInstalled { get; }
    bool IsActive { get; set; }
    void AddApplicationPath(string path);
    /// <returns>true si l'instance a été ajoutée, false si elle était déjà masquée.</returns>
    bool AddBlockedInstanceId(string instanceId);
    void RemoveBlockedInstanceId(string instanceId);
}

/// <summary>
/// Masque la vraie manette aux jeux (spec §8). Règle de sûreté : on ne masque jamais si DualTune n'a pas pu
/// s'autoriser lui-même, et un fichier témoin permet de démasquer après un plantage.
/// </summary>
public sealed class HidHideController : IDeviceHider
{
    private readonly IHidHideDriver? _driver;
    private readonly string _markerPath;
    private string? _hidden;   // masquage posé par DualTune (à retirer à la fermeture)
    private string? _current;  // manette actuellement masquée, par nous ou par un autre logiciel

    public HidHideController(IHidHideDriver? driver, string stateDir)
    {
        _driver = driver;
        _markerPath = Path.Combine(stateDir, "hidden.txt");
    }

    public string? Error { get; private set; }

    public bool IsHiding => _current is not null;

    public bool IsAvailable
    {
        get
        {
            try { return _driver?.IsInstalled == true; }
            catch (Exception e) { Error = e.Message; return false; }
        }
    }

    /// <summary>
    /// Autorise DualTune à voir les manettes masquées. À faire dès le démarrage : si un autre logiciel
    /// masque déjà la manette, DualTune ne la trouverait jamais sinon.
    /// </summary>
    public bool RegisterApp(string appExePath)
    {
        if (!IsAvailable) return false;
        try
        {
            _driver!.AddApplicationPath(appExePath);
            return true;
        }
        catch (Exception e)
        {
            Error = $"Vectra n'a pas pu s'autoriser dans HidHide : {e.Message}";
            return false;
        }
    }

    public bool Hide(string instancePath, string appExePath)
    {
        if (!IsAvailable) return false;
        if (string.Equals(_current, instancePath, StringComparison.OrdinalIgnoreCase)) return true;
        Unhide();
        if (!RegisterApp(appExePath)) return false;

        try
        {
            // Si un autre logiciel masquait déjà cette manette, ce masquage n'est pas le nôtre : on n'y touchera pas.
            bool added = _driver!.AddBlockedInstanceId(instancePath);
            _current = instancePath;
            if (added)
            {
                _hidden = instancePath;
                Directory.CreateDirectory(Path.GetDirectoryName(_markerPath)!);
                File.WriteAllText(_markerPath, instancePath);
            }
            _driver.IsActive = true;
            Error = null;
            return true;
        }
        catch (Exception e)
        {
            Error = $"Masquage impossible : {e.Message}";
            return false;
        }
    }

    public void Unhide()
    {
        _current = null;
        if (_hidden is null || _driver is null) return;
        try
        {
            _driver.RemoveBlockedInstanceId(_hidden);
            _hidden = null;
            File.Delete(_markerPath);
        }
        catch (Exception e)
        {
            Error = $"Démasquage impossible : {e.Message}";
        }
    }

    public void RecoverFromCrash()
    {
        if (_driver is null || !File.Exists(_markerPath)) return;
        try
        {
            var id = File.ReadAllText(_markerPath).Trim();
            if (id.Length > 0) _driver.RemoveBlockedInstanceId(id);
            File.Delete(_markerPath);
        }
        catch (Exception e)
        {
            Error = $"Récupération après plantage impossible : {e.Message}";
        }
    }
}

/// <summary>Adaptateur vers le pilote HidHide réel (Nefarius.Drivers.HidHide).</summary>
public sealed class NefariusHidHideDriver : IHidHideDriver
{
    private readonly HidHideControlService _svc = new();

    public bool IsInstalled => _svc.IsInstalled;

    public bool IsActive
    {
        get => _svc.IsActive;
        set => _svc.IsActive = value;
    }

    public void AddApplicationPath(string path)
    {
        if (_svc.IsAppListInverted)
            throw new InvalidOperationException("la liste d'applications HidHide est en mode inversé");
        if (!_svc.ApplicationPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            _svc.AddApplicationPath(path, throwIfInvalid: true);
    }

    public bool AddBlockedInstanceId(string instanceId)
    {
        if (_svc.BlockedInstanceIds.Contains(instanceId, StringComparer.OrdinalIgnoreCase)) return false;
        _svc.AddBlockedInstanceId(instanceId);
        return true;
    }

    public void RemoveBlockedInstanceId(string instanceId) => _svc.RemoveBlockedInstanceId(instanceId);
}
