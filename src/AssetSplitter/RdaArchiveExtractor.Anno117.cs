using RDAExplorer;

namespace AssetProcessor;

public static partial class RdaArchiveExtractor
{
    private static void ExtractAnno117(PipelineContext context, string gameRootPath, string extractFilter, string outputFolder)
    {
        string[] anno117RdaFiles = ["config.rda", "shared_configs.rda"];
        int total = anno117RdaFiles.Length;
        int processed = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (!context.DebugMode)
        {
            BackendI18nLine.Write("phaseRdaAnno117", total);
        }
        else
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaStartingPhase"), "Anno 117", total));
        }

        foreach (string rdaFileName in anno117RdaFiles)
        {
            context.ThrowIfCancellationRequested();
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
            context.ProgressReporter.ReportProgress(rdaProgress, processed.ToString(), total.ToString());
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
            BackendI18nLine.Write("rdaExtractionCompleteShort", processed, total, PipelineRunSupport.FormatElapsed(sw.Elapsed));
        }
        else
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugRdaFinishedPhase"), $"Anno 117 ({PipelineRunSupport.FormatElapsed(sw.Elapsed)})"));
        }
    }
}
