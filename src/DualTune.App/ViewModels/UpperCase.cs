using System.Globalization;
using System.Windows.Data;

namespace DualTune.App.ViewModels;

/// <summary>Affiche un texte en majuscules (libellés du thème course).</summary>
public sealed class UpperCase : IValueConverter
{
    public static UpperCase Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString()?.ToUpper(culture) ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
