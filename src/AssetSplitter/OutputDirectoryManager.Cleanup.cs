
namespace AssetProcessor;

public static partial class OutputDirectoryManager
{
    /// <summary>Mod-export-only: delete intermediate output_xml after packages are written.</summary>
    public static void TryRemoveIntermediateAssetOutputAfterModExport(PipelineContext context)
    {
        if (!PipelineFeatureGates.IsModExportOnlyRun(context))
        {
            return;
        }

        string pathToRemove = context.IsSingleAssetMode
            ? Directory.GetParent(context.AssetOutputDir)?.FullName ?? context.AssetOutputDir
            : context.AssetOutputDir;

        if (string.IsNullOrWhiteSpace(pathToRemove) || !Directory.Exists(pathToRemove))
        {
            return;
        }

        if (!OutputPathSafety.IsPathUnderDirectory(pathToRemove, context.GameOutputRoot))
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
                context.Log.WriteLocalized(ConsoleMessageKind.Info, "modExportRemovedStagingFolder", always: true, pathToRemove);
            }
        }
        catch (IOException ex)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Warning, "modExportStagingFolderDeleteFailed", always: true, pathToRemove, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Warning, "modExportStagingFolderDeleteFailed", always: true, pathToRemove, ex.Message);
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
}
