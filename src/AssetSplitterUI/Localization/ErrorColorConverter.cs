using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.Localization;

/// <summary>
/// Converts a boolean error state to a <see cref="SolidColorBrush"/>.
/// <see langword="true"/> -> error red; <see langword="false"/> or non-bool -> theme accent.
/// </summary>
public class ErrorColorConverter : IValueConverter
{
    private static readonly SolidColorBrush DarkErrorBrush = new(Color.Parse("#F16D6D"));
    private static readonly SolidColorBrush DarkAccentBrush = new(Color.Parse("#D4A843"));
    private static readonly SolidColorBrush LightErrorBrush = new(Color.Parse("#B82828"));
    private static readonly SolidColorBrush LightAccentBrush = new(Color.Parse("#9D6F1F"));

    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isLight = ApplicationThemeService.IsLightTheme;
        return value is true
            ? isLight ? LightErrorBrush : DarkErrorBrush
            : isLight ? LightAccentBrush : DarkAccentBrush;
    }

    /// <summary>Not supported; one-way binding only.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException("ErrorColorConverter is one-way only.");
}
