using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace AssetSplitterUI.Localization;

/// <summary>
/// Markup extension that creates a passthrough one-way binding with a converter.
/// Usage: <c>{loc:Localize app.title}</c> or <c>{loc:Localize Key=app.title}</c>
/// The binding has no explicit Source or Path — it inherits from DataContext.
/// When the code-behind resets DataContext on language change, all bindings re-evaluate
/// and the converter calls <see cref="StringResourceManager.GetString"/> with the new language.
/// </summary>
public class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; } = "";

    public LocalizeExtension() { }
    public LocalizeExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // Bind to the LanguageVersion property on the manager.
        // When it changes, Avalonia will re-evaluate this binding and call the converter again.
        return new Binding(nameof(StringResourceManager.LanguageVersion))
        {
            Source = StringResourceManager.Instance,
            Converter = new LocConverter(Key),
            Mode = BindingMode.OneWay
        };
    }

    private sealed class LocConverter(string key) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            StringResourceManager.Instance.GetString(key);

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
