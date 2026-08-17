using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using AssetSplitterUI.Localization;

namespace AssetSplitterUI.Services;

internal static partial class ConsoleOutputLocalizer
{
    public static bool TryGetAutoUpdateLocalizationKey(string line, [NotNullWhen(true)] out string? key, out object[]? args)
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

    public static bool TryGetBackendConsoleLocalizationKey(string line, [NotNullWhen(true)] out string? key, out object[]? args)
    {
        key = null;
        args = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string trimmedLine = line.Trim();
        if (TryGetExactBackendKey(trimmedLine, out key)
            || TryGetExtractionSummaryKey(trimmedLine, out key, out args))
        {
            return true;
        }

        trimmedLine = StripBackendLogPrefix(trimmedLine);
        if (TryGetExactBackendKey(trimmedLine, out key)
            || TryGetExtractionSummaryKey(trimmedLine, out key, out args))
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

        if (TryGetIssueSummaryBackendKey(trimmedLine, out key, out args))
        {
            return true;
        }

        if (TryParseFormattingRunSummary(trimmedLine, out args))
        {
            key = "consoleMessages.formattingRunSummary";
            return true;
        }

        if (TryParseOutputCacheMessage(trimmedLine, out key, out args))
        {
            return true;
        }

        return TryGetParameterizedBackendKey(trimmedLine, out key, out args);
    }

    private static bool TryParseOutputCacheMessage(string line, [NotNullWhen(true)] out string? key, out object[]? args)
    {
        key = null;
        args = null;

        Match complete = OutputCacheCompleteRegex.Match(line);
        if (complete.Success)
        {
            key = "consoleMessages.outputCacheComplete";
            args = [complete.Groups[1].Value, complete.Groups[2].Value];
            return true;
        }

        Match repair = OutputCacheRepairRegex.Match(line);
        if (repair.Success)
        {
            key = "consoleMessages.outputCacheRepair";
            args = [repair.Groups[1].Value];
            return true;
        }

        Match full = OutputCacheFullRebuildRegex.Match(line);
        if (full.Success)
        {
            key = "consoleMessages.outputCacheFullRebuild";
            args = [full.Groups[1].Value];
            return true;
        }

        Match gameDiff = OutputCacheGameDiffRegex.Match(line);
        if (gameDiff.Success)
        {
            key = "consoleMessages.outputCacheGameDiff";
            args =
            [
                gameDiff.Groups[1].Value,
                gameDiff.Groups[2].Value,
                gameDiff.Groups[3].Value,
                gameDiff.Groups[4].Value,
            ];
            return true;
        }

        Match sourceDiff = OutputCacheSourceDiffRegex.Match(line);
        if (sourceDiff.Success)
        {
            key = "consoleMessages.outputCacheSourceDiff";
            args =
            [
                sourceDiff.Groups[1].Value,
                sourceDiff.Groups[2].Value,
                sourceDiff.Groups[3].Value,
                sourceDiff.Groups[4].Value,
            ];
            return true;
        }

        Match hashBackfillDiff = OutputCacheHashBackfillDiffRegex.Match(line);
        if (hashBackfillDiff.Success)
        {
            key = "consoleMessages.outputCacheHashBackfillDiff";
            args =
            [
                hashBackfillDiff.Groups[1].Value,
                hashBackfillDiff.Groups[2].Value,
                hashBackfillDiff.Groups[3].Value,
                hashBackfillDiff.Groups[4].Value,
            ];
            return true;
        }

        Match sourceCorrupt = SourceXmlCacheCorruptRegex.Match(line);
        if (sourceCorrupt.Success)
        {
            key = "consoleMessages.sourceXmlCacheCorrupt";
            // Message format: ({1}=file) (build: {0}) — regex groups are file then build.
            args = [sourceCorrupt.Groups[2].Value, sourceCorrupt.Groups[1].Value];
            return true;
        }

        return false;
    }

    private static bool TryParseFormattingRunSummary(string line, out object[]? args)
    {
        args = null;
        Match match = FormattingRunSummaryRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        args =
        [
            match.Groups[1].Value,
            match.Groups[2].Value,
            match.Groups[3].Value,
            match.Groups[4].Value
        ];
        return true;
    }

    private static bool TryGetIssueSummaryBackendKey(string line, [NotNullWhen(true)] out string? key, out object[]? args)
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

