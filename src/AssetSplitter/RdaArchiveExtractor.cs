using RDAExplorer;

namespace AssetProcessor;

/// <summary>Phase 1 RDA decompression: extracts core game XML from Anno 117 or Anno 1800 archives.</summary>
public static class RdaArchiveExtractor
{
    /// <summary>
    /// Extracts matching files from game RDA archives into <paramref name="outputFolder"/>.
    /// Anno 117 uses config.rda and shared_configs.rda; Anno 1800 iterates data*.rda in reverse order.
    /// </summary>
    public static void Extract(PipelineContext context, string gameRootPath, string extractFilter, string outputFolder, string gameType)
    {
        if (context.DebugMode)
            WriteDebugSearchPlan(context, gameRootPath, extractFilter, outputFolder, gameType);

        if (gameType.Contains("117", StringComparison.OrdinalIgnoreCase))
            ExtractAnno117(context, gameRootPath, extractFilter, outputFolder);
        else
            ExtractAnno1800(context, gameRootPath, extractFilter, outputFolder);

        if (context.DebugMode)
            WriteDebugOutputInventory(context, outputFolder);
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
            context.Log.Debug(ConsoleMessages.Get("debugRdaArchiveSearchOrderAnno1800"));
        }
    }

    private static void WriteDebugOutputInventory(PipelineContext context, string outputFolder)
    {
        if (!Directory.Exists(outputFolder))
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaOutputMissing"), outputFolder));
            return;
        }

        var files = Directory.GetFiles(outputFolder, "*", SearchOption.TopDirectoryOnly)
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
            Console.WriteLine(string.Format(ConsoleMessages.Get("phaseRdaAnno117"), total));
        else
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaStartingPhase"), "Anno 117", total));

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

            Directory.CreateDirectory(outputFolder);
            RdaExtractStatistics stats = RDAFileExtension.ExtractAll(
                rdaPath,
                outputFolder,
                extractFilter,
                bare: true,
                onArchiveAnalyzed: context.DebugMode ? diagnostics =>
                    LogArchiveInternals(context, diagnostics)
                    : null,
                onExtracted: context.DebugMode ? (archivePath, outputPath, bytes) =>
                    context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaExtractedFile"), archivePath, outputPath, bytes.ToString("N0")))
                    : null,
                onSkipped: context.DebugMode ? (archivePath, reason) =>
                {
                    if (reason.Contains("filter criteria", StringComparison.OrdinalIgnoreCase))
                        return;
                    context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaSkippedEntry"), archivePath, reason));
                }
                : null);

            if (context.DebugMode)
            {
                context.Log.Debug(string.Format(
                    ConsoleMessages.Get("debugRdaArchiveSummary"),
                    rdaFileName,
                    stats.TotalEntries.ToString("N0"),
                    stats.Extracted.ToString("N0"),
                    stats.SkippedChecksumOrMetadata.ToString("N0"),
                    stats.SkippedInvalidPath.ToString("N0"),
                    stats.SkippedFilterMismatch.ToString("N0")));
            }

            if (HasAnno117CoreFiles(context, outputFolder))
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaFoundCoreFiles"), rdaFileName));
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaEarlyStopReason"), DescribeAnno117CoreRequirements(context)));
                break;
            }

            if (context.DebugMode)
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaContinueSearch"), rdaFileName));
        }

        sw.Stop();

        if (!context.DebugMode)
            Console.WriteLine(string.Format(ConsoleMessages.Get("rdaExtractionCompleteShort"), processed, total, sw.Elapsed.ToString(@"mm\:ss")));
        else
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaFinishedPhase"), $"Anno 117 ({sw.Elapsed:mm\\:ss})"));
    }

    private static void ExtractAnno1800(PipelineContext context, string gameRootPath, string extractFilter, string outputFolder)
    {
        string mainDataPath = Path.Combine(gameRootPath, "maindata");
        string[] rdaFiles = AssetProcessorFileSystem.FileList(mainDataPath, "data*.rda");
        int totalRdas = rdaFiles.Length;
        int processed = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (!context.DebugMode)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("phaseRdaAnno1800"), totalRdas));
            Console.WriteLine(ConsoleMessages.Get("rdaLongRunningNote"));
        }
        else
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaStartingPhase"), "Anno 1800", totalRdas));
            context.Log.Debug(string.Format(
                ConsoleMessages.Get("debugRdaProcessingOrder"),
                string.Join(", ", rdaFiles.OrderByDescending(ParseDataRdaIndex).Select(Path.GetFileName))));
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

            Directory.CreateDirectory(outputFolder);
            RdaExtractStatistics stats = RDAFileExtension.ExtractAll(
                rdaPath,
                outputFolder,
                extractFilter,
                bare: true,
                onArchiveAnalyzed: context.DebugMode ? diagnostics =>
                    LogArchiveInternals(context, diagnostics)
                    : null,
                onExtracted: context.DebugMode ? (archivePath, outputPath, bytes) =>
                    context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaExtractedFile"), archivePath, outputPath, bytes.ToString("N0")))
                    : null,
                onSkipped: context.DebugMode ? (archivePath, reason) =>
                {
                    if (reason.Contains("filter criteria", StringComparison.OrdinalIgnoreCase))
                        return;
                    context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaSkippedEntry"), archivePath, reason));
                }
                : null);

            if (context.DebugMode)
            {
                context.Log.Debug(string.Format(
                    ConsoleMessages.Get("debugRdaArchiveSummary"),
                    rdaName,
                    stats.TotalEntries.ToString("N0"),
                    stats.Extracted.ToString("N0"),
                    stats.SkippedChecksumOrMetadata.ToString("N0"),
                    stats.SkippedInvalidPath.ToString("N0"),
                    stats.SkippedFilterMismatch.ToString("N0")));
            }

            bool hasRequiredAssetFiles = File.Exists(Path.Combine(outputFolder, "assets.xml"))
                && File.Exists(Path.Combine(outputFolder, "properties.xml"))
                && File.Exists(Path.Combine(outputFolder, "templates.xml"))
                && File.Exists(Path.Combine(outputFolder, "datasets.xml"));

            bool hasLanguageFiles = File.Exists(Path.Combine(outputFolder, "texts_english.xml"))
                || (!context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(Path.Combine(outputFolder, context.AssetLanguage)));

            if (hasRequiredAssetFiles && hasLanguageFiles)
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaFoundCoreFiles"), rdaName));
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaEarlyStopReason"), DescribeAnno1800CoreRequirements(context)));
                break;
            }

            if (context.DebugMode)
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaContinueSearch"), rdaName));
        }

        sw.Stop();

        if (!context.DebugMode)
            Console.WriteLine(string.Format(ConsoleMessages.Get("rdaExtractionCompleteNonDebug"), processed, totalRdas, sw.Elapsed.ToString(@"mm\:ss")));
        else
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaFinishedPhase"), $"Anno 1800 ({sw.Elapsed:mm\\:ss})"));
    }

    private static string DescribeAnno117CoreRequirements(PipelineContext context)
    {
        string lang = context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? "texts_english.xml or configured language file"
            : context.AssetLanguage;
        return $"assets.xml, properties.xml, templates.xml, datasets.xml, {lang}";
    }

    private static string DescribeAnno1800CoreRequirements(PipelineContext context)
    {
        string lang = context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? "texts_english.xml"
            : context.AssetLanguage;
        return $"assets.xml, properties.xml, templates.xml, datasets.xml, {lang}";
    }

    private static bool HasAnno117CoreFiles(PipelineContext context, string outputFolder)
    {
        bool hasLangFile = File.Exists(Path.Combine(outputFolder, "texts_english.xml"))
            || (!context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase)
                && File.Exists(Path.Combine(outputFolder, context.AssetLanguage)));

        return File.Exists(Path.Combine(outputFolder, "assets.xml"))
            && File.Exists(Path.Combine(outputFolder, "properties.xml"))
            && File.Exists(Path.Combine(outputFolder, "templates.xml"))
            && File.Exists(Path.Combine(outputFolder, "datasets.xml"))
            && hasLangFile;
    }

    private static int ParseDataRdaIndex(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (name.StartsWith("data", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(name.AsSpan(4), out int idx))
            return idx;
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

        foreach (RdaBlockDiagnostics block in diagnostics.Blocks)
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
}


