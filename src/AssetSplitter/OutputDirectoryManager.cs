
namespace AssetProcessor;

public static partial class OutputDirectoryManager
{
    /// <summary>Resolves AnnoAssets / game subfolder layout and updates context path properties.</summary>
    public static bool TryResolveOutputLayout(PipelineContext context, string gameType, out string gameOutputRoot)
    {
        gameOutputRoot = "";
        if (string.IsNullOrWhiteSpace(context.BaseOutputDir))
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "permissionsError", always: true);
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
            context.AssetOutputDir = context.SingleAssetOutputRoot;
            context.AssetModOutputRoot = Path.Combine(gameOutputRoot, "single_guid_mods");
            context.SingleAssetModOutputRoot = Path.Combine(gameOutputRoot, "single_guid_mods", folderName);
        }
        else
        {
            context.AssetOutputDir = Path.Combine(gameOutputRoot, "output_xml_" + normalizedGameType);
            context.AssetModOutputRoot = Path.Combine(gameOutputRoot, "output_xml_" + normalizedGameType + "_mods");
        }

        if (context.DebugMode)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugOutputLayoutAnnoAssets"), context.AnnoAssetsRoot));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugOutputLayoutGameRoot"), context.GameOutputRoot));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugOutputLayoutAssetOut"), context.AssetOutputDir));
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
                    context.Log.WriteLocalized(ConsoleMessageKind.Info, "creatingOutputDirectory", always: true, context.BaseOutputDir);
                }

                _ = Directory.CreateDirectory(context.BaseOutputDir);
                if (context.DebugMode)
                {
                    context.Log.Write(ConsoleMessageKind.Ok, ConsoleMessages.Get("outputDirectoryCreated"));
                }
            }

            if (!Directory.Exists(context.AnnoAssetsRoot))
            {
                _ = Directory.CreateDirectory(context.AnnoAssetsRoot);
            }

            ModReadmeWriter.WriteAnnoAssetsReadme(context.AnnoAssetsRoot);

            if (!Directory.Exists(context.GameOutputRoot))
            {
                _ = Directory.CreateDirectory(context.GameOutputRoot);
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "outputDirectoryCreateFailed", always: true, context.GameOutputRoot, ex.Message);
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "permissionsError", always: true);
            return false;
        }
    }

    public static bool EnsureGameDirectoryExists(PipelineContext context)
    {
        if (Directory.Exists(context.AssetRoot))
        {
            return true;
        }

        Console.WriteLine();
        context.Log.WriteLocalized(ConsoleMessageKind.Error, "gameNotDetected", always: true);
        Console.WriteLine();
        context.Log.WriteLocalized(ConsoleMessageKind.Warning, "gameDirectoryNotFound", always: true, context.AssetRoot);
        Console.WriteLine();
        context.Log.WriteLocalized(ConsoleMessageKind.Info, "solutionsLabel", always: true);
        context.Log.WriteLocalized(ConsoleMessageKind.Info, "solutionVerifyGamePath", always: true);
        context.Log.WriteLocalized(ConsoleMessageKind.Info, "solutionUseFullPath", always: true);
        context.Log.WriteLocalized(ConsoleMessageKind.Info, "solutionRunLauncher", always: true);
        Console.WriteLine();
        return false;
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

    /// <summary><c>source_xml_{gameType}</c> under the game output root.</summary>
    public static string GetSourceXmlDirectory(string gameOutputRoot, string gameType) =>
        Path.Combine(gameOutputRoot, "source_xml_" + NormalizeGameType(gameType));

    /// <summary><c>output_xml_{gameType}</c> under the game output root (batch mode).</summary>
    public static string GetAssetXmlOutputDirectory(string gameOutputRoot, string gameType) =>
        Path.Combine(gameOutputRoot, "output_xml_" + NormalizeGameType(gameType));

    /// <summary><c>output_xml_{gameType}_mods</c> under the game output root (batch mode).</summary>
    public static string GetAssetModOutputDirectory(string gameOutputRoot, string gameType) =>
        Path.Combine(gameOutputRoot, "output_xml_" + NormalizeGameType(gameType) + "_mods");

    /// <summary><c>output_templates_{gameType}</c> under the game output root.</summary>
    public static string GetTemplatesOutputDirectory(string gameOutputRoot, string gameType) =>
        Path.Combine(gameOutputRoot, "output_templates_" + NormalizeGameType(gameType));

    private static string NormalizeGameType(string gameType) =>
        GameTypeDetector.IsAnno117(gameType) ? GameTypeDetector.Anno117 : GameTypeDetector.Anno1800;

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
}
