using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace DualTune.App.Localization;

/// <summary>
/// <c>{l:Tr clé}</c> : texte traduit, mis à jour en direct quand la langue change
/// (liaison à <see cref="Loc.Instance"/>[clé]).
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class Tr : MarkupExtension
{
    public Tr() { }

    public Tr(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
        // Dans un Setter de style ou de déclencheur, la liaison elle-même est la valeur attendue.
        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget { TargetObject: not DependencyObject })
            return binding;
        return binding.ProvideValue(serviceProvider);
    }
}
