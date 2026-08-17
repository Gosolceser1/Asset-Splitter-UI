using System.Globalization;
using System.Text.RegularExpressions;
using AssetSplitterUI.Localization;

namespace AssetSplitterUI.Services;

internal static partial class ConsoleOutputLocalizer
{
    public static string? FilterLine(string message) => FilterLine(message, debugMode: false);

    public static string? FilterLine(string message, bool debugMode)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        if (!debugMode && IsInternalBackendMarker(message))
        {
            return null;
        }

        // Always keep structured I18N markers so the console coordinator can resolve consoleMessages.* keys
        // (including couldNotMoveToTemplateFolder). Do not strip them in debug mode either.
        if (message.TrimStart().StartsWith("[I18N] ", StringComparison.Ordinal))
        {
            return message;
        }

        if (debugMode)
        {
            if (message.StartsWith("[PLAN]", StringComparison.Ordinal)
                || message.StartsWith("=== PHASE", StringComparison.Ordinal)
                || message.StartsWith("[ISSUE_SUMMARY]", StringComparison.Ordinal))
            {
                return message;
            }
        }

        if (message.Contains("[DEBUG]", StringComparison.OrdinalIgnoreCase))
        {
            if (message.StartsWith("[DEBUG] args[", StringComparison.Ordinal)
                || message.StartsWith("[DEBUG] Total arguments:", StringComparison.Ordinal)
                || message.Contains("[DEBUG] Flags applied:", StringComparison.Ordinal)
                || message.Contains("singleGuidFilter='' length=0", StringComparison.Ordinal))
            {
                return null;
            }

            return message;
        }

        if (!debugMode && message.Contains("Full inheritance applied to:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!debugMode && message.Contains("Resolved dependencies for:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (message.Contains("[RDA COMPLETE]")
            || message.Contains("ANNO ASSET SPLITTER v2.0")
            || message.Contains("Asset Extraction & ModOp Generator")
            || message.Contains("Select language and run again")
            || message.Contains("Configure options in the GUI"))
        {
            return null;
        }

        string trimmed = message.TrimStart();
        if (IsBoxDrawingBorder(trimmed))
        {
            return null;
        }

        if (message.Contains(BoxHorizontal, StringComparison.Ordinal) && message.Length < 70)
        {
            return null;
        }

        string trimmedMsg = message.Trim();
        if (trimmedMsg.Length > 3 && trimmedMsg.All(c => c is '\u2500' or '\u2501' or '\u2550'))
        {
            return null;
        }

        return message;
    }

    private static bool IsInternalBackendMarker(string message)
    {
        string trimmed = message.TrimStart();
        return trimmed.StartsWith("[PLAN]", StringComparison.Ordinal)
            || trimmed.StartsWith("[PLAN+]", StringComparison.Ordinal)
            || trimmed.StartsWith("[PHASE1_ONLY]", StringComparison.Ordinal)
            || trimmed.StartsWith("[ISSUE_SUMMARY]", StringComparison.Ordinal);
    }

    private static bool IsBoxDrawingBorder(string trimmed)
    {
        return trimmed.Length >= 3
          && (IsGarbledBoxDrawing(trimmed) || IsUtf8BoxDrawing(trimmed));
    }

    private static bool IsGarbledBoxDrawing(string trimmed)
    {
        return trimmed.StartsWith(BoxDrawingPrefix, StringComparison.Ordinal)
          && trimmed[2] is '\u0152' or '\u201A' or '\u201D' or '\u0153';
    }

    private static bool IsUtf8BoxDrawing(string trimmed)
    {
        char first = trimmed[0];
        return first is '\u250C' or '\u2510' or '\u2514' or '\u2518' or '\u251C' or '\u2524'
                    or '\u252C' or '\u2534' or '\u2502' or '\u2500' or '\u2550' or '\u2551'
                    or '\u2554' or '\u2557' or '\u255A' or '\u255D' or '\u2560' or '\u2563'
                    or '\u2566' or '\u2569';
    }
}
