using System.Globalization;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.Services;

internal static class ConsoleOutputLocalizer
{
    // Box-drawing characters output by the backend console. These are UTF-8 multi-byte
    // sequences (e.g. 0xE2 0x94 0x80 = U+2500 "─") incorrectly decoded as Windows-1252
    // by the Process.StandardOutput reader, producing garbled Unicode strings.
    // The constants below match the garbled form so box borders can be filtered out.
    // If the stdout reader encoding changes, these constants must be updated.
    private const string BoxDrawingPrefix = "\u00E2\u201D";
    private const string BoxHorizontal = "\u00E2\u201D\u20AC";

    private static readonly string[] IndentKeys =
    [
        "consoleMessages.phase1Summary",
        "consoleMessages.phase2Instructions",
        "consoleMessages.extractionComplete",
        "consoleMessages.assetsSaved"
    ];

    private static readonly Dictionary<string, string> ExactBackendKeys = new()
    {
        ["=== PHASE 1: RDA EXPLORER ==="] = "consoleMessages.backendPhase1Label",
        ["=== PHASE 2: ASSET SPLITTER ==="] = "consoleMessages.backendPhase2Label",
        ["=== PHASE 3: ASSET EXTRACTION ==="] = "consoleMessages.backendPhase3Label",
        ["=== PHASE 3B: SPLIT TEMPLATES ==="] = "consoleMessages.backendPhase3SplitLabel",
        ["=== PHASE 4: GUID FILE INDEX ==="] = "consoleMessages.backendPhase4GuidIndexLabel",
        ["=== PHASE 5: TEMPLATE INHERITANCE (MERGE) ==="] = "consoleMessages.backendPhase5MergeLabel",
        ["=== PHASE 6: DEPENDENCY RESOLUTION ==="] = "consoleMessages.backendPhase6DepsLabel",
        ["=== PHASE 7: FORMATTING ==="] = "consoleMessages.backendPhase7FormatLabel",
        ["=== PHASE 7: ASSET MOD PACKAGES ==="] = "consoleMessages.backendPhase7Label",
        ["=== PHASE 8: ASSET MOD PACKAGES ==="] = "consoleMessages.backendPhase8ModsLabel",
        ["Extracting game data from RDA archives..."] = "consoleMessages.extractingGameData",
        ["done."] = "consoleMessages.done",
        ["Building asset name registry from game database..."] = "consoleMessages.buildingAssetRegistry",
        ["reading properties file..."] = "consoleMessages.readingPropertiesFile",
        ["Merging templates..."] = "consoleMessages.mergeTemplates",
        ["formatting assets..."] = "consoleMessages.formattingAssets",
        ["Preparing BaseAssetGUID reference pass..."] = "consoleMessages.preparingBaseAssetGuid",
        ["extracting assets..."] = "consoleMessages.extractingAssets",
        ["=== DEVELOPER ISSUE SUMMARY ==="] = "issueSummary.header"
    };

    private static readonly (string prefix, string suffix, string keyName)[] ParameterizedBackendPatterns =
    [
        ("reading language file (", ")...", "consoleMessages.readingLanguageFileWithLang"),
        ("Extracting ", " assets to XML...", "consoleMessages.extractingToXml"),
        ("Inheriting template properties for ", " assets...", "consoleMessages.inheritingTemplateProperties"),
        ("Preparing final formatting pass - scanning output directory...", " files found", "consoleMessages.preparingFormattingWithCount"),
        ("Final processing of ", " files...", "consoleMessages.finalProcessing"),
        ("Processing ", " BaseAssetGUID reference files...", "consoleMessages.processingBaseAssetGuid")
    ];

