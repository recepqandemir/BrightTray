using System;
using System.Windows.Markup;
using BrightTray.Services;

namespace BrightTray.UI;

/// <summary>XAML içinde kullanım: <c>Text="{ui:Loc Brightness}"</c></summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
