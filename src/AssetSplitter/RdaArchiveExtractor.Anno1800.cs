using RDAExplorer;

namespace AssetProcessor;

public static partial class RdaArchiveExtractor
{
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
            BackendI18nLine.Write("phaseRdaAnno1800", totalRdas);
            BackendI18nLine.Write("rdaLongRunningNote");
        }
        else
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaStartingPhase"), "Anno 1800", totalRdas));
            LogAnno1800ProcessingOrder(context, rdaFiles);
        }

        foreach (string rdaPath in rdaFiles.OrderByDescending(ParseDataRdaIndex))
        {
            context.ThrowIfCancellationRequested();
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
            context.ProgressReporter.ReportProgress(rdaProgressAnno1800, processed.ToString(), totalRdas.ToString());
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
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaFinishedPhase"), $"Anno 1800 ({PipelineRunSupport.FormatElapsed(sw.Elapsed)})"));
        }
        else
        {
            BackendI18nLine.Write("rdaExtractionCompleteNonDebug", processed, totalRdas, PipelineRunSupport.FormatElapsed(sw.Elapsed));
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
            PipelineRunSupport.FormatElapsed(elapsed),
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
}
