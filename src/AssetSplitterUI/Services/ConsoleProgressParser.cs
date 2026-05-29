using System.Globalization;

namespace AssetSplitterUI.Services;

internal static class ConsoleProgressParser
{
    public static bool TryParsePercent(string outputLine, out double percent)
    {
        percent = 0;
        if (string.IsNullOrWhiteSpace(outputLine))
            return false;

        int percentEnd = outputLine.LastIndexOf('%');
        if (percentEnd <= 0)
            return false;

        // Prefer '[' (the format used by AssetProcessorProgressReporter: [ 45.0%])
        int percentStart = outputLine.LastIndexOf('[', percentEnd);
        if (percentStart < 0 || percentStart >= percentEnd)
        {
            percentStart = outputLine.LastIndexOf('(', percentEnd);
        }

        if (percentStart < 0 || percentStart >= percentEnd)
            return false;

        string percentText = outputLine[(percentStart + 1)..percentEnd].Trim();
        return double.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out percent);
    }
}
