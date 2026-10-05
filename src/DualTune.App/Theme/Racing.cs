using System.Windows;

namespace DualTune.App.Theme;

/// <summary>Propriétés attachées du thème course.</summary>
public static class Racing
{
    /// <summary>Résumé affiché à droite du bandeau d'une section (visible surtout quand elle est repliée).</summary>
    public static readonly DependencyProperty SummaryProperty = DependencyProperty.RegisterAttached(
        "Summary", typeof(string), typeof(Racing), new PropertyMetadata(null));

    public static string? GetSummary(DependencyObject d) => (string?)d.GetValue(SummaryProperty);

    public static void SetSummary(DependencyObject d, string? value) => d.SetValue(SummaryProperty, value);
}
