namespace AssetProcessor;

public static class FinalFormattingOrchestrator
{
    public static int Execute(PipelineContext context, string gameType, TimeSpan? totalElapsed = null)
    {
        bool modExportOnly = PipelineFeatureGates.IsModExportOnlyRun(context);
        string[] mainOutputFiles = AssetProcessorFileSystem.FileList(context.AssetOut);

        if (!modExportOnly)
        {
            Console.WriteLine();
            if (!context.DebugMode)
            {
                Console.Write(ConsoleMessages.Get("preparingFormatting"));
            }

            if (context.DebugMode)
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugFormatScanRoot"), context.AssetOut));
                context.Log.Write("FORMAT", string.Format(ConsoleMessages.Get("formatProcessingMainOutputFiles"), mainOutputFiles.Length.ToString("N0")));
            }
            else
            {
                Console.WriteLine(" " + string.Format(ConsoleMessages.Get("filesFound"), mainOutputFiles.Length.ToString("N0")));
            }

            FormattingService.FormatXml(context, mainOutputFiles, context.PropertyScan ?? PropertyScanner.Empty, gameType, true);

            string baseStaging = OutputDirectoryManager.GetBaseAssetGuidStagingPath(context);
            if (Directory.Exists(baseStaging))
            {
                Console.WriteLine();
                Console.WriteLine(ConsoleMessages.Get("preparingBaseAssetGuid"));
                string[] staged = AssetProcessorFileSystem.FileList(baseStaging);
                if (staged.Length > 0)
                {
                    if (context.DebugMode)
                    {
                        context.Log.Write("FORMAT", string.Format(ConsoleMessages.Get("formatProcessingBaseAssetGuidFilesNoMove"), staged.Length.ToString("N0")));
                    }
                    else
                    {
                        Console.WriteLine(string.Format(ConsoleMessages.Get("processingBaseAssetGuid"), staged.Length.ToString("N0")));
                    }

                    FormattingService.FormatXml(context, staged, context.PropertyScan ?? PropertyScanner.Empty, gameType, false, true);
                }

                OutputDirectoryManager.TryRemoveEmptyStagingDirectory(context);
            }

            AnnotateSplitTemplateFilesIfNeeded(context, gameType);
        }
        else if (!context.DebugMode)
        {
            Console.WriteLine();
            Console.WriteLine(string.Format(ConsoleMessages.Get("modExportBuildingPackages"), mainOutputFiles.Length.ToString("N0")));
        }

        if (context.CreateAssetMods)
        {
            if (!modExportOnly)
            {
                Console.WriteLine();
            }

            AssetModPackageExporter.Export(context, gameType);
            OutputDirectoryManager.TryRemoveIntermediateAssetOutputAfterModExport(context);
        }

        OutputDirectoryManager.TryDeleteFixerScratchFile(context.BaseOutputDir);

        Console.WriteLine();

        if (!context.DebugMode)
        {
            string baseStagingPath = OutputDirectoryManager.GetBaseAssetGuidStagingPath(context);
            int stagedCount = Directory.Exists(baseStagingPath)
                ? AssetProcessorFileSystem.FileList(baseStagingPath).Length
                : 0;
            int totalAssets = mainOutputFiles.Length + stagedCount;
            Console.WriteLine(ConsoleMessages.Get("extractionCompleteHeader"));
            Console.WriteLine(string.Format(ConsoleMessages.Get("processedAssetsSummary"), totalAssets.ToString("N0")));
            if (context.CreateAssetMods)
            {
                Console.WriteLine(ConsoleMessages.Get("assetModsCreatedNote"));
            }

            if (totalElapsed.HasValue)
            {
                Console.WriteLine(string.Format(ConsoleMessages.Get("totalTimeSummary"), totalElapsed.Value.ToString(@"mm\:ss")));
            }

            Console.WriteLine();
        }

        context.Log.Write("COMPLETE", ConsoleMessages.Get("assetExtractionSuccess"), always: true);
        return 0;
    }

    /// <summary>Phase 7: GUID comments on split template files (-c and --split-templates, after dictionaries exist).</summary>
    private static void AnnotateSplitTemplateFilesIfNeeded(PipelineContext context, string gameType)
    {
        if (!context.AssetComments || !context.AssetSplitTemplates || context.PropertyScan is null)
        {
            return;
        }

        string templateOutput = Path.Combine(context.GameOutputRoot, "output_templates_" + gameType);
        if (!Directory.Exists(templateOutput))
        {
            return;
        }

        string[] templateFiles = Directory.GetFiles(templateOutput, "*.xml");
        if (templateFiles.Length == 0)
        {
            return;
        }

        if (!context.DebugMode)
        {
            Console.WriteLine(ConsoleMessages.Get("annotatingTemplateComments"));
        }

        FormattingService.AnnotateFilesWithGuidComments(context, templateFiles, context.PropertyScan);
    }
}
