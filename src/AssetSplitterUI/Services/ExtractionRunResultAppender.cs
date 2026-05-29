using AssetSplitterUI.ViewModels;

namespace AssetSplitterUI.Services;

/// <summary>Appends Phase 1/2 completion banners, run outcome messages, and run header to the console log.</summary>
internal static class ExtractionRunResultAppender
{
    public const string Phase1Separator = "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";
    public const string Phase2Separator = "═══════════════════════════════════════════════════════════";

    public static void AppendPhase1Complete(MainWindowLogStore logStore)
    {
        logStore.AppendRaw("");
        logStore.AppendRaw(Phase1Separator);
        logStore.AppendLocalized("consoleMessages.phase1Summary");
        logStore.AppendRaw("");
        logStore.AppendLocalized("consoleMessages.phase2Instructions");
        logStore.AppendRaw(Phase1Separator);
        logStore.AppendRaw("");
    }

    public static void AppendPhase2Complete(
        MainWindowLogStore logStore,
        string outputPath,
        string gamePath,
        string gameType,
        string? singleGuid,
        bool createAssetMods)
    {
        logStore.AppendRaw("");
        logStore.AppendRaw(Phase2Separator);
        logStore.AppendLocalized("consoleMessages.extractionComplete");
        logStore.AppendLocalized("consoleMessages.assetsSaved");
        AppendResolvedOutputFolders(logStore, outputPath, gamePath, gameType, singleGuid, createAssetMods);
        logStore.AppendRaw(Phase2Separator);
        logStore.AppendRaw("");
    }

    private static void AppendResolvedOutputFolders(
        MainWindowLogStore logStore,
        string outputPath,
        string gamePath,
        string gameType,
        string? singleGuid,
        bool createAssetMods)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        string normalizedGameType = IsAnno117(gameType, gamePath) ? "anno117" : "anno1800";
        string annoAssetsRoot = Path.Combine(outputPath, "AnnoAssets");
        string gameFolder = normalizedGameType == "anno117" ? "Anno117" : "Anno1800";
        string gameOutputRoot = Path.Combine(annoAssetsRoot, gameFolder);
        logStore.AppendRaw("");
        logStore.AppendLocalized("consoleMessages.outputFolders");

        if (!string.IsNullOrWhiteSpace(singleGuid))
        {
            logStore.AppendLocalized("consoleMessages.outputXmlFolder", [Path.Combine(gameOutputRoot, "single_guid_output_xml_" + normalizedGameType)]);
            if (createAssetMods)
                logStore.AppendLocalized("consoleMessages.outputModsFolder", [Path.Combine(gameOutputRoot, "single_guid_mods")]);
            return;
        }

        logStore.AppendLocalized("consoleMessages.outputXmlFolder", [Path.Combine(gameOutputRoot, "output_xml_" + normalizedGameType)]);
        if (createAssetMods)
            logStore.AppendLocalized("consoleMessages.outputModsFolder", [Path.Combine(gameOutputRoot, "output_xml_" + normalizedGameType + "_mods")]);
    }

    private static bool IsAnno117(string gameType, string gamePath)
    {
        if (gameType.Equals("anno117", StringComparison.OrdinalIgnoreCase)
            || gameType.Equals("Anno117", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string folderName = Path.GetFileName(gamePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return folderName.Contains("Anno 117", StringComparison.OrdinalIgnoreCase)
            || folderName.Contains("Anno117", StringComparison.OrdinalIgnoreCase);
    }

    public static void AppendCancelled(MainWindowLogStore logStore)
    {
        logStore.AppendRaw("");
        logStore.AppendLocalized("dialogs.extractionCancelled");
    }

    public static void AppendError(MainWindowLogStore logStore, string message)
    {
        logStore.AppendRaw("");
        logStore.AppendLocalized("consoleMessages.errorWithMessage", [message]);
    }

    /// <summary>Logs the run header (borders, game/output paths, language, options).</summary>
    public static void AppendRunHeader(
        MainWindowLogStore logStore,
        string gamePath,
        string outputPath,
        string language,
        string? singleGuid,
        bool addComments,
        bool fixDependencies,
        bool createTemplateFolders,
        bool modOpsWrap,
        bool includeDefaultProperties,
        bool splitTemplates,
        bool createAssetMods,
        bool debugMode,
        string? resolvedAnnoAssetsPath = null,
        string? uiLanguageLabel = null)
    {
        foreach (string boxLine in RunHeaderBox.BuildLines())
            logStore.AppendRaw(boxLine);
        logStore.AppendLocalized("consoleMessages.consoleGame", [Path.GetFileName(gamePath)]);
        logStore.AppendLocalized("consoleMessages.consoleOutput", [resolvedAnnoAssetsPath ?? outputPath]);

        if (language == "none") return;

        logStore.AppendLocalized("consoleMessages.consoleLanguage", [language]);
        if (debugMode && !string.IsNullOrWhiteSpace(uiLanguageLabel))
            logStore.AppendLocalized("consoleMessages.consoleUiLanguage", [uiLanguageLabel]);
        if (!string.IsNullOrWhiteSpace(singleGuid))
            logStore.AppendLocalized("consoleMessages.consoleSingleGuid", [singleGuid.Trim()]);
        logStore.AppendRaw("");
        AppendLogOption(logStore, "consoleMessages.optionComments", addComments);
        AppendLogOption(logStore, "consoleMessages.optionDependencies", fixDependencies);
        AppendLogOption(logStore, "consoleMessages.optionFolders", createTemplateFolders);
        AppendLogOption(logStore, "consoleMessages.optionModOpsWrap", modOpsWrap);
        AppendLogOption(logStore, "consoleMessages.optionIncludeDefaultProperties", includeDefaultProperties);
        AppendLogOption(logStore, "consoleMessages.optionSplitTemplates", splitTemplates);
        AppendLogOption(logStore, "consoleMessages.optionCreateAssetMods", createAssetMods);
        AppendLogOption(logStore, "consoleMessages.optionDebugMode", debugMode);
        logStore.AppendRaw("");
    }

    private static void AppendLogOption(MainWindowLogStore logStore, string messageKey, bool isOn) =>
        logStore.AppendLocalized(messageKey, [isOn]);

    /// <summary>Returns a localization key if validation fails, or null when paths are valid.</summary>
    public static string? ValidatePaths(string gamePath, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
            return "dialogs.selectGameDir";
        if (string.IsNullOrWhiteSpace(outputPath))
            return "dialogs.selectOutputDir";
        if (File.Exists(outputPath))
            return "dialogs.invalidOutputPath";
        return null;
    }
}
