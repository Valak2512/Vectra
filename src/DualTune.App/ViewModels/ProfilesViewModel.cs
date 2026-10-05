using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Input;
using DualTune.App.Localization;
using DualTune.Core;

namespace DualTune.App.ViewModels;

/// <summary>Liste des profils : sélection, création, duplication, renommage, suppression (spec §9).</summary>
public sealed class ProfilesViewModel : ViewModelBase
{
    private readonly ProfileStore _store;
    private readonly Action<Profile> _activate;
    private readonly Func<string, string?> _askName;
    private readonly Func<string, bool> _confirm;
    private readonly Func<string, string?> _pickSavePath;
    private readonly Func<string?> _pickOpenPath;
    private readonly Dictionary<string, Profile> _profiles;
    private string _selected;
    private string? _error;

    /// <param name="askName">Demande un nom (valeur proposée en paramètre) ; null si annulé.</param>
    /// <param name="confirm">Pose une question oui/non.</param>
    /// <param name="pickSavePath">Demande où exporter (nom de fichier proposé) ; null si annulé.</param>
    /// <param name="pickOpenPath">Demande le fichier à importer ; null si annulé.</param>
    public ProfilesViewModel(ProfileStore store, IReadOnlyList<Profile> profiles, string activeName,
        Action<Profile> activate, Func<string, string?> askName, Func<string, bool> confirm,
        Func<string, string?>? pickSavePath = null, Func<string?>? pickOpenPath = null)
    {
        _pickSavePath = pickSavePath ?? (_ => null);
        _pickOpenPath = pickOpenPath ?? (() => null);
        _store = store;
        _activate = activate;
        _askName = askName;
        _confirm = confirm;
        _profiles = profiles.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        Names = new ObservableCollection<string>(profiles.Select(p => p.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase));
        _selected = activeName;

        NewCommand = new RelayCommand(() => Create(_askName(Loc.T("profiles.newName")), Profile.CreateDefault()));
        DuplicateCommand = new RelayCommand(() => Create(_askName(Loc.F("profiles.copyName", _selected)), _profiles[_selected]));
        RenameCommand = new RelayCommand(Rename);
        DeleteCommand = new RelayCommand(_ => Delete(), _ => Names.Count > 1);
        ExportCommand = new RelayCommand(Export);
        ImportCommand = new RelayCommand(Import);
    }

    public ObservableCollection<string> Names { get; }

    public string? Error { get => _error; private set => Set(ref _error, value); }

    public ICommand NewCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }

    public string Selected
    {
        get => _selected;
        set
        {
            if (value is null || !_profiles.TryGetValue(value, out var p) || value == _selected) return;
            _selected = value;
            Error = null;
            _activate(p);
            Raise();
        }
    }

    /// <summary>Le profil actif a été modifié : garder la dernière version en mémoire.</summary>
    public void OnActiveChanged(Profile p) => _profiles[p.Name] = p;

    private void Create(string? rawName, Profile template)
    {
        if (!TryValidate(rawName, out var name)) return;
        var p = template with { Name = name };
        _store.Save(p);
        _profiles[name] = p;
        Names.Add(name);
        Selected = name;
    }

    private void Rename()
    {
        var old = _selected;
        if (!TryValidate(_askName(old), out var name, allowSameAs: old)) return;
        var p = _profiles[old] with { Name = name };
        _store.Delete(old);
        _store.Save(p);
        _profiles.Remove(old);
        _profiles[name] = p;
        Names[Names.IndexOf(old)] = name;
        _selected = name;
        _activate(p);
        Raise(nameof(Selected));
    }

    private void Delete()
    {
        if (Names.Count <= 1) return;
        var victim = _selected;
        if (!_confirm(Loc.F("profiles.confirmDelete", victim))) return;
        _store.Delete(victim);
        _profiles.Remove(victim);
        Names.Remove(victim);
        _selected = "";
        Selected = Names[0];
    }

    private void Export()
    {
        var invalid = Path.GetInvalidFileNameChars();
        var fileName = new string(_selected.Select(c => invalid.Contains(c) ? '_' : c).ToArray()) + ".json";
        if (_pickSavePath(fileName) is not { } path) return;
        try
        {
            _store.Export(_profiles[_selected], path);
            Error = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Error = Loc.F("profiles.exportFailed", e.Message);
        }
    }

    private void Import()
    {
        if (_pickOpenPath() is not { } path) return;
        Profile p;
        try
        {
            p = _store.Import(path);
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Error = Loc.F("profiles.importFailed", e.Message);
            return;
        }

        var name = p.Name.Trim();
        for (int i = 2; _profiles.ContainsKey(name); i++) name = $"{p.Name.Trim()} ({i})";
        Error = null;
        Create(name, p);
    }

    private bool TryValidate(string? raw, out string name, string? allowSameAs = null)
    {
        name = raw?.Trim() ?? "";
        if (raw is null) return false; // annulé
        if (name.Length == 0)
        {
            Error = Loc.T("profiles.emptyName");
            return false;
        }
        if (_profiles.ContainsKey(name) && !string.Equals(name, allowSameAs, StringComparison.OrdinalIgnoreCase))
        {
            Error = Loc.F("profiles.exists", name);
            return false;
        }
        Error = null;
        return true;
    }
}
