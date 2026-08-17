using System.Globalization;
using System.Text.RegularExpressions;
using AssetSplitterUI.Localization;

namespace AssetSplitterUI.Services;

internal static partial class ConsoleOutputLocalizer
{
    public static string LocalizeProgressLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line;
        }

        // Never mangle raw debug output
        if (line.Contains("[DEBUG]", StringComparison.OrdinalIgnoreCase))
        {
            return line;
        }

        // Keep the "[xx.x%] [count/total] - " prefix — regular users want to see % and counts.
        // Translation of operation labels (Extracting → Извлечение, etc.) still happens below.

        StringResourceManager strings = StringResourceManager.Instance;
        foreach (var (search, resourceKey) in ProgressReplacements)
        {
            line = line.Replace(search, " - " + ResolveProgressLabel(strings, resourceKey, search) + " ", StringComparison.Ordinal);
        }

        foreach (var (search, resourceKey) in ProgressSuffixReplacements)
        {
            line = line.Replace(search, " - " + ResolveProgressLabel(strings, resourceKey, search), StringComparison.Ordinal);
        }

        foreach (var (search, resourceKey) in ProgressSummaryReplacements)
        {
            line = line.Replace(search, " - " + TrimProgressSummary(ResolveProgressLabel(strings, resourceKey, search)), StringComparison.Ordinal);
        }

        return line;
    }

    /// <summary>Returns UI string for a progress label, or <paramref name="fallbackEnglish"/> when the key is missing.</summary>
    private static string ResolveProgressLabel(StringResourceManager strings, string resourceKey, string fallbackEnglish)
    {
        string localized = strings.GetString(resourceKey);
        return localized == resourceKey || localized.StartsWith("consoleMessages.", StringComparison.Ordinal)
            ? fallbackEnglish
            : localized;
    }
}
