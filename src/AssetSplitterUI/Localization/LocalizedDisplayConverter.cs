using System.Globalization;
using Avalonia.Data.Converters;

namespace AssetSplitterUI.Localization;

/// <summary>
/// Converts a bound value to a localized string using <see cref="StringResourceManager"/>.
/// Implements <see cref="IMultiValueConverter"/> so the second binding (typically
/// <c>StringResourceManager.Instance.LanguageVersion</c>) forces re-evaluation on UI language change.
/// </summary>
/// <remarks>
/// Usage with <c>MultiBinding</c>:
/// <code>
/// &lt;TextBlock&gt;
///   &lt;TextBlock.Text&gt;
///     &lt;MultiBinding Converter="{StaticResource LocalizedDisplayConverter}"&gt;
///       &lt;Binding Path="SomeProperty"/&gt;
///       &lt;Binding Path="LanguageVersion" Source="{x:Static loc:StringResourceManager.Instance}"/&gt;
///     &lt;/MultiBinding&gt;
///   &lt;/TextBlock.Text&gt;
/// &lt;/TextBlock&gt;
/// </code>
/// Set <c>ConverterParameter="theme"</c> to prefix the value with "themes.".
/// Set <c>ConverterParameter="gameLanguage"</c> to localize game language IDs.
/// </remarks>
public class LocalizedDisplayConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var value = values.Count > 0 ? values[0] as string : null;
        if (string.IsNullOrEmpty(value))
            return value ?? "";

        var key = (parameter as string) switch
        {
            "theme" => "themes." + value,
            "gameLanguage" => "gameLanguages." + value.ToLowerInvariant(),
            _ => value
        };
        return StringResourceManager.Instance.GetString(key);
    }
}
