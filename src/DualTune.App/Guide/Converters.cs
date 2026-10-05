using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DualTune.App.Guide;

/// <summary>Visible si la valeur (int) égale le paramètre (index d'étape) ; « !n » : visible si différente.</summary>
public sealed class StepVisibility : IValueConverter
{
    public static StepVisibility Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string arg = parameter?.ToString() ?? "";
        bool negate = arg.StartsWith('!');
        bool equal = value is int i && int.TryParse(negate ? arg[1..] : arg, out int p) && i == p;
        return equal != negate ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Booléen → Visibility ; paramètre « invert » pour l'inverse.</summary>
public sealed class BoolVisibility : IValueConverter
{
    public static BoolVisibility Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool v && v;
        if (parameter as string == "invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Visible si la chaîne n'est pas vide (paramètre « invert » : si elle est vide).</summary>
public sealed class TextVisibility : IValueConverter
{
    public static TextVisibility Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool has = !string.IsNullOrEmpty(value as string);
        if (parameter as string == "invert") has = !has;
        return has ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