    public static bool TryResolvePhaseHeader(string line, [NotNullWhen(true)] out string? localizedTitle)
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
            localizedTitle = ResolveLocalized(key, null);
            return true;
        }

        localizedTitle = trimmed;
        return true;
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

    private static bool TryGetExtractionSummaryKey(string line, [NotNullWhen(true)] out string? key, out object[]? args)
    {
        key = null;
        args = null;

        if (line.Length == 0)
        {
            return false;
        }

        if (line.Equals(BackendConsoleMilestonePatterns.ExtractionCompleteHeader, StringComparison.Ordinal))
        {
            key = "consoleMessages.extractionCompleteHeader";
            return true;
        }

        if (line.StartsWith(BackendConsoleMilestonePatterns.ProcessedAssetsPrefix, StringComparison.OrdinalIgnoreCase)
            && line.EndsWith(BackendConsoleMilestonePatterns.ProcessedAssetsSuffix, StringComparison.OrdinalIgnoreCase))
        {
            string count = line[
                BackendConsoleMilestonePatterns.ProcessedAssetsPrefix.Length
                ..^BackendConsoleMilestonePatterns.ProcessedAssetsSuffix.Length].Trim();
            if (count == "0")
            {
                key = "consoleMessages.processedZeroAssetsSummary";
                return true;
            }

            key = "consoleMessages.processedAssetsSummary";
            args = [count];
            return true;
        }

        if (line.StartsWith(BackendConsoleMilestonePatterns.ProcessedAssetsPrefix, StringComparison.OrdinalIgnoreCase)
            && line.EndsWith(BackendConsoleMilestonePatterns.ProcessedResourcesSuffix, StringComparison.OrdinalIgnoreCase))
        {
            string count = line[
                BackendConsoleMilestonePatterns.ProcessedAssetsPrefix.Length
                ..^BackendConsoleMilestonePatterns.ProcessedResourcesSuffix.Length].Trim();
            if (count == "0")
            {
                key = "consoleMessages.processedZeroAssetsSummary";
                return true;
            }

            key = "consoleMessages.processedAssetsSummary";
            args = [count];
            return true;
        }

        if (line.StartsWith(BackendConsoleMilestonePatterns.TotalTimePrefix, StringComparison.OrdinalIgnoreCase))
        {
            key = "consoleMessages.totalTimeSummary";
            args = [line[BackendConsoleMilestonePatterns.TotalTimePrefix.Length..].Trim()];
            return true;
        }

        const string rdaNonDebugPrefix = "RDA extraction complete — processed ";
        const string rdaNonDebugSuffix = " archives in ";
        if (line.StartsWith(rdaNonDebugPrefix, StringComparison.Ordinal)
            && TryReadRdaArchiveSummary(line, rdaNonDebugPrefix, rdaNonDebugSuffix, out string processedPair, out string elapsed))
        {
            string[] parts = processedPair.Split('/', 2);
            if (parts.Length == 2)
            {
                key = "consoleMessages.rdaExtractionCompleteNonDebug";
                args = [parts[0].Trim(), parts[1].Trim(), elapsed.Trim()];
                return true;
            }
        }

        const string rdaShortPrefix = "RDA extraction complete (";
        const string rdaShortSuffix = " archives processed in ";
        if (line.StartsWith(rdaShortPrefix, StringComparison.Ordinal)
            && line.EndsWith(")", StringComparison.Ordinal)
            && TryReadRdaArchiveSummary(line, rdaShortPrefix, rdaShortSuffix, out processedPair, out elapsed))
        {
            string[] parts = processedPair.Split('/', 2);
            if (parts.Length == 2)
            {
                key = "consoleMessages.rdaExtractionCompleteShort";
                args = [parts[0].Trim(), parts[1].Trim(), elapsed.Trim().TrimEnd(')')];
                return true;
            }
        }

        return false;
    }

    private static bool TryReadRdaArchiveSummary(
        string line,
        string prefix,
        string suffix,
        out string processedPair,
        out string elapsed)
    {
        processedPair = "";
        elapsed = "";

        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        int suffixIndex = line.IndexOf(suffix, prefix.Length, StringComparison.Ordinal);
        if (suffixIndex < 0)
        {
            return false;
        }

        processedPair = line[prefix.Length..suffixIndex];
        elapsed = line[(suffixIndex + suffix.Length)..];
        return processedPair.Length > 0 && elapsed.Length > 0;
    }

    private static string StripBackendLogPrefix(string line)
    {
        string current = line;
        while (TryStripOneBackendLogPrefix(current, out string stripped))
        {
            current = stripped;
        }

        return current;
    }

    private static bool TryStripOneBackendLogPrefix(string line, out string stripped)
    {
        ReadOnlySpan<string> prefixes =
        [
            "[INFO] ",
            "[SUCCESS] ",
            "[COMPLETE] ",
            "[OK] ",
            "[RDA COMPLETE] ",
            "[SOURCE READY] ",
            "[WARNING] ",
            "[WARN] ",
            "[ERROR] ",
            "[READY] "
        ];

        foreach (string prefix in prefixes)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                stripped = line[prefix.Length..].TrimStart();
                return true;
            }
        }

        stripped = line;
        return false;
    }

    private static bool TryGetExactBackendKey(string line, [NotNullWhen(true)] out string? key)
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

        key = null;
        return false;
    }

    private static bool TryGetParameterizedBackendKey(string line, [NotNullWhen(true)] out string? key, out object[]? args)
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
