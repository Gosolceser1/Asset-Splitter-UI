namespace AssetProcessor;

public static class FinalFormattingOrchestrator
{
    public static int Execute(PipelineContext context, string gameType, System.Diagnostics.Stopwatch? totalSw = null)
    {
        bool modExportOnly = PipelineFeatureGates.IsModExportOnlyRun(context);
        string[] mainOutputFiles = ResolveMainFilesForFormatting(context);
        string[] stagedFiles = ResolveStagedFilesForFormatting(context);

        if (!modExportOnly)
        {
            Console.WriteLine();
            if (context.DebugMode)
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugFormatScanRoot"), context.AssetOutputDir));
                context.Log.WriteLocalized(ConsoleMessageKind.Format, "formatProcessingMainOutputFiles", always: true, mainOutputFiles.Length.ToString("N0"));
            }
            else if (mainOutputFiles.Length > 0)
            {
                BackendI18nLine.Write("preparingFormattingWithCount", mainOutputFiles.Length.ToString("N0"));
            }

            if (mainOutputFiles.Length > 0)
            {
                FormattingService.FormatXml(context, mainOutputFiles, context.PropertyScan ?? PropertyScanner.Empty, gameType, true);
            }

            string baseStaging = OutputDirectoryManager.GetBaseAssetGuidStagingPath(context);
            if (stagedFiles.Length > 0 && Directory.Exists(baseStaging))
            {
                Console.WriteLine();
                BackendI18nLine.Write("preparingBaseAssetGuid");
                if (context.DebugMode)
                {
                    context.Log.WriteLocalized(ConsoleMessageKind.Format, "formatProcessingBaseAssetGuidFilesNoMove", always: true, stagedFiles.Length.ToString("N0"));
                }
                else
                {
                    BackendI18nLine.Write("processingBaseAssetGuid", stagedFiles.Length.ToString("N0"));
                }

                // Distinct progress verb so the GUI tracker finalizes the main "Processing"
                // bucket instead of overwriting it with the smaller staging totals.
                FormattingService.FormatXml(
                    context,
                    stagedFiles,
                    context.PropertyScan ?? PropertyScanner.Empty,
                    gameType,
                    allowMove: false,
                    skipInitialMessage: true,
                    progressVerb: "Formatting references");

                OutputDirectoryManager.TryRemoveEmptyStagingDirectory(context);
            }

            AnnotateSplitTemplateFilesIfNeeded(context, gameType);
        }
        else if (!context.DebugMode)
        {
            Console.WriteLine();
            BackendI18nLine.Write("modExportBuildingPackages", mainOutputFiles.Length.ToString("N0"));
        }

        // Flat XML is final here (formatted, or raw extract for mod-export-only). Log content-changed
        // diffs before mod export may delete intermediate output_xml.
        OutputRunCache.TryFlushFlatXmlContentChangedDiffs(context);

        if (context.CreateAssetMods)
        {
            if (!modExportOnly)
            {
                Console.WriteLine();
            }

            AssetModPackageExportResult modExport = AssetModPackageExporter.Export(context, gameType);
            // Only delete intermediate XML after a successful prepare; a failed prepare
            // used to return (0,0) and still wipe output_xml on mod-export-only runs.
            if (modExport.Prepared)
            {
                OutputDirectoryManager.TryRemoveIntermediateAssetOutputAfterModExport(context);
            }
        }

        OutputDirectoryManager.TryDeleteFixerScratchFile(context.BaseOutputDir);

        Console.WriteLine();

        bool hasErrors = context.Issues.HasErrors;

        // Stamp/inventory hashing can take minutes on large trees — do it before the
        // "extraction complete" summary so progress lines replace the last Annotating tick.
        if (!hasErrors)
        {
            OutputRunCache.WriteStampAndInventory(context, gameType, context.Log);
        }

        if (!context.DebugMode)
        {
            int stagedCount = stagedFiles.Length;
            int totalAssets = context.PreserveOutputDirectories && context.RepairPackageGuids is { Count: > 0 }
                ? Math.Max(mainOutputFiles.Length + stagedCount, context.RepairPackageGuids.Count)
                : mainOutputFiles.Length + stagedCount;
            BackendI18nLine.Write("extractionCompleteHeader");
            BackendI18nLine.Write("processedAssetsSummary", totalAssets.ToString("N0"));
            if (context.CreateAssetMods)
            {
                BackendI18nLine.Write("assetModsCreatedNote");
            }

            if (totalSw is not null)
            {
                // Read elapsed while the stopwatch is still running so formatting + stamp time is included.
                BackendI18nLine.Write("totalTimeSummary", PipelineRunSupport.FormatElapsed(totalSw.Elapsed));
                if (totalSw.IsRunning)
                {
                    totalSw.Stop();
                }
            }

            Console.WriteLine();
        }
        else if (totalSw is not null && totalSw.IsRunning)
        {
            totalSw.Stop();
        }

        if (!hasErrors)
        {
            if (Console.IsOutputRedirected)
            {
                Console.WriteLine(BackendI18nLine.Format("consoleMessages.successExtractionComplete"));
            }
            else
            {
                context.Log.WriteLocalized(ConsoleMessageKind.Complete, "assetExtractionSuccess", always: true);
            }
        }

        return hasErrors ? 1 : 0;
    }

    /// <summary>Phase 7: GUID comments on split template files (-c and --split-templates, after dictionaries exist).</summary>
    private static void AnnotateSplitTemplateFilesIfNeeded(PipelineContext context, string gameType)
    {
        if (!context.AddComments || !context.SplitTemplates || context.PropertyScan is null)
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

        if (context.PreserveOutputDirectories && !context.ForceResplitTemplates)
        {
            // Presence / DiffRepair of asset XML must not re-annotate every split template file.
            return;
        }

        if (!context.DebugMode)
        {
            BackendI18nLine.Write("annotatingTemplateComments");
        }

        FormattingService.AnnotateFilesWithGuidComments(context, templateFiles, context.PropertyScan);
    }

    private static string[] ResolveMainFilesForFormatting(PipelineContext context)
    {
        if (context.PreserveOutputDirectories && context.RepairOnlyGuids is not null)
        {
            // Empty set = mod-only repair: do not re-format the whole template tree.
            return context.RepairOutputFiles
                .Where(path => File.Exists(path) && !IsUnderBaseStaging(context, path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (context.PreserveOutputDirectories
            && context.RepairPackageGuids is { Count: > 0 }
            && context.RepairOnlyGuids is null)
        {
            return [];
        }

        return AssetProcessorFileSystem.FileList(context.AssetOutputDir);
    }

    private static string[] ResolveStagedFilesForFormatting(PipelineContext context)
    {
        if (context.PreserveOutputDirectories && context.RepairOnlyGuids is not null)
        {
            return context.RepairOutputFiles
                .Where(path => File.Exists(path) && IsUnderBaseStaging(context, path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (context.PreserveOutputDirectories
            && context.RepairPackageGuids is { Count: > 0 }
            && context.RepairOnlyGuids is null)
        {
            return [];
        }

        string baseStaging = OutputDirectoryManager.GetBaseAssetGuidStagingPath(context);
        return Directory.Exists(baseStaging) ? AssetProcessorFileSystem.FileList(baseStaging) : [];
    }

    private static bool IsUnderBaseStaging(PipelineContext context, string path)
    {
        // Honour the configured staging folder name (app_settings.json "baseasset_folder").
        string stagingFolderName = Path.GetFileName(OutputDirectoryManager.GetBaseAssetGuidStagingPath(context));
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/" + stagingFolderName + "/", StringComparison.OrdinalIgnoreCase);
    }
}
