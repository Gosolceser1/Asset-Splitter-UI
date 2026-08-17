using RDAExplorer;

namespace AssetProcessor;

public static partial class RdaArchiveExtractor
{
    private static void WriteDebugSearchPlan(
        PipelineContext context,
        string gameRootPath,
        string extractFilter,
        string outputFolder,
        string gameType)
    {
        context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaGameRoot"), gameRootPath));
        context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaMainDataFolder"), Path.Combine(gameRootPath, "maindata")));
        context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaOutputFolder"), outputFolder));
        context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaBareExtractMode"), "true"));
        string languageRequirement = context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? "none (language file not required for this run)"
            : context.AssetLanguage;
        context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaAssetLanguage"), languageRequirement));

        string[] groups = extractFilter.Split(';', StringSplitOptions.RemoveEmptyEntries);
        context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaFilterCriteria"), groups.Length, extractFilter));

        for (int i = 0; i < groups.Length; i++)
        {
            string group = groups[i];
            string[] parts = group.Split('+', StringSplitOptions.RemoveEmptyEntries);
            string readable = string.Join(" AND ", parts.Select(p => p.StartsWith('!') ? $"NOT '{p[1..]}'" : $"'{p}'"));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaFilterGroup"), i + 1, readable));
        }

        if (gameType.Contains("117", StringComparison.OrdinalIgnoreCase))
        {
            context.Log.Debug(string.Format(
                ConsoleMessages.Get("debugRdaArchiveSearchOrder"),
                "config.rda, shared_configs.rda (stop when core files found)"));
        }
        else
        {
            context.Log.Debug(ConsoleMessages.Get("debugRdaArchiveSearchOrderAnno1800NewestWins"));
            context.Log.Debug(ConsoleMessages.Get("debugRdaAnno1800SkipExistingPolicy"));
        }

        string earlyStopFiles = string.Join(", ", GetRequiredBareFilesForEarlyStop(extractFilter, context));
        context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaEarlyStopRequirements"), earlyStopFiles));
    }

    private static void WriteDebugOutputInventory(PipelineContext context, string outputFolder)
    {
        if (!Directory.Exists(outputFolder))
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaOutputMissing"), outputFolder));
            return;
        }

        string[] files = Directory.GetFiles(outputFolder, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaOutputInventory"), files.Length, outputFolder));
        foreach (string file in files)
        {
            long bytes = new FileInfo(file).Length;
            context.Log.Debug(string.Format(
                ConsoleMessages.Get("debugRdaOutputFile"),
                Path.GetFileName(file),
                bytes.ToString("N0")));
        }
    }

    private static void LogArchiveCompact(
        PipelineContext context,
        string rdaName,
        RdaArchiveDiagnostics diagnostics,
        RdaExtractStatistics stats)
    {
        context.Log.Debug(string.Format(
            ConsoleMessages.Get("debugRdaArchiveCompact"),
            rdaName,
            diagnostics.FileSizeBytes.ToString("N0"),
            stats.TotalEntries.ToString("N0"),
            stats.SkippedExistingBare.ToString("N0"),
            stats.SkippedFilterMismatch.ToString("N0")));
    }

    private static void LogArchiveExtractedSummary(
        PipelineContext context,
        string rdaName,
        RdaArchiveDiagnostics diagnostics,
        RdaExtractStatistics stats)
    {
        context.Log.Debug(string.Format(
            ConsoleMessages.Get("debugRdaArchiveExtractedSummary"),
            rdaName,
            diagnostics.FileSizeBytes.ToString("N0"),
            stats.TotalEntries.ToString("N0"),
            stats.Extracted.ToString("N0"),
            stats.SkippedExistingBare.ToString("N0"),
            stats.SkippedChecksumOrMetadata.ToString("N0")));
    }

    private static void LogArchiveStatsSummary(PipelineContext context, string rdaName, RdaExtractStatistics stats)
    {
        context.Log.Debug(string.Format(
            ConsoleMessages.Get("debugRdaArchiveSummary"),
            rdaName,
            stats.TotalEntries.ToString("N0"),
            stats.Extracted.ToString("N0"),
            stats.SkippedChecksumOrMetadata.ToString("N0"),
            stats.SkippedInvalidPath.ToString("N0"),
            stats.SkippedFilterMismatch.ToString("N0"),
            stats.SkippedExistingBare.ToString("N0")));
    }

    private static void LogContinueSearch(PipelineContext context, string archiveName, IReadOnlyList<string> missingFiles)
    {
        string missingList = missingFiles.Count == 0
            ? "(unknown)"
            : string.Join(", ", missingFiles);
        context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaContinueSearch"), archiveName, missingList));
    }

    private static void ReportRdaPhaseProgressComplete(PipelineContext context, int archivesProcessed, string operation)
    {
        if (archivesProcessed <= 0)
        {
            return;
        }

        string count = archivesProcessed.ToString();
        context.ProgressReporter.ReportProgress(operation, count, count);
    }

    private static void LogArchiveInternals(PipelineContext context, RdaArchiveDiagnostics diagnostics)
    {
        context.Log.Debug(string.Format(
            ConsoleMessages.Get("debugRdaArchiveLayout"),
            diagnostics.FileSizeBytes.ToString("N0"),
            diagnostics.FirstBlockOffset.ToString("N0"),
            diagnostics.Blocks.Count.ToString("N0")));

        context.Log.Debug(string.Format(
            ConsoleMessages.Get("debugRdaArchiveInternals"),
            diagnostics.ArchiveFormat,
            diagnostics.BlocksRead.ToString("N0"),
            diagnostics.TotalEntries.ToString("N0"),
            diagnostics.CompressedEntries.ToString("N0"),
            diagnostics.EncryptedEntries.ToString("N0"),
            diagnostics.MemoryResidentEntries.ToString("N0"),
            diagnostics.DeletedEntries.ToString("N0")));

        context.Log.Debug(ConsoleMessages.Get("debugRdaEntryFlagScope"));

        var fileBearingBlocks = diagnostics.Blocks
            .Where(block => block.FileCount > 0)
            .ToList();

        if (fileBearingBlocks.Count == 0)
        {
            context.Log.Debug(ConsoleMessages.Get("debugRdaNoFileBearingBlocks"));
            return;
        }

        int blocksToLog = Math.Min(MaxBlockDetailsLogged, fileBearingBlocks.Count);
        context.Log.Debug(string.Format(
            ConsoleMessages.Get("debugRdaBlockDetailSummary"),
            diagnostics.Blocks.Count.ToString("N0"),
            fileBearingBlocks.Count.ToString("N0"),
            blocksToLog.ToString("N0")));

        for (int i = 0; i < blocksToLog; i++)
        {
            LogBlockDetail(context, fileBearingBlocks[i]);
        }

        int omitted = fileBearingBlocks.Count - blocksToLog;
        if (omitted > 0)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaBlockDetailTruncated"), omitted.ToString("N0")));
        }
    }

    private static void LogBlockDetail(PipelineContext context, RdaBlockDiagnostics block)
    {
        context.Log.Debug(string.Format(
            ConsoleMessages.Get("debugRdaBlockDetail"),
            block.Index + 1,
            block.Offset.ToString("N0"),
            $"0x{block.RawFlags:X}",
            block.IsCompressed,
            block.IsEncrypted,
            block.IsMemoryResident,
            block.IsDeleted,
            block.FileCount.ToString("N0"),
            block.DirectorySize.ToString("N0"),
            block.DecompressedDirectorySize.ToString("N0"),
            block.NextBlockOffset.ToString("N0")));
    }
}