    private static readonly (string search, string resourceKey)[] ProgressReplacements =
    [
        (" - Extracting: ", "consoleMessages.extractingLabel"),
        (" - Extracting from RDA: ", "consoleMessages.extractingFromRdaLabel"),
        (" - Merging: ", "consoleMessages.mergingLabel"),
        (" - Resolving: ", "consoleMessages.resolvingLabel"),
        (" - Scanning: ", "consoleMessages.scanningLabel"),
        (" - Processing: ", "consoleMessages.processingProgress"),
        (" - Indexing: ", "consoleMessages.indexingLabel"),
        (" - Inheriting: ", "consoleMessages.inheritingLabel"),
        (" - Annotating: ", "consoleMessages.annotatingLabel"),
        (" - Creating mod: ", "consoleMessages.creatingModLabel")
    ];

    private static readonly (string search, string resourceKey)[] ProgressSuffixReplacements =
    [
        (" - Formatting....", "consoleMessages.formattingProgress"),
        (" - Formatting...", "consoleMessages.formattingProgress"),
        (" - Merging...", "consoleMessages.mergingProgress"),
        (" - Extracting...", "consoleMessages.extractingProgress")
    ];

    private static readonly (string search, string resourceKey)[] ProgressSummaryReplacements =
    [
        (" - Formatting assets", "consoleMessages.formattingAssets"),
        (" - Extracting assets", "consoleMessages.extractingAssets"),
        (" - Merging templates", "consoleMessages.mergeTemplates"),
        (" - Resolving dependencies", "consoleMessages.resolvingDependenciesSummary"),
        (" - Scanning dependencies", "consoleMessages.scanningDependenciesSummary"),
        ("Extracting from RDA", "consoleMessages.extractingFromRda"),
        ("Building GUID index", "consoleMessages.buildingGuidIndex"),
        ("Annotating template comments", "consoleMessages.annotatingTemplateComments"),
        ("Creating asset mods", "consoleMessages.creatingAssetMods")
    ];

    private static readonly (string match, string keyName)[] AutoUpdatePatterns =
    [
        ("Game templates changed - updating configuration", "consoleMessages.autoUpdateUpdating"),
        ("Skipped - template list appears custom", "consoleMessages.autoUpdateSkipped")
    ];

    private static readonly (string marker, string suffix, string keyName)[] AutoUpdateCountPatterns =
    [
        ("Updated template config with ", " templates", "consoleMessages.autoUpdateConfigUpdated"),
        ("Updated template list with ", " templates from game", "consoleMessages.autoUpdateListUpdated")
    ];

    public static string ResolveLocalized(string key, object[]? args)
    {
        string text = StringResourceManager.Instance.GetString(key);
        if (args is null || args.Length == 0)
        {
            return IndentKeys.Contains(key) ? "  " + text.TrimStart() : text;
        }

        if (args.Length == 1 && args[0] is bool isOn)
        {
            return ResolveOptionLine(text, isOn);
        }

        object[] resolvedArgs = ResolveNestedArgs(args);
        if (text.IndexOf('{') >= 0)
        {
            return ApplyLocalizedLinePrefix(key, FormatLocalizedText(text, resolvedArgs));
        }

        string resolvedText = resolvedArgs.Length == 1 ? "  " + text + " " + resolvedArgs[0] : text;
        return ApplyLocalizedLinePrefix(key, resolvedText);
    }

    private static string ApplyLocalizedLinePrefix(string key, string text) =>
        key is "issueSummary.sampleChildParent" or "issueSummary.sampleGuid" or "issueSummary.sampleFileDetail"
            ? StringResourceManager.Instance.GetString("issueSummary.sampleBullet") + text
            : text;

    private static object[] ResolveNestedArgs(object[] args)
    {
        object[] resolved = new object[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            resolved[i] = args[i] is LocalizedConsoleArgument localized
                ? ResolveLocalized(localized.Key, localized.Args)
                : args[i];
        }

        return resolved;
    }

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

