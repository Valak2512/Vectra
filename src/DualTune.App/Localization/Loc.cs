using System.ComponentModel;
using System.Globalization;

namespace DualTune.App.Localization;

/// <summary>Langue de l'interface (français ou anglais) et accès aux textes traduits.</summary>
public sealed class Loc : INotifyPropertyChanged
{
    public const string French = "fr", English = "en";

    /// <summary>Instance de l'application, utilisée par les vues et les view-models.</summary>
    public static Loc Instance { get; } = new(French);

    private string _language;

    public Loc(string language) => _language = Normalize(language) ?? French;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Après un changement de langue, pour les textes composés par les view-models.</summary>
    public event Action? LanguageChanged;

    public string Language
    {
        get => _language;
        set
        {
            var lang = Normalize(value) ?? French;
            if (lang == _language) return;
            _language = lang;
            Raise("Item[]"); // rafraîchit toutes les liaisons {l:Tr …}
            Raise(nameof(Language));
            Raise(nameof(IsFrench));
            Raise(nameof(IsEnglish));
            LanguageChanged?.Invoke();
        }
    }

    // Propriétés « bouton radio » pour le sélecteur FRANÇAIS / ENGLISH.
    public bool IsFrench { get => _language == French; set { if (value) Language = French; } }
    public bool IsEnglish { get => _language == English; set { if (value) Language = English; } }

    public string this[string key] =>
        Strings.All.TryGetValue(key, out var t) ? (_language == English ? t.En : t.Fr) : $"‹{key}›";

    public string Format(string key, params object?[] args) => string.Format(CultureInfo.InvariantCulture, this[key], args);

    /// <summary>Texte traduit dans la langue de l'application.</summary>
    public static string T(string key) => Instance[key];

    /// <summary>Texte traduit avec paramètres {0}, {1}…</summary>
    public static string F(string key, params object?[] args) => Instance.Format(key, args);

    /// <summary>Langue au démarrage : le choix enregistré, sinon celle de Windows (français ou, à défaut, anglais).</summary>
    public static string Resolve(string? saved, CultureInfo windowsUi) =>
        Normalize(saved) ?? (windowsUi.TwoLetterISOLanguageName == French ? French : English);

    private static string? Normalize(string? lang) => lang is French or English ? lang : null;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
