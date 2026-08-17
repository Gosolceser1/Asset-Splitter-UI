using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AssetSplitterUI.Localization;

/// <summary>
/// Converts a hex color string (e.g. <c>"#26A69A"</c>) to a <see cref="SolidColorBrush"/>.
/// Falls back to the accent teal when the value is null, empty, or unparseable.
/// </summary>
public class StringToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush FallbackBrush = new(Color.Parse("#26A69A"));

    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string { Length: > 0 } hex)
        {
            try { return new SolidColorBrush(Color.Parse(hex)); }
            catch (FormatException) { }
        }
        return FallbackBrush;
    }

    /// <summary>Not supported; one-way binding only.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException("StringToBrushConverter is one-way only.");
}
