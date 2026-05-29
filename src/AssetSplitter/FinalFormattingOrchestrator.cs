namespace AssetProcessor;

public static class FinalFormattingOrchestrator
{
    public static int Execute(PipelineContext context, string gameType, TimeSpan? totalElapsed = null)
    {
        Console.WriteLine();
        if (!context.DebugMode)
            Console.Write(ConsoleMessages.Get("preparingFormatting"));
        string[] mainOutputFiles = AssetProcessorFileSystem.FileList(context.AssetOut);
        if (context.DebugMode)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugFormatScanRoot"), context.AssetOut));
            context.Log.Write("FORMAT", string.Format(ConsoleMessages.Get("formatProcessingMainOutputFiles"), mainOutputFiles.Length.ToString("N0")));
        }
        else
        {
            Console.WriteLine(" " + string.Format(ConsoleMessages.Get("filesFound"), mainOutputFiles.Length.ToString("N0")));
        }

        FormattingService.FormatXml(context, mainOutputFiles, context.PropertyScan!, gameType, true);

        string baseStaging = Path.Combine(context.AssetOut, "BaseAssetGUID");
        if (Directory.Exists(baseStaging))
        {
            Console.WriteLine();
            Console.WriteLine(ConsoleMessages.Get("preparingBaseAssetGuid"));
            string[] staged = AssetProcessorFileSystem.FileList(baseStaging);
            if (staged.Length > 0)
            {
                if (context.DebugMode)
                    context.Log.Write("FORMAT", string.Format(ConsoleMessages.Get("formatProcessingBaseAssetGuidFilesNoMove"), staged.Length.ToString("N0")));
                else
                    Console.WriteLine(string.Format(ConsoleMessages.Get("processingBaseAssetGuid"), staged.Length.ToString("N0")));
                FormattingService.FormatXml(context, staged, context.PropertyScan!, gameType, false, true);
            }
        }

        if (context.CreateAssetMods)
        {
            Console.WriteLine();
            AssetModPackageExporter.Export(context, gameType);
        }

        string fixerLogPath = Path.Combine(context.BaseOutputDir, "fixer.txt");
        if (File.Exists(fixerLogPath))
            File.Delete(fixerLogPath);

        Console.WriteLine();

        if (!context.DebugMode)
        {
            int totalAssets = mainOutputFiles.Length + (Directory.Exists(baseStaging) ? AssetProcessorFileSystem.FileList(baseStaging).Length : 0);
            Console.WriteLine(ConsoleMessages.Get("extractionCompleteHeader"));
            Console.WriteLine(string.Format(ConsoleMessages.Get("processedAssetsSummary"), totalAssets.ToString("N0")));
            if (context.CreateAssetMods)
                Console.WriteLine(ConsoleMessages.Get("assetModsCreatedNote"));
            if (totalElapsed.HasValue)
                Console.WriteLine(string.Format(ConsoleMessages.Get("totalTimeSummary"), totalElapsed.Value.ToString(@"mm\:ss")));
            Console.WriteLine();
        }

        context.Log.Write("COMPLETE", ConsoleMessages.Get("assetExtractionSuccess"), always: true);
        return 0;
    }
}