    public static bool TryGetAutoUpdateLocalizationKey(string line, out string? key, out object[]? args)
    {
        key = null;
        args = null;

        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("[AUTO-UPDATE]", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var (match, keyName) in AutoUpdatePatterns)
        {
            if (line.Contains(match, StringComparison.Ordinal))
            {
                key = keyName;
                return true;
            }
        }

        foreach (var (marker, suffix, keyName) in AutoUpdateCountPatterns)
        {
            if (TryReadCountAfterMarker(line, marker, suffix, out int count))
            {
                key = keyName;
                args = [count];
                return true;
            }
        }

        return false;
    }

    public static bool TryGetBackendConsoleLocalizationKey(string line, out string? key, out object[]? args)
    {
        key = null;
        args = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string trimmedLine = line.Trim();
        if (TryGetExactBackendKey(trimmedLine, out key))
        {
            return true;
        }

        if (trimmedLine.StartsWith("Applying: XML cleanup", StringComparison.Ordinal))
        {
            key = trimmedLine.Contains(", Translations", StringComparison.Ordinal)
                ? "consoleMessages.applyingXmlCleanupWithTranslations"
                : "consoleMessages.applyingXmlCleanup";
            return true;
        }

        if (trimmedLine.Contains("[SUCCESS]", StringComparison.Ordinal)
            && trimmedLine.Contains("Asset extraction completed successfully!", StringComparison.Ordinal))
        {
            key = "consoleMessages.successExtractionComplete";
            return true;
        }

        if (TryGetIssueSummaryBackendKey(trimmedLine, out key, out args))
        {
            return true;
        }

        return TryGetParameterizedBackendKey(trimmedLine, out key, out args);
    }

