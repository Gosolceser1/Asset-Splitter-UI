using RDAExplorer;

namespace AssetProcessor;

public static partial class RdaArchiveExtractor
{
    private static bool TryExtractArchive(
        PipelineContext context,
        string rdaPath,
        string rdaName,
        string outputFolder,
        string extractFilter,
        out RdaExtractStatistics stats,
        bool skipExistingBareOutput = false,
        bool compactAnno1800Debug = false,
        Dictionary<string, (string Archive, long Bytes)>? fileProvenance = null)
    {
        RdaArchiveDiagnostics? archiveDiagnostics = null;
        var pendingDebugLines = new List<string>();
        try
        {
            stats = RDAFileExtension.ExtractAll(
                rdaPath,
                outputFolder,
                extractFilter,
                bare: true,
                skipExistingBareOutput: skipExistingBareOutput,
                onArchiveAnalyzed: context.DebugMode ? diagnostics => archiveDiagnostics = diagnostics : null,
                onExtracted: context.DebugMode ? (archivePath, outputPath, bytes) =>
                {
                    string bareName = Path.GetFileName(outputPath);
                    if (fileProvenance is not null && bareName.Length > 0)
                    {
                        fileProvenance[bareName] = (rdaName, bytes);
                    }

                    pendingDebugLines.Add(string.Format(
                        ConsoleMessages.Get("debugRdaExtractedFile"),
                        archivePath,
                        outputPath,
                        bytes.ToString("N0")));
                }
            : null,
                onSkipped: context.DebugMode ? (archivePath, reason) =>
                {
                    if (reason.Contains("filter criteria", StringComparison.OrdinalIgnoreCase)
                        || reason.Contains("newer patch already", StringComparison.OrdinalIgnoreCase)
                        || reason.Contains("checksum or metadata", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    pendingDebugLines.Add(string.Format(
                        ConsoleMessages.Get("debugRdaSkippedEntry"),
                        archivePath,
                        reason));
                }
            : null);

            if (stats.UnreadableBlocks.Count > 0 || stats.FailedEntries.Count > 0)
            {
                // Files from the unreadable block / failed entries may now come from older archives:
                // warn and do not trust this extraction (Phase 1 reports it and skips the stamp).
                foreach (string blockMessage in stats.UnreadableBlocks.Concat(stats.FailedEntries))
                {
                    context.Log.Write(
                        ConsoleMessageKind.Warning,
                        string.Format(ConsoleMessages.Get("debugRdaArchiveReadFailed"), rdaName, blockMessage),
                        always: true);
                }

                context.RdaArchiveReadFailures.Add(rdaName);
            }

            if (context.DebugMode && archiveDiagnostics is not null)
            {
                if (compactAnno1800Debug)
                {
                    if (stats.Extracted == 0)
                    {
                        LogArchiveCompact(context, rdaName, archiveDiagnostics, stats);
                    }
                    else
                    {
                        LogArchiveExtractedSummary(context, rdaName, archiveDiagnostics, stats);
                    }
                }
                else if (stats.Extracted > 0)
                {
                    LogArchiveInternals(context, archiveDiagnostics);
                    LogArchiveStatsSummary(context, rdaName, stats);
                }

                foreach (string line in pendingDebugLines)
                {
                    context.Log.Debug(line);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            stats = new RdaExtractStatistics();
            context.Log.Write(ConsoleMessageKind.Warning, string.Format(ConsoleMessages.Get("debugRdaArchiveReadFailed"), rdaName, ex.Message), always: true);
            // Remember the failure: later archives may now supply older copies of the same files,
            // so Phase 1 must not stamp this source_xml as a valid extraction for this build.
            context.RdaArchiveReadFailures.Add(rdaName);
            return false;
        }
    }
}
