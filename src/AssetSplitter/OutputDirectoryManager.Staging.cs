
namespace AssetProcessor;

public static partial class OutputDirectoryManager
{
    /// <summary>Phase 3: lazy — only when -f is on and the first inherited (BaseAssetGUID) asset is written.</summary>
    public static string EnsureBaseAssetGuidStagingDirectory(PipelineContext context)
    {
        string stagingPath = GetBaseAssetGuidStagingPath(context);
        _ = Directory.CreateDirectory(stagingPath);
        return stagingPath;
    }

    public static string GetBaseAssetGuidStagingPath(PipelineContext context)
    {
        string configuredFolder = context.AppSettingsConfig?.Settings?.OutputStructure?.BaseassetFolder
            ?? OutputStructureSettings.DefaultBaseAssetFolder;
        string folderName = OutputPathSafety.SanitizeSinglePathSegment(
            configuredFolder,
            OutputStructureSettings.DefaultBaseAssetFolder);
        return Path.Combine(context.AssetOutputDir, folderName);
    }

    /// <summary>Phase 6: remove staging copy after a successful parent merge wrote the complete mod to output root.</summary>
    public static void TryRemoveMergedStagingFile(PipelineContext context, string stagingFilePath)
    {
        string stagingRoot = GetBaseAssetGuidStagingPath(context);
        if (!OutputPathSafety.IsPathUnderDirectory(stagingFilePath, stagingRoot))
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
}
