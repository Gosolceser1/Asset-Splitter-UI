namespace AssetProcessor;

/// <summary>
/// Creates output folders at the pipeline phase that first needs them — not during init or Phase 1
/// for paths that belong to later phases.
/// </summary>
public static class OutputDirectoryManager
{
    /// <summary>
    /// Computes output layout strings only. Does not create any directories on disk.
    /// </summary>
    public static bool TryResolveOutputLayout(PipelineContext context, string gameType, out string gameOutputRoot)
    {
        gameOutputRoot = "";
        if (string.IsNullOrWhiteSpace(context.BaseOutputDir))
        {
            context.Log.Write("ERROR", ConsoleMessages.Get("permissionsError"), always: true);
            return false;
        }

        string annoAssetsRoot = Path.GetFileName(Path.TrimEndingDirectorySeparator(context.BaseOutputDir)).Equals("AnnoAssets", StringComparison.OrdinalIgnoreCase)
          ? context.BaseOutputDir
          : Path.Combine(context.BaseOutputDir, "AnnoAssets");

        string normalizedGameType = GameTypeDetector.IsAnno117(gameType)
            ? GameTypeDetector.Anno117
            : GameTypeDetector.Anno1800;
        string gameSubfolder = normalizedGameType.Equals(GameTypeDetector.Anno1800, StringComparison.OrdinalIgnoreCase)
            ? "Anno1800"
            : "Anno117";
        gameOutputRoot = Path.Combine(annoAssetsRoot, gameSubfolder);
        context.GameOutputRoot = gameOutputRoot;
        context.AnnoAssetsRoot = annoAssetsRoot;

        if (context.IsSingleAssetMode)
        {
            string folderName = BuildSingleAssetFolderName(context, normalizedGameType);
            string singleGuidXmlRoot = Path.Combine(gameOutputRoot, "single_guid_output_xml_" + normalizedGameType);
            context.SingleAssetOutputRoot = Path.Combine(singleGuidXmlRoot, folderName);
            context.AssetOut = context.SingleAssetOutputRoot;
            context.AssetModOutputRoot = Path.Combine(gameOutputRoot, "single_guid_mods");
            context.SingleAssetModOutputRoot = Path.Combine(gameOutputRoot, "single_guid_mods", folderName);
        }
        else
        {
            context.AssetOut = Path.Combine(gameOutputRoot, "output_xml_" + normalizedGameType);
            context.AssetModOutputRoot = Path.Combine(gameOutputRoot, "output_xml_" + normalizedGameType + "_mods");
        }

        if (context.DebugMode)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugOutputLayoutAnnoAssets"), context.AnnoAssetsRoot));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugOutputLayoutGameRoot"), context.GameOutputRoot));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugOutputLayoutAssetOut"), context.AssetOut));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugOutputLayoutModOut"), context.AssetModOutputRoot));
            if (context.IsSingleAssetMode)
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugOutputLayoutSingleMod"), context.SingleAssetModOutputRoot));
            }
        }

        return true;
    }

    /// <summary>Phase 1: creates AnnoAssets + game root (e.g. Anno1800/). Does not touch source_xml or output folders.</summary>
    public static bool TryEnsureGameOutputRoot(PipelineContext context)
    {
        try
        {
            if (!Directory.Exists(context.BaseOutputDir))
            {
                if (context.DebugMode)
                {
                    context.Log.Write("INFO", string.Format(ConsoleMessages.Get("creatingOutputDirectory"), context.BaseOutputDir));
                }

                _ = Directory.CreateDirectory(context.BaseOutputDir);
                if (context.DebugMode)
                {
                    context.Log.Write("OK", ConsoleMessages.Get("outputDirectoryCreated"));
                }
            }

            if (!Directory.Exists(context.AnnoAssetsRoot))
            {
                _ = Directory.CreateDirectory(context.AnnoAssetsRoot);
            }

            if (!Directory.Exists(context.GameOutputRoot))
            {
                _ = Directory.CreateDirectory(context.GameOutputRoot);
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("outputDirectoryCreateFailed"), context.GameOutputRoot, ex.Message), always: true);
            context.Log.Write("ERROR", ConsoleMessages.Get("permissionsError"), always: true);
            return false;
        }
    }

    /// <summary>Phase 1: wipes and recreates <c>source_xml_{game}/</c>.</summary>
    public static bool TryPrepareSourceXmlDirectory(PipelineContext context, string sourceXmlPath)
    {
        if (!TryEnsureGameOutputRoot(context))
        {
            return false;
        }

        try
        {
            if (Directory.Exists(sourceXmlPath) && !AssetProcessorFileSystem.IsDirectoryEmpty(sourceXmlPath))
            {
                Directory.Delete(sourceXmlPath, true);
                if (context.DebugMode)
                {
                    context.Log.Write("INFO", ConsoleMessages.Get("clearedExistingSourceXmlFolder"));
                }
            }

            _ = Directory.CreateDirectory(sourceXmlPath);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("sourceXmlFolderPrepareFailed"), sourceXmlPath, ex.Message), always: true);
            return false;
        }
    }

    /// <summary>Phase 3: creates and clears the per-run asset XML output folder (not mod or template folders).</summary>
    public static bool TryPrepareAssetOutputDirectory(PipelineContext context)
    {
        if (!TryEnsureGameOutputRoot(context))
        {
            return false;
        }

        try
        {
            _ = Directory.CreateDirectory(context.AssetOut);
            return ClearDirectoryIfNeeded(context, context.AssetOut, ConsoleMessages.Get("clearedExistingOutputFolder"));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("outputFolderClearFailed"), ex.Message), always: true);
            return false;
        }
    }

    /// <summary>Phase 2B: creates/clears <c>output_templates_{game}/</c> when --split-templates runs.</summary>
    public static bool TryPrepareTemplateSplitDirectory(PipelineContext context, string gameType)
    {
        if (!TryEnsureGameOutputRoot(context))
        {
            return false;
        }

        string outputBase = Path.Combine(context.GameOutputRoot, "output_templates_" + gameType);
        try
        {
            _ = Directory.CreateDirectory(outputBase);
            return ClearDirectoryIfNeeded(context, outputBase, ConsoleMessages.Get("clearedExistingTemplateSplitFolder"));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("outputFolderClearFailed"), ex.Message), always: true);
            return false;
        }
    }

    /// <summary>Phase 3: lazy — only when -f is on and the first inherited (BaseAssetGUID) asset is written.</summary>
    public static string EnsureBaseAssetGuidStagingDirectory(PipelineContext context)
    {
        string stagingPath = GetBaseAssetGuidStagingPath(context);
        _ = Directory.CreateDirectory(stagingPath);
        return stagingPath;
    }

    public static string GetBaseAssetGuidStagingPath(PipelineContext context)
    {
        string folderName = context.AppSettingsConfig?.Settings?.OutputStructure?.BaseassetFolder ?? OutputStructureSettings.DefaultBaseAssetFolder;
        return Path.Combine(context.AssetOut, folderName);
    }

    /// <summary>Phase 6: remove staging copy after a successful parent merge wrote the complete mod to output root.</summary>
    public static void TryRemoveMergedStagingFile(PipelineContext context, string stagingFilePath)
    {
        string stagingRoot = GetBaseAssetGuidStagingPath(context);
        if (!IsPathUnderDirectory(stagingFilePath, stagingRoot))
        {
            return;
        }

        try
        {
            if (File.Exists(stagingFilePath))
            {
                File.Delete(stagingFilePath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepStagingFileDeleteFailed"), stagingFilePath, ex.Message));
        }
    }

    /// <summary>Phase 6 end: drop the staging folder when every inherited asset was merged out (e.g. no PaMSy left).</summary>
    public static void TryRemoveEmptyStagingDirectory(PipelineContext context)
    {
        string stagingPath = GetBaseAssetGuidStagingPath(context);
        try
        {
            if (!Directory.Exists(stagingPath))
            {
                return;
            }

            if (!AssetProcessorFileSystem.IsDirectoryEmpty(stagingPath))
            {
                return;
            }

            Directory.Delete(stagingPath);
            if (context.DebugMode)
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepRemovedEmptyStagingFolder"), stagingPath));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepStagingFolderDeleteFailed"), stagingPath, ex.Message));
        }
    }

    private static bool IsPathUnderDirectory(string filePath, string directoryPath)
    {
        string fullFile = Path.GetFullPath(filePath);
        string fullDir = Path.GetFullPath(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
        return fullFile.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Phase 7: lazy — when -t moves a file into a template subfolder.</summary>
    public static string EnsureTemplateSubfolder(PipelineContext context, string templateName)
    {
        string safeTemplateName = AssetTextSanitizer.SanitizeFileNamePart(templateName, 90);
        if (string.IsNullOrWhiteSpace(safeTemplateName))
        {
            safeTemplateName = "Unknown";
        }

        string destDir = Path.Combine(context.AssetOut, safeTemplateName);
        _ = Directory.CreateDirectory(destDir);
        return destDir;
    }

    /// <summary>Mod-export-only: delete intermediate output_xml after packages are written.</summary>
    public static void TryRemoveIntermediateAssetOutputAfterModExport(PipelineContext context)
    {
        if (!PipelineFeatureGates.IsModExportOnlyRun(context))
        {
            return;
        }

        string pathToRemove = context.IsSingleAssetMode
            ? Directory.GetParent(context.AssetOut)?.FullName ?? context.AssetOut
            : context.AssetOut;

        if (string.IsNullOrWhiteSpace(pathToRemove) || !Directory.Exists(pathToRemove))
        {
            return;
        }

        if (!IsPathUnderDirectory(pathToRemove, context.GameOutputRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(pathToRemove, recursive: true);
            if (context.DebugMode)
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugModExportRemovedStaging"), pathToRemove));
            }
            else
            {
                context.Log.Write("INFO", string.Format(ConsoleMessages.Get("modExportRemovedStagingFolder"), pathToRemove), always: true);
            }
        }
        catch (IOException ex)
        {
            context.Log.Write("WARN", string.Format(ConsoleMessages.Get("modExportStagingFolderDeleteFailed"), pathToRemove, ex.Message), always: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            context.Log.Write("WARN", string.Format(ConsoleMessages.Get("modExportStagingFolderDeleteFailed"), pathToRemove, ex.Message), always: true);
        }
    }

    /// <summary>Phase 8: wipes and recreates the mod package root for this run.</summary>
    public static bool TryPrepareModOutputDirectory(PipelineContext context)
    {
        if (!TryEnsureGameOutputRoot(context))
        {
            return false;
        }

        string outputRoot = context.IsSingleAssetMode
            ? context.SingleAssetModOutputRoot
            : context.AssetModOutputRoot;

        try
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, true);
            }

            _ = Directory.CreateDirectory(outputRoot);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("modOutputFolderPrepareFailed"), outputRoot, ex.Message), always: true);
            return false;
        }
    }

    public static void TryDeleteFixerScratchFile(string baseOutputDir)
    {
        if (string.IsNullOrWhiteSpace(baseOutputDir))
        {
            return;
        }

        string fixerLogPath = Path.Combine(baseOutputDir, "fixer.txt");
        try
        {
            if (File.Exists(fixerLogPath))
            {
                File.Delete(fixerLogPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Best effort — scratch file only.
        }
    }

    /// <summary>End of run / issue report: creates <c>logs/</c> only when something is written.</summary>
    public static string? TryPrepareLogsDirectory(string annoAssetsRoot)
    {
        if (string.IsNullOrWhiteSpace(annoAssetsRoot))
        {
            return null;
        }

        string logsDir = Path.Combine(annoAssetsRoot, "logs");
        try
        {
            _ = Directory.CreateDirectory(logsDir);
            return logsDir;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Phase 3 single-GUID rename: parent container only — not mod output.</summary>
    public static bool EnsureSingleGuidXmlContainer(PipelineContext context, string normalizedGameType)
    {
        if (!TryEnsureGameOutputRoot(context))
        {
            return false;
        }

        string singleGuidXmlRoot = Path.Combine(context.GameOutputRoot, "single_guid_output_xml_" + normalizedGameType);
        try
        {
            _ = Directory.CreateDirectory(singleGuidXmlRoot);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("outputDirectoryCreateFailed"), singleGuidXmlRoot, ex.Message), always: true);
            return false;
        }
    }

    /// <summary>Lazy mkdir for nested paths (e.g. Phase 8 mod package asset folders).</summary>
    public static void EnsureDirectoryExists(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        _ = Directory.CreateDirectory(directoryPath);
    }

    public static bool EnsureGameDirectoryExists(PipelineContext context)
    {
        if (Directory.Exists(context.AssetRoot))
        {
            return true;
        }

        Console.WriteLine();
        context.Log.Write("ERROR", ConsoleMessages.Get("gameNotDetected"), always: true);
        Console.WriteLine();
        context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("gameDirectoryNotFound"), context.AssetRoot), always: true);
        Console.WriteLine();
        context.Log.Write("INFO", ConsoleMessages.Get("solutionsLabel"), always: true);
        context.Log.Write("INFO", ConsoleMessages.Get("solutionVerifyGamePath"), always: true);
        context.Log.Write("INFO", ConsoleMessages.Get("solutionUseFullPath"), always: true);
        context.Log.Write("INFO", ConsoleMessages.Get("solutionRunLauncher"), always: true);
        Console.WriteLine();
        return false;
    }

    private static string BuildSingleAssetFolderName(PipelineContext context, string gameType)
    {
        string safeGuid = AssetTextSanitizer.SanitizeFileNamePart(context.SingleAssetGuid, 40);
        string safeName = "Unknown Asset";
        if (!string.IsNullOrWhiteSpace(context.SingleAssetDisplayName))
        {
            safeName = AssetTextSanitizer.SanitizeFileNamePart(context.SingleAssetDisplayName, 80);
        }

        return $"{safeGuid} - {safeName}";
    }

    private static bool ClearDirectoryIfNeeded(PipelineContext context, string directoryPath, string clearedMessage)
    {
        try
        {
            if (AssetProcessorFileSystem.IsDirectoryEmpty(directoryPath))
            {
                return true;
            }

            Directory.Delete(directoryPath, true);
            _ = Directory.CreateDirectory(directoryPath);

            if (context.DebugMode)
            {
                context.Log.Write("INFO", clearedMessage);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("outputFolderClearFailed"), ex.Message), always: true);
            return false;
        }

        return true;
    }
}
