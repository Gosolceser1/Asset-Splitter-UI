namespace AssetProcessor;

public static class OutputDirectoryManager
{
    public static bool TryPrepareOutputDirectories(PipelineContext context, string gameType, out string gameOutputRoot)
    {
        gameOutputRoot = "";
        if (!EnsureBaseOutputDirectory(context))
            return false;

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

            // Only clear the specific GUID folder on re-run, not the entire collection.
            _ = Directory.CreateDirectory(context.AssetOut);
            if (!ClearOutputDirectoryIfNeeded(context))
                return false;
        }
        else
        {
            context.AssetOut = Path.Combine(gameOutputRoot, "output_xml_" + normalizedGameType);
            context.AssetModOutputRoot = Path.Combine(gameOutputRoot, "output_xml_" + normalizedGameType + "_mods");
            _ = Directory.CreateDirectory(context.AssetOut);
            if (!ClearOutputDirectoryIfNeeded(context))
                return false;
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

    private static bool EnsureBaseOutputDirectory(PipelineContext context)
    {
        if (Directory.Exists(context.BaseOutputDir))
            return true;

        try
        {
            if (context.DebugMode)
                context.Log.Write("INFO", string.Format(ConsoleMessages.Get("creatingOutputDirectory"), context.BaseOutputDir));

            _ = Directory.CreateDirectory(context.BaseOutputDir);

            if (context.DebugMode)
                context.Log.Write("OK", ConsoleMessages.Get("outputDirectoryCreated"));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("outputDirectoryCreateFailed"), context.BaseOutputDir, ex.Message), always: true);
            context.Log.Write("ERROR", ConsoleMessages.Get("permissionsError"), always: true);
            return false;
        }

        return true;
    }

    private static bool ClearOutputDirectoryIfNeeded(PipelineContext context)
    {
        if (AssetProcessorFileSystem.IsDirectoryEmpty(context.AssetOut))
            return true;

        try
        {
            Directory.Delete(context.AssetOut, true);
            _ = Directory.CreateDirectory(context.AssetOut);

            if (context.DebugMode)
                context.Log.Write("INFO", ConsoleMessages.Get("clearedExistingOutputFolder"));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("outputFolderClearFailed"), ex.Message), always: true);
            return false;
        }

        return true;
    }

    public static bool EnsureGameDirectoryExists(PipelineContext context)
    {
        if (Directory.Exists(context.AssetRoot))
            return true;

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
}
