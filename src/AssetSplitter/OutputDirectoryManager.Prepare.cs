
namespace AssetProcessor;

public static partial class OutputDirectoryManager
{
    /// <summary>Phase 1: wipes and recreates <c>source_xml_{game}/</c>.</summary>
    public static bool TryPrepareSourceXmlDirectory(PipelineContext context, string sourceXmlPath)
    {
        if (!TryEnsureGameOutputRoot(context))
        {
            return false;
        }

        string requiredRoot = ResolveRequiredOutputRoot(context);
        try
        {
            if (Directory.Exists(sourceXmlPath) && !AssetProcessorFileSystem.IsDirectoryEmpty(sourceXmlPath))
            {
                if (!OutputPathSafety.TryDeleteDirectoryRecursive(context, sourceXmlPath, requiredRoot))
                {
                    context.Log.WriteLocalized(ConsoleMessageKind.Error, "sourceXmlFolderPrepareFailed", always: true, sourceXmlPath, "path not under output root or contains a reparse point");
                    return false;
                }

                if (context.DebugMode)
                {
                    context.Log.Write(ConsoleMessageKind.Info, ConsoleMessages.Get("clearedExistingSourceXmlFolder"));
                }
            }

            _ = Directory.CreateDirectory(sourceXmlPath);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "sourceXmlFolderPrepareFailed", always: true, sourceXmlPath, ex.Message);
            return false;
        }
    }

    /// <summary>Phase 3: creates the per-run asset XML output folder; clears unless repair preserve is on.</summary>
    public static bool TryPrepareAssetOutputDirectory(PipelineContext context)
    {
        if (!TryEnsureGameOutputRoot(context))
        {
            return false;
        }

        try
        {
            _ = Directory.CreateDirectory(context.AssetOutputDir);
            if (context.PreserveOutputDirectories)
            {
                return true;
            }

            return ClearDirectoryIfNeeded(context, context.AssetOutputDir, ConsoleMessages.Get("clearedExistingOutputFolder"));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "outputFolderClearFailed", always: true, ex.Message);
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
            if (context.PreserveOutputDirectories && !context.ForceResplitTemplates)
            {
                return true;
            }

            return ClearDirectoryIfNeeded(context, outputBase, ConsoleMessages.Get("clearedExistingTemplateSplitFolder"));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "outputFolderClearFailed", always: true, ex.Message);
            return false;
        }
    }

    /// <summary>Phase 8: recreates the mod package root; wipes unless repair preserve is on.</summary>
    public static bool TryPrepareModOutputDirectory(PipelineContext context)
    {
        if (!TryEnsureGameOutputRoot(context))
        {
            return false;
        }

        string outputRoot = context.IsSingleAssetMode
            ? context.SingleAssetModOutputRoot
            : context.AssetModOutputRoot;

        string requiredRoot = ResolveRequiredOutputRoot(context);
        try
        {
            if (Directory.Exists(outputRoot) && !context.PreserveOutputDirectories)
            {
                if (!OutputPathSafety.TryDeleteDirectoryRecursive(context, outputRoot, requiredRoot))
                {
                    context.Log.WriteLocalized(ConsoleMessageKind.Error, "modOutputFolderPrepareFailed", always: true, outputRoot, "path not under output root or contains a reparse point");
                    return false;
                }
            }

            _ = Directory.CreateDirectory(outputRoot);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "modOutputFolderPrepareFailed", always: true, outputRoot, ex.Message);
            return false;
        }
    }

    /// <summary>Phase 7: lazy — when -t moves a file into a template subfolder.</summary>
    public static string EnsureTemplateSubfolder(PipelineContext context, string templateName)
    {
        string safeTemplateName = OutputPathSafety.SanitizeSinglePathSegment(templateName, "Unknown");

        string combined = Path.Combine(context.AssetOutputDir, safeTemplateName);
        if (!OutputPathSafety.TryGetContainedPath(context.AssetOutputDir, combined, out string destDir))
        {
            if (!OutputPathSafety.TryGetContainedPath(context.AssetOutputDir, "Unknown", out destDir))
            {
                throw new InvalidOperationException("Could not resolve a safe template subfolder under the asset output directory.");
            }
        }

        _ = Directory.CreateDirectory(destDir);
        return destDir;
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
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "outputDirectoryCreateFailed", always: true, singleGuidXmlRoot, ex.Message);
            return false;
        }
    }

    private static bool ClearDirectoryIfNeeded(PipelineContext context, string directoryPath, string clearedMessage)
    {
        return OutputPathSafety.TryClearDirectory(
            context,
            directoryPath,
            ResolveRequiredOutputRoot(context),
            clearedMessage);
    }

    private static string ResolveRequiredOutputRoot(PipelineContext context) =>
        !string.IsNullOrWhiteSpace(context.GameOutputRoot)
            ? context.GameOutputRoot
            : context.AnnoAssetsRoot;
}