    private static bool TryGetIssueSummaryBackendKey(string line, out string? key, out object[]? args)
    {
        key = null;
        args = null;

        if (line.StartsWith("[ISSUE_SUMMARY]", StringComparison.Ordinal))
        {
            string payload = line["[ISSUE_SUMMARY]".Length..].Trim();
            if (ExtractionIssueSummaryLoader.TryParseSummaryMarker("[ISSUE_SUMMARY] " + payload) is { ReportPath: { } path })
            {
                key = "issueSummary.reportPathMarker";
                args = [path];
                return true;
            }

            return false;
        }

        if (line.StartsWith("Recorded issues: ", StringComparison.Ordinal))
        {
            if (TryParseIssueCounts(line, out int warnings, out int errors))
            {
                key = "issueSummary.counts";
                args = [warnings, errors];
                return true;
            }
        }

        if (line.StartsWith("Full structured report: ", StringComparison.Ordinal))
        {
            key = "issueSummary.reportPath";
            args = [line["Full structured report: ".Length..].Trim()];
            return true;
        }

        if (line.StartsWith("  Root cause: ", StringComparison.Ordinal))
        {
            key = "issueSummary.rootCause";
            args = [line["  Root cause: ".Length..].Trim()];
            return true;
        }

        if (line.StartsWith("  Hint: ", StringComparison.Ordinal))
        {
            key = "issueSummary.hint";
            args = [line["  Hint: ".Length..].Trim()];
            return true;
        }

        if (line.StartsWith("  … and ", StringComparison.Ordinal) && line.Contains(" more in the JSON report", StringComparison.Ordinal))
        {
            string middle = line["  … and ".Length..];
            int space = middle.IndexOf(' ');
            if (space > 0 && int.TryParse(middle[..space], NumberStyles.Integer, CultureInfo.InvariantCulture, out int more))
            {
                key = "issueSummary.moreInReport";
                args = [more];
                return true;
            }
        }

        if (line.StartsWith("[", StringComparison.Ordinal) && line.Contains("] ", StringComparison.Ordinal) && line.Contains("occurrence", StringComparison.Ordinal))
        {
            int close = line.IndexOf(']');
            if (close > 1)
            {
                string code = line[1..close];
                string rest = line[(close + 1)..].Trim();
                if (rest.StartsWith("occurrence", StringComparison.Ordinal))
                {
                    string countPart = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                    if (int.TryParse(countPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                    {
                        key = "issueSummary.groupHeader";
                        args = [IssueSummaryLocalizer.GetGroupTitle(code), count];
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryParseIssueCounts(string line, out int warnings, out int errors)
    {
        warnings = 0;
        errors = 0;

        const string prefix = "Recorded issues: ";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string rest = line[prefix.Length..];
        int comma = rest.IndexOf(',');
        if (comma < 0)
        {
            return false;
        }

        string warnPart = rest[..comma].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        string errPart = rest[(comma + 1)..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return int.TryParse(warnPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out warnings)
            && int.TryParse(errPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out errors);
    }

    public static bool TryResolvePhaseHeader(string line, out string? localizedTitle)
    {
        localizedTitle = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string trimmed = line.Trim();
        if (!trimmed.StartsWith("=== PHASE", StringComparison.Ordinal))
        {
            return false;
        }

        if (TryGetExactBackendKey(trimmed, out string? key))
        {
            localizedTitle = ResolveLocalized(key!, null);
            return true;
        }

        localizedTitle = trimmed;
        return true;
    }

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

    private static string ResolveOptionLine(string text, bool isOn)
    {
        if (isOn)
        {
            return "  " + text;
        }

        string label = StripLeadingSymbol(text);
        return "  \u25CB " + label + " " + StringResourceManager.Instance.GetString("consoleMessages.optionOff");
    }

    private static string StripLeadingSymbol(string text)
    {
        int firstSpace = text.IndexOf(' ');
        if (firstSpace is > 0 and <= 4 && !char.IsLetterOrDigit(text[0]))
        {
            return text[(firstSpace + 1)..];
        }

        return text;
    }

    private static string FormatLocalizedText(string text, object[] args)
    {
        try
        {
            return string.Format(CultureInfo.InvariantCulture, text, args);
        }
        catch (FormatException)
        {
            string result = text;
            for (int i = 0; i < args.Length; i++)
            {
                result = result.Replace("{" + i + "}", args[i]?.ToString() ?? "", StringComparison.Ordinal);
            }

            return result;
        }
    }

    private static bool TryReadCountAfterMarker(string line, string marker, string suffix, out int count)
    {
        count = 0;
        int markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0 || !line.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        int valueStart = markerIndex + marker.Length;
        string countText = line[valueStart..^suffix.Length].Trim();
        return int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
    }

    private static bool TryGetExactBackendKey(string line, out string? key)
    {
        if (ExactBackendKeys.TryGetValue(line, out key))
        {
            return true;
        }

        if (line.Contains("extracting from RDA file(s)", StringComparison.Ordinal)
            && line.Contains("done", StringComparison.Ordinal))
        {
            key = "consoleMessages.extractingFromRda";
            return true;
        }

        return false;
    }

    private static bool TryGetParameterizedBackendKey(string line, out string? key, out object[]? args)
    {
        foreach (var (prefix, suffix, keyName) in ParameterizedBackendPatterns)
        {
            if (TryReadMiddleValue(line, prefix, suffix, out string value))
            {
                key = keyName;
                args = [value];
                return true;
            }
        }

        key = null;
        args = null;
        return false;
    }

    private static bool TryReadMiddleValue(string line, string prefix, string suffix, out string value)
    {
        value = "";
        if (!line.StartsWith(prefix, StringComparison.Ordinal) || !line.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        value = line[prefix.Length..^suffix.Length].Trim();
        return value.Length > 0;
    }

    private static string TrimProgressSummary(string text)
    {
        string trimmed = text.Trim();
        while (trimmed.EndsWith(".", StringComparison.Ordinal) || trimmed.EndsWith("。", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^1].TrimEnd();
        }

        return trimmed;
    }
}

internal sealed record LocalizedConsoleArgument(string Key, object[]? Args = null);
