using RDAExplorer;

namespace AssetProcessor;

/// <summary>Phase 1 RDA decompression: extracts core game XML from Anno 117 or Anno 1800 archives.</summary>
public static class RdaArchiveExtractor
{
    private const int MaxBlockDetailsLogged = 12;

    /// <summary>
    /// Extracts matching files from game RDA archives into <paramref name="outputFolder"/>.
    /// Anno 117 uses config.rda and shared_configs.rda; Anno 1800 iterates data*.rda in reverse order.
    /// </summary>
    public static void Extract(PipelineContext context, string gameRootPath, string extractFilter, string outputFolder, string gameType)
    {
        if (context.DebugMode)
        {
            WriteDebugSearchPlan(context, gameRootPath, extractFilter, outputFolder, gameType);
        }

        if (gameType.Contains("117", StringComparison.OrdinalIgnoreCase))
        {
            ExtractAnno117(context, gameRootPath, extractFilter, outputFolder);
        }
        else
        {
            ExtractAnno1800(context, gameRootPath, extractFilter, outputFolder);
        }

        if (context.DebugMode && gameType.Contains("117", StringComparison.OrdinalIgnoreCase))
        {
            WriteDebugOutputInventory(context, outputFolder);
        }
    }

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

    private static void ExtractAnno117(PipelineContext context, string gameRootPath, string extractFilter, string outputFolder)
    {
        string[] anno117RdaFiles = ["config.rda", "shared_configs.rda"];
        int total = anno117RdaFiles.Length;
        int processed = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (!context.DebugMode)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("phaseRdaAnno117"), total));
        }
        else
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaStartingPhase"), "Anno 117", total));
        }

        foreach (string rdaFileName in anno117RdaFiles)
        {
            string rdaPath = Path.Combine(gameRootPath, "maindata", rdaFileName);
            if (!File.Exists(rdaPath))
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaMissingArchive"), rdaPath));
                continue;
            }

            processed++;
            string rdaProgress = context.DebugMode
                ? ConsoleMessages.Get("extractingFromRda")
                : $"Extracting from RDA: {rdaFileName}";
            context.ProgressReporter.OutputFixer(rdaProgress, processed.ToString(), total.ToString());
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaProcessing"), rdaFileName, processed, total));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaArchivePath"), rdaPath));

            if (!TryExtractArchive(context, rdaPath, rdaFileName, outputFolder, extractFilter, out RdaExtractStatistics stats))
            {
                continue;
            }

            if (HasRequiredExtractFiles(context, outputFolder, extractFilter))
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaFoundCoreFiles"), rdaFileName));
                context.Log.Debug(string.Format(
                    ConsoleMessages.Get("debugRdaEarlyStopReason"),
                    DescribeRequiredFiles(extractFilter, context)));
                break;
            }

            if (context.DebugMode)
            {
                LogContinueSearch(context, rdaFileName, GetMissingRequiredFiles(context, outputFolder, extractFilter));
            }
        }

        sw.Stop();
        ReportRdaPhaseProgressComplete(context, processed, context.DebugMode
            ? ConsoleMessages.Get("extractingFromRda")
            : "Extracting from RDA: complete");

        if (!context.DebugMode)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("rdaExtractionCompleteShort"), processed, total, sw.Elapsed.ToString(@"mm\:ss")));
        }
        else
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaFinishedPhase"), $"Anno 117 ({sw.Elapsed:mm\\:ss})"));
        }
    }

    private static void ExtractAnno1800(PipelineContext context, string gameRootPath, string extractFilter, string outputFolder)
    {
        string mainDataPath = Path.Combine(gameRootPath, "maindata");
        string[] rdaFiles = AssetProcessorFileSystem.FileList(mainDataPath, "data*.rda");
        int totalRdas = rdaFiles.Length;
        int processed = 0;
        string? stoppedAtArchive = null;
        var fileProvenance = new Dictionary<string, (string Archive, long Bytes)>(StringComparer.OrdinalIgnoreCase);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (!context.DebugMode)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("phaseRdaAnno1800"), totalRdas));
            Console.WriteLine(ConsoleMessages.Get("rdaLongRunningNote"));
        }
        else
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaStartingPhase"), "Anno 1800", totalRdas));
            LogAnno1800ProcessingOrder(context, rdaFiles);
        }

        foreach (string rdaPath in rdaFiles.OrderByDescending(ParseDataRdaIndex))
        {
            if (!File.Exists(rdaPath))
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaMissingArchive"), rdaPath));
                continue;
            }

            processed++;
            string rdaName = Path.GetFileName(rdaPath);
            string rdaProgressAnno1800 = context.DebugMode
                ? ConsoleMessages.Get("extractingFromRda")
                : $"Extracting from RDA: {rdaName}";
            context.ProgressReporter.OutputFixer(rdaProgressAnno1800, processed.ToString(), totalRdas.ToString());
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaProcessing"), rdaName, processed, totalRdas));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaArchivePath"), rdaPath));

            if (!TryExtractArchive(
                    context,
                    rdaPath,
                    rdaName,
                    outputFolder,
                    extractFilter,
                    out RdaExtractStatistics stats,
                    skipExistingBareOutput: true,
                    compactAnno1800Debug: context.DebugMode,
                    fileProvenance: fileProvenance))
            {
                continue;
            }

            if (HasRequiredExtractFiles(context, outputFolder, extractFilter))
            {
                stoppedAtArchive = rdaName;
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaFoundCoreFiles"), rdaName));
                context.Log.Debug(string.Format(
                    ConsoleMessages.Get("debugRdaEarlyStopReason"),
                    DescribeRequiredFiles(extractFilter, context)));
                break;
            }

            if (context.DebugMode)
            {
                LogContinueSearch(context, rdaName, GetMissingRequiredFiles(context, outputFolder, extractFilter));
            }
        }

        sw.Stop();
        ReportRdaPhaseProgressComplete(context, processed, context.DebugMode
            ? ConsoleMessages.Get("extractingFromRda")
            : "Extracting from RDA: complete");

        if (context.DebugMode)
        {
            WriteDebugAnno1800PhaseSummary(context, processed, totalRdas, sw.Elapsed, stoppedAtArchive, fileProvenance);
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaFinishedPhase"), $"Anno 1800 ({sw.Elapsed:mm\\:ss})"));
        }
        else
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("rdaExtractionCompleteNonDebug"), processed, totalRdas, sw.Elapsed.ToString(@"mm\:ss")));
        }
    }

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
            context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("debugRdaArchiveReadFailed"), rdaName, ex.Message), always: true);
            return false;
        }
    }

    private static void LogAnno1800ProcessingOrder(PipelineContext context, string[] rdaFiles)
    {
        string?[] ordered = rdaFiles.OrderByDescending(ParseDataRdaIndex).Select(Path.GetFileName).ToArray();
        if (ordered.Length <= 8)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaProcessingOrder"), string.Join(", ", ordered)));
            return;
        }

        context.Log.Debug(string.Format(
            ConsoleMessages.Get("debugRdaProcessingOrderCompact"),
            ordered[0],
            ordered[^1],
            ordered.Length));
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

    private static void WriteDebugAnno1800PhaseSummary(
        PipelineContext context,
        int archivesScanned,
        int totalArchives,
        TimeSpan elapsed,
        string? stoppedAtArchive,
        Dictionary<string, (string Archive, long Bytes)> fileProvenance)
    {
        string stopNote = stoppedAtArchive is not null
            ? string.Format(ConsoleMessages.Get("debugRdaAnno1800StoppedAt"), stoppedAtArchive)
            : ConsoleMessages.Get("debugRdaAnno1800NoEarlyStop");

        context.Log.Debug(string.Format(
            ConsoleMessages.Get("debugRdaAnno1800PhaseSummary"),
            archivesScanned.ToString("N0"),
            totalArchives.ToString("N0"),
            elapsed.ToString(@"mm\:ss"),
            stopNote,
            fileProvenance.Count.ToString("N0")));

        if (fileProvenance.Count == 0)
        {
            return;
        }

        context.Log.Debug(ConsoleMessages.Get("debugRdaFileProvenanceHeader"));
        foreach (var entry in fileProvenance.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            context.Log.Debug(string.Format(
                ConsoleMessages.Get("debugRdaFileProvenanceLine"),
                entry.Key,
                entry.Value.Archive,
                entry.Value.Bytes.ToString("N0")));
        }
    }

    private static string DescribeRequiredFiles(string extractFilter, PipelineContext context) =>
        string.Join(", ", GetRequiredBareFilesForEarlyStop(extractFilter, context));

    private static bool HasRequiredExtractFiles(PipelineContext context, string outputFolder, string extractFilter) =>
        GetMissingRequiredFiles(context, outputFolder, extractFilter).Count == 0;

    private static List<string> GetMissingRequiredFiles(PipelineContext context, string outputFolder, string extractFilter)
    {
        var missing = new List<string>();
        foreach (string fileName in GetRequiredBareFilesForEarlyStop(extractFilter, context))
        {
            if (!File.Exists(Path.Combine(outputFolder, fileName)))
            {
                missing.Add(fileName);
            }
        }

        return missing;
    }

    private static IReadOnlyList<string> GetRequiredBareFilesForEarlyStop(string extractFilter, PipelineContext context)
    {
        var required = new List<string>();
        foreach (string group in extractFilter.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string path = group.Trim();
            if (path.EndsWith("texts_", StringComparison.Ordinal))
            {
                if (!required.Contains("texts_english.xml", StringComparer.OrdinalIgnoreCase))
                {
                    required.Add("texts_english.xml");
                }

                continue;
            }

            int slash = path.LastIndexOf('/');
            string fileName = slash >= 0 ? path[(slash + 1)..] : path;
            if (!fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!required.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            {
                required.Add(fileName);
            }
        }

        if (!context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase)
            && !required.Contains(context.AssetLanguage, StringComparer.OrdinalIgnoreCase))
        {
            required.Add(context.AssetLanguage);
        }

        return required;
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
        context.ProgressReporter.OutputFixer(operation, count, count);
    }

    private static int ParseDataRdaIndex(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (name.StartsWith("data", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(name.AsSpan(4), out int idx))
        {
            return idx;
        }

        return -1;
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
