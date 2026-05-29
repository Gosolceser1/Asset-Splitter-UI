using AssetSplitterUI.ViewModels;

namespace AssetSplitterUI.Services;

/// <summary>Assigns a <see cref="LogLineKind"/> from raw console text.</summary>
internal static class ConsoleLineClassifier
{
    public static LogLineKind Classify(string text)
    {
        if (string.IsNullOrEmpty(text)) return LogLineKind.Empty;

        var t = text.TrimStart();
        if (t.Length == 0) return LogLineKind.Empty;

        return t[0] switch
        {
            '┌' or '│' or '└' or '├' => LogLineKind.Header,
            '━' or '═' => LogLineKind.Separator,
            '[' when ConsoleProgressLineParser.IsProgressLine(t) => LogLineKind.Progress,
            '[' when IsTraceLine(t) => LogLineKind.Trace,
            '[' when IsDebugLine(t) => LogLineKind.Debug,
            '[' when t.Contains("] Processing", StringComparison.Ordinal) => LogLineKind.Processing,
            '[' when t.Contains("[DONE]", StringComparison.Ordinal) => LogLineKind.Success,
            '[' when t.Contains("[SUCCESS]", StringComparison.Ordinal) => LogLineKind.Success,
            _ => ClassifyContent(t),
        };
    }

    private static LogLineKind ClassifyContent(string t) =>
        IsTraceLine(t) ? LogLineKind.Trace :
        IsDebugLine(t) ? LogLineKind.Debug :
        IsPhaseOrStepHeader(t) ? LogLineKind.Phase :
        IsOptionOnLine(t) ? LogLineKind.OptionOn :
        IsOptionOffLine(t) ? LogLineKind.OptionOff :
        IsCommandLine(t) ? LogLineKind.Command :
        t.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) ? LogLineKind.Error :
        t.StartsWith("WARNING", StringComparison.OrdinalIgnoreCase) ? LogLineKind.Warning :
        IsErrorLine(t) ? LogLineKind.Error :
        IsWarningLine(t) ? LogLineKind.Warning :
        IsSuccessLine(t) ? LogLineKind.Success :
        IsSummaryLine(t) ? LogLineKind.Summary :
        IsSubsystemLine(t) ? LogLineKind.Subsystem :
        IsInfoLine(t) ? LogLineKind.Info :
        LogLineKind.Normal;

    /// <summary>True for backend alert tags — used in the developer report (not asset-name heuristics).</summary>
    public static bool IsReportableAlert(string text) =>
        !string.IsNullOrWhiteSpace(text) &&
        (text.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("[WARN]", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("[WARNING]", StringComparison.OrdinalIgnoreCase));

    private static bool IsPhaseOrStepHeader(string t) =>
        t.Contains("PHASE", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("ФАЗА", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("===", StringComparison.Ordinal) ||
        t.StartsWith("Extracting game data from RDA", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("extracting from RDA", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("reading language file", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("Building asset name registry", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("reading properties file", StringComparison.OrdinalIgnoreCase) ||
        (t.StartsWith("Extracting ", StringComparison.OrdinalIgnoreCase) &&
         (t.Contains(" assets to XML", StringComparison.OrdinalIgnoreCase) ||
          t.Contains(" assets to ModOp", StringComparison.OrdinalIgnoreCase))) ||
        t.StartsWith("extracting assets", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("Inheriting template properties", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("Merging templates", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("Preparing final formatting pass", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("Preparing BaseAssetGUID reference pass", StringComparison.OrdinalIgnoreCase) ||
        (t.StartsWith("Processing ", StringComparison.OrdinalIgnoreCase) &&
         t.Contains("BaseAssetGUID reference files", StringComparison.OrdinalIgnoreCase)) ||
        t.StartsWith("Final processing of ", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("Source files copied to", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("formatting assets", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("Resolving BaseAssetGUID", StringComparison.OrdinalIgnoreCase) ||
        (t.StartsWith("Resolving ", StringComparison.OrdinalIgnoreCase) &&
         t.Contains("dependencies", StringComparison.OrdinalIgnoreCase));

    private static bool IsSuccessLine(string t) =>
        t.Contains("extraction complete", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("извлечение завершено", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("assets saved", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[OK]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[ОК]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[READY]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[COMPLETE]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[ГОТОВО]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[SUCCESS]", StringComparison.Ordinal) ||
        t.Trim() == "[DONE]" ||
        (t.StartsWith("✓", StringComparison.Ordinal) && !IsOptionOnLine(t));

    private static bool IsErrorLine(string t) =>
        t.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("exception", StringComparison.OrdinalIgnoreCase);

    private static bool IsWarningLine(string t) =>
        t.Contains("[WARN]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[WARNING]", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("WARN", StringComparison.OrdinalIgnoreCase);

    private static bool IsOptionOnLine(string t) =>
        t.StartsWith("✓ ", StringComparison.Ordinal);

    private static bool IsOptionOffLine(string t) =>
        t.StartsWith("○ ", StringComparison.Ordinal);

    private static bool IsInfoLine(string t) =>
        t.StartsWith("Game:", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("Output:", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("Language:", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("Game Language:", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[SINGLE]", StringComparison.OrdinalIgnoreCase);

    private static bool IsDebugLine(string t) =>
        t.Contains("[DEBUG]", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("DEBUG:", StringComparison.OrdinalIgnoreCase);

    private static bool IsCommandLine(string t) =>
        t.StartsWith("AssetProcessor.exe ", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("\\AssetProcessor.dll", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("dotnet ", StringComparison.OrdinalIgnoreCase);

    private static bool IsSubsystemLine(string t) =>
        t.Contains("[INFO]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[CONFIG]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[TRANS]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[ASSETS]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[ANALYZE]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[EXTRACT]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[TEMPLATES]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[RDA]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[GUID]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[MODS]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[MERGE]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[CACHE]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[FIX]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[SPLIT]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[PHASE]", StringComparison.OrdinalIgnoreCase);

    private static bool IsSummaryLine(string t) =>
        t.Contains("… ×", StringComparison.Ordinal) ||
        t.StartsWith("Loaded ", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("Config loaded", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("Processed ", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("Created ", StringComparison.OrdinalIgnoreCase) ||
        t.StartsWith("RDA extraction complete", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("translations", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("asset names", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("archives processed", StringComparison.OrdinalIgnoreCase);

    private static bool IsTraceLine(string t) =>
        t.Contains("[DEBUG][MERGE]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[DEBUG][CACHE]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[DEBUG][GUID]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("[DEBUG][RDA]", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("ОБЪЕДИНЕНИЕ:", StringComparison.OrdinalIgnoreCase) ||
        t.Contains("ПРОПУСК:", StringComparison.OrdinalIgnoreCase);
}
