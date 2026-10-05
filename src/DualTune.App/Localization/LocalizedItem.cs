using System.ComponentModel;

namespace DualTune.App.Localization;

/// <summary>Élément de liste : une valeur et son libellé traduit, qui suit la langue.</summary>
public sealed class LocalizedItem<T> : INotifyPropertyChanged
{
    private readonly string _key;

    public LocalizedItem(T value, string key)
    {
        Value = value;
        _key = key;
        Loc.Instance.LanguageChanged += () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public T Value { get; }

    /// <summary>Raccourci pour les préréglages de courbe (valeur = nom interne).</summary>
    public T Name => Value;

    public string Label => Loc.T(_key);

    public override string ToString() => Label;
}
