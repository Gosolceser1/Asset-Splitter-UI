using System.Xml;

namespace AssetProcessor;

public static class PipelineOrchestrator
{
    #region Initialization & Setup

    public static int Run(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        ConsoleMessages.SetLanguage(AssetProcessorCommandLineOptions.GetConsoleLanguage(args));
        if (!Console.IsOutputRedirected)
        {
            AssetProcessorConsole.ShowStartupBanner(
                ConsoleMessages.Get("bannerTitle"),
                ConsoleMessages.Get("bannerSubtitle"));
        }

        if (AssetProcessorCommandLineOptions.IsHelpRequest(args))
        {
            HelpDisplay.Long();
            return 0;
        }

        if (args.Length < 3 && !args.Any(a => a.StartsWith("--", StringComparison.Ordinal)))
        {
            HelpDisplay.Short();
            return 1;
        }

        if (AssetProcessorCommandLineOptions.HasTemplateCommand(args))
        {
            int templateResult = HandleTemplateCommand(args);
            if (templateResult == 0)
            {
                return 0;
            }

            return templateResult;
        }

        if (args.Length < 3)
        {
            HelpDisplay.Short();
            return 1;
        }

        AssetProcessorCommandLineOptions options;
        try
        {
            options = AssetProcessorCommandLineOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            AssetProcessorConsole.WriteColoredMessage($"[ERROR] {ex.Message}", "ERROR");
            return 1;
        }

        var context = CreateContext(options);
        int initializationResult = InitializePipeline(context, options, out string? gameType, out string? gameOutputRoot, out bool autoTemplates);
        if (initializationResult != 0)
        {
            return initializationResult;
        }

        if (!context.DebugMode)
        {
            Console.WriteLine(ConsoleMessages.Get("pipelineStarting"));
            if (!Console.IsOutputRedirected)
            {
                WriteDetectedGameBuild(context);
            }
        }
        else
        {
            LogFlowStep(context, "Pipeline start -> validate paths/options -> run phases in strict order");
            WriteDetectedGameBuild(context);
        }

        var totalSw = System.Diagnostics.Stopwatch.StartNew();

        LogFlowStep(context, "Step 1: Phase 1 RDA extraction");
        int phase1Result = ExecutePhase1RdaExtraction(context, gameType, gameOutputRoot);
        if (phase1Result != 0)
        {
            return phase1Result;
        }

        if (context.AssetComments
            && context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            context.Log.Write("ERROR", ConsoleMessages.Get("commentsRequireLanguage"), always: true);
            return 1;
        }

        if (options.SourceExtractionOnly || PipelineFeatureGates.IsSourceExtractionOnlyRun(context))
        {
            LogFlowStep(context, "Step 2: validate Phase 1 output (source extraction only — no processing flags)");
            if (!File.Exists(Path.Combine(context.SourceXmlFolder, "assets.xml")))
            {
                context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("xmlFileNotFound"), "assets.xml"), always: true);
                return 1;
            }

            Console.WriteLine(ProcessingRunPolicy.Phase1OnlyMarker);
            if (!Console.IsOutputRedirected)
            {
                Console.WriteLine();
                context.Log.Write("OK", ConsoleMessages.Get("sourceExtractionComplete"), always: true);
                Console.WriteLine();
            }

            return FinishPipelineRun(context, totalSw, assetCount: 0, succeeded: true);
        }

        LogFlowStep(context, "Step 2: Phase 2 config + dictionaries + property scan");
        int phase2ResultFull = ExecutePhase2ConfigLoading(context);
        if (phase2ResultFull != 0)
        {
            return phase2ResultFull;
        }

        LogFlowStep(context, "Step 3: Phase 3 asset extraction");
        WritePhaseHeader(context, "phase3Label");
        int phase3Result = ExecutePhase3AssetExtraction(context, options.SingleAssetGuid, gameType, gameOutputRoot, out string[]? mainOutputFiles, out string[]? baseAssetGuidFiles);
        if (phase3Result != 0)
        {
            return phase3Result;
        }

        // Phase 3 is complete — plan only the work still ahead (phases 4–8).
        // Extraction progress is already in the UI tracker; including it again skews the bar.
        long guidIndexWork = PipelineFeatureGates.NeedsGuidIndex(context) ? mainOutputFiles.Length : 0;
        long templateMergeWork = PipelineFeatureGates.NeedsTemplateMerge(context) ? mainOutputFiles.Length : 0;
        long dependencyWork = context.AssetFix
            ? baseAssetGuidFiles.Count(file => !file.Contains("PaMSy", StringComparison.OrdinalIgnoreCase))
            : 0;
        long formattingWork = PipelineFeatureGates.NeedsXmlEnrichment(context)
            ? mainOutputFiles.Length + (context.AssetFix ? baseAssetGuidFiles.Length : 0)
            : 0;
        long templateAnnotateWork = context.AssetSplitTemplates && context.AssetComments
            ? CountSplitTemplateFiles(context, gameType)
            : 0;
        long assetModWork = context.CreateAssetMods
            ? mainOutputFiles.Length + baseAssetGuidFiles.Length
            : 0;
        long plannedWork = guidIndexWork
                         + templateMergeWork
                         + dependencyWork
                         + formattingWork
                         + templateAnnotateWork
                         + assetModWork;
        Console.WriteLine($"[PLAN] {plannedWork}");
        if (context.DebugMode)
        {
            context.Log.Debug(string.Format(
                ConsoleMessages.Get("debugWorkPlanAnnounced"),
                plannedWork.ToString("N0"),
                mainOutputFiles.Length.ToString("N0"),
                guidIndexWork.ToString("N0"),
                templateMergeWork.ToString("N0"),
                dependencyWork.ToString("N0"),
                formattingWork.ToString("N0"),
                assetModWork.ToString("N0")));
        }

        if (PipelineFeatureGates.IsSplitOnlyRun(context))
        {
            LogFlowStep(context, ConsoleMessages.Get("debugFlowSplitOnlyComplete"));
            return FinishPipelineRun(context, totalSw, mainOutputFiles.Length, succeeded: true);
        }

        #region Phases 4–8 — index, merge, dependencies, formatting, mod packages

        if (PipelineFeatureGates.NeedsGuidIndex(context))
        {
            LogFlowStep(context, "Step 4: Phase 4 GUID file index");
            WritePhaseHeader(context, "phase4GuidIndexLabel");
            if (context.DebugMode)
            {
                context.Log.Debug(ConsoleMessages.Get("debugPhaseGuidIndex"));
            }

            var guidSw = System.Diagnostics.Stopwatch.StartNew();
            context.GuidIndex = new GuidFileIndex();
            context.GuidIndex.Build(AssetProcessorFileSystem.CollectGuidIndexFilePaths(context), context);
            guidSw.Stop();

            if (context.DebugMode)
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugGuidIndexComplete"), guidSw.Elapsed.ToString(@"mm\:ss"), context.GuidIndex?.Count.ToString("N0") ?? "0"));
            }
            else
            {
                Console.WriteLine(string.Format(ConsoleMessages.Get("guidIndexComplete"), guidSw.Elapsed.ToString(@"mm\:ss")));
            }
        }
        else
        {
            LogFlowStep(context, ConsoleMessages.Get("debugFlowPhase4Skipped"));
        }

        if (PipelineFeatureGates.NeedsTemplateMerge(context))
        {
            LogFlowStep(context, "Step 5: Phase 5 template inheritance merge");
            WritePhaseHeader(context, "phase5TemplateMergeLabel");
            int phase5Result = TemplateMergeOrchestrator.Execute(context, gameType, mainOutputFiles);
            if (phase5Result != 0)
            {
                return phase5Result;
            }
        }
        else
        {
            LogFlowStep(context, ConsoleMessages.Get("debugFlowPhase5Skipped"));
        }

        if (context.AssetFix)
        {
            LogFlowStep(context, "Step 6: Phase 6 dependency resolution (-f)");
            WritePhaseHeader(context, "phase6DependencyResolutionLabel");
            DependencyResolutionOrchestrator.Execute(context, baseAssetGuidFiles);
            RefreshGuidFileIndex(context);
        }
        else
        {
            LogFlowStep(context, "Step 6: skipped (no -f flag)");
        }

        if (PipelineFeatureGates.NeedsFormatting(context))
        {
            totalSw.Stop();
            string formattingPhaseKey = PipelineFeatureGates.IsModExportOnlyRun(context)
                ? "phase8AssetModPackages"
                : "phase7FormattingLabel";
            LogFlowStep(context, PipelineFeatureGates.IsModExportOnlyRun(context)
                ? "Step 7: Phase 8 mod packages (mod-export-only)"
                : "Step 7: Phase 7 formatting (+ Phase 8 mod packages when enabled)");
            WritePhaseHeader(context, formattingPhaseKey);
            int formattingResult = FinalFormattingOrchestrator.Execute(context, gameType, totalSw.Elapsed);
            PipelineIssueReporter.WriteSummary(context, formattingResult == 0);
            return formattingResult;
        }

        LogFlowStep(context, ConsoleMessages.Get("debugFlowPhase7Skipped"));
        return FinishPipelineRun(context, totalSw, mainOutputFiles.Length, succeeded: true);

        #endregion
    }

    private static PipelineContext CreateContext(AssetProcessorCommandLineOptions options)
    {
        bool singleAssetMode = !string.IsNullOrWhiteSpace(options.SingleAssetGuid);
        var context = new PipelineContext
        {
            AssetRoot = options.AssetRoot,
            BaseOutputDir = options.BaseOutputDir,
            AssetLanguage = options.AssetLanguage,
            ReadmeLanguage = options.ReadmeLanguage,
            CustomFixlistFile = options.CustomFixlistFile,
            AssetComments = options.AssetComments,
            AssetFix = options.AssetFix,
            AssetTemplates = singleAssetMode ? false : options.AssetTemplates,
            AssetModOpsWrap = options.AssetModOpsWrap,
            AssetNoDefaultProperties = options.AssetNoDefaultProperties,
            AssetSplitTemplates = !singleAssetMode && options.AssetSplitTemplates,
            CreateAssetMods = options.CreateAssetMods,
            DebugMode = options.DebugMode,
            SingleAssetGuid = options.SingleAssetGuid
        };
        context.ProgressReporter.Initialize(context);
        return context;
    }

    private static int InitializePipeline(
        PipelineContext context,
        AssetProcessorCommandLineOptions options,
        out string gameType,
        out string gameOutputRoot,
        out bool autoTemplates)
    {
        autoTemplates = options.AutoTemplates;
        if (context.DebugMode)
        {
            WriteDebugArguments(context, Environment.GetCommandLineArgs(), options);
        }

        gameType = GameTypeDetector.DetectFromPath(context.AssetRoot);
        if (gameType.Equals(GameTypeDetector.UnknownAnno, StringComparison.OrdinalIgnoreCase))
        {
            context.Log.Write("ERROR", ConsoleMessages.Get("gameNotDetected"), always: true);
            context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("gameDirectoryNotFound"), context.AssetRoot), always: true);
            gameOutputRoot = "";
            return 1;
        }

        context.DetectedGameType = gameType;
        context.DetectedGameBuild = GameBuildDetector.TryDetect(context.AssetRoot, gameType);
        context.AutoTemplates = autoTemplates;
        context.CustomTemplateFile = options.CustomTemplateFile;
        if (!OutputDirectoryManager.EnsureGameDirectoryExists(context))
        {
            gameOutputRoot = "";
            return 1;
        }

        if (!OutputDirectoryManager.TryResolveOutputLayout(context, gameType, out gameOutputRoot))
        {
            return 1;
        }

        return 0;
    }

    #endregion

    #region Phase 1 - RDA Extraction

    private static int HandleTemplateCommand(string[] args)
    {
        string gamePath = args[0];
        string detectedGameType = GameTypeDetector.DetectFromPath(gamePath);

        if (args.Any(arg => arg.Equals("--compare-templates", StringComparison.Ordinal)))
        {
            TemplateExtractor.CompareTemplates(gamePath, detectedGameType);
            return 0;
        }

        int count = TemplateExtractor.ExtractAndUpdateTemplates(gamePath, detectedGameType);
        if (count > 0)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("templateUpdateSucceeded"), count));
            return 0;
        }

        Console.WriteLine(ConsoleMessages.Get("templateUpdateFailed"));
        return 1;
    }

    private static void WriteDebugArguments(PipelineContext context, string[] args, AssetProcessorCommandLineOptions options)
    {
        if (Console.IsOutputRedirected)
        {
            context.Log.Debug(string.Format(
                ConsoleMessages.Get("debugStartupFromGui"),
                options.AssetRoot,
                options.BaseOutputDir,
                context.AssetLanguage,
                AssetProcessorCommandLineOptions.GetConsoleLanguage(Environment.GetCommandLineArgs()),
                FormatDebugFlags(context)));
            if (!string.IsNullOrEmpty(options.SingleAssetGuid))
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugExtractedGuid"), options.SingleAssetGuid));
            }

            return;
        }

        context.Log.Debug(string.Format(ConsoleMessages.Get("debugTotalArguments"), args.Length));
        for (int i = 0; i < args.Length; i++)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugArgumentValue"), i, args[i]));
        }

        context.Log.Debug(string.Format(
            ConsoleMessages.Get("debugFlagsApplied"),
            context.AssetComments,
            context.AssetFix,
            context.AssetTemplates,
            context.AssetModOpsWrap,
            context.AssetSplitTemplates,
            context.CreateAssetMods));
        if (!string.IsNullOrEmpty(options.SingleAssetGuid))
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugExtractedGuid"), options.SingleAssetGuid));
        }
    }

    private static string FormatDebugFlags(PipelineContext context) =>
        string.Join(' ', new[]
        {
            context.AssetComments ? "-c" : null,
            context.AssetFix ? "-f" : null,
            context.AssetTemplates ? "-t" : null,
            context.DebugMode ? "-d" : null,
            context.AssetModOpsWrap ? null : "--no-modops-wrap",
            context.AssetNoDefaultProperties ? "--no-default-properties" : null,
            context.AssetSplitTemplates ? "--split-templates" : null,
            context.CreateAssetMods ? "--create-asset-mods" : null,
        }.Where(static flag => flag is not null));

    private static int ExecutePhase1RdaExtraction(
        PipelineContext context,
        string gameType,
        string gameOutputRoot)
    {
        bool directSourceFilesAvailable = HasDirectSourceFiles(context);
        if (!directSourceFilesAvailable && Directory.Exists(context.AssetRoot))
        {
            Console.WriteLine();
            if (context.DebugMode)
            {
                context.Log.Write("PHASE", ConsoleMessages.Get("phase1Label"));
                context.Log.Write("RDA", ConsoleMessages.Get("extractingFromRdaCompressed"));
                context.Log.Write("INFO", ConsoleMessages.Get("rdaFilesContainAssets"));
            }
            else
            {
                // New improved phase headers are printed inside RdaArchiveExtractor
                Console.WriteLine($"\n{ConsoleMessages.Get("phase1Label")}");
                Console.WriteLine($"{ConsoleMessages.Get("extractingGameData")}");
            }
            string sourceXmlPath = Path.Combine(gameOutputRoot, "source_xml_" + gameType);
            if (!OutputDirectoryManager.TryPrepareSourceXmlDirectory(context, sourceXmlPath))
            {
                return 1;
            }

            context.SourceXmlFolder = sourceXmlPath + Path.DirectorySeparatorChar;
            try
            {
                if (gameType.Contains("117", StringComparison.OrdinalIgnoreCase))
                {
                    if (context.DebugMode)
                    {
                        context.Log.Write("RDA", ConsoleMessages.Get("rdaAnno117Detected"));
                        context.Log.Write("INFO", ConsoleMessages.Get("rdaExtractingAnno117CoreFiles"));
                    }
                    RdaArchiveExtractor.Extract(context, context.AssetRoot, PipelineFeatureGates.GetRdaExtractFilter(gameType), sourceXmlPath, gameType);
                }
                else
                {
                    if (context.DebugMode)
                    {
                        context.Log.Write("RDA", ConsoleMessages.Get("rdaAnno1800Detected"));
                        context.Log.Write("INFO", ConsoleMessages.Get("rdaExtractingAnno1800CoreFiles"));
                    }
                    RdaArchiveExtractor.Extract(context, context.AssetRoot, PipelineFeatureGates.GetRdaExtractFilter(gameType), sourceXmlPath, gameType);
                }
                if (context.DebugMode)
                {
                    context.Log.Write("OK", ConsoleMessages.Get("rdaDecompressionSuccessful"));
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("rdaExtractionFailed"), ex.Message), always: true);
                return 1;
            }
        }
        else
        {
            string sourceXmlDirectory = Path.Combine(gameOutputRoot, "source_xml_" + gameType);
            if (!OutputDirectoryManager.TryPrepareSourceXmlDirectory(context, sourceXmlDirectory))
            {
                return 1;
            }

            foreach (string filename in SourceXmlCatalog.GetExpectedFileNames(gameType, context.AssetLanguage)
                         .Concat(SourceXmlCatalog.GetOptionalFileNames(gameType)))
            {
                string sourceFile = Path.Combine(context.AssetRoot, filename);
                string destFile = Path.Combine(sourceXmlDirectory, filename);
                if (!File.Exists(sourceFile))
                {
                    continue;
                }

                try
                {
                    File.Copy(sourceFile, destFile, true);
                }
                catch (Exception ex)
                {
                    context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("couldNotCopyToSourceXml"), filename, ex.Message), always: true);
                }
            }
            context.SourceXmlFolder = sourceXmlDirectory + Path.DirectorySeparatorChar;
            context.Log.Write("OK", string.Format(ConsoleMessages.Get("sourceFilesCopied"), sourceXmlDirectory), always: true);
        }
        return ValidatePhase1SourceFiles(context, gameType) ? 0 : 1;
    }

    private static bool HasDirectSourceFiles(PipelineContext context)
    {
        if (!File.Exists(Path.Combine(context.AssetRoot, "assets.xml"))
            || !File.Exists(Path.Combine(context.AssetRoot, "properties.xml")))
        {
            return false;
        }

        return context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase)
            || File.Exists(Path.Combine(context.AssetRoot, context.AssetLanguage));
    }

    private static bool ValidatePhase1SourceFiles(PipelineContext context, string gameType)
    {
        string[] missingFiles =
        [
            ..SourceXmlCatalog.GetExpectedFileNames(gameType, context.AssetLanguage)
                .Where(fileName => !File.Exists(Path.Combine(context.SourceXmlFolder, fileName)))
        ];

        foreach (string missingFile in missingFiles)
        {
            context.Log.Write(
                "ERROR",
                string.Format(ConsoleMessages.Get("xmlFileNotFound"), Path.Combine(context.SourceXmlFolder, missingFile)),
                always: true);
        }

        return missingFiles.Length == 0;
    }

    #endregion

    #region Phase 2 - Config Processing

    private static int ExecutePhase2ConfigLoading(PipelineContext context)
    {
        if (!File.Exists(Path.Combine(context.SourceXmlFolder, "assets.xml")))
        {
            context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("xmlFileNotFound"), "assets.xml"), always: true);
            return 1;
        }
        if (!context.AssetLanguage.Equals("none") && !File.Exists(Path.Combine(context.SourceXmlFolder, context.AssetLanguage)))
        {
            context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("xmlFileNotFound"), Path.Combine(context.SourceXmlFolder, context.AssetLanguage)), always: true);
            return 1;
        }
        Console.WriteLine("");
        if (context.AssetComments
            && context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            context.Log.Write("ERROR", ConsoleMessages.Get("commentsRequireLanguage"), always: true);
            return 1;
        }

        WritePhaseHeader(context, "phase2Label");
        LoadProcessingPrerequisites(context);

        int splitTemplatesResult = ExecutePhase2SplitTemplates(context, context.DetectedGameType);
        if (splitTemplatesResult != 0)
        {
            return splitTemplatesResult;
        }

        if (context.DebugMode)
        {
            context.Log.Write("CONFIG", ConsoleMessages.Get("loadingGameTemplatesConfig"));
            context.Log.Write("INFO", ConsoleMessages.Get("convertingRawXmlToMods"));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugPhase2SourceXml"), context.SourceXmlFolder));
        }

        if (!context.AssetLanguage.Equals("none"))
        {
            var phase2Sw = System.Diagnostics.Stopwatch.StartNew();
            TranslationDictionaryLoader.Load(context);
            TextMetadataDictionaryLoader.Load(context);
            AssetNameRegistry.Load(context);
            context.LineIdContext = LineIdContextIndex.TryBuild(context);
            if (PipelineFeatureGates.NeedsPropertyScan(context))
            {
                context.PropertyScan = PropertyScanner.Scan(context);
            }

            phase2Sw.Stop();

            context.LoadedPropertyNames = context.PropertyScan?.EligibleProperties ?? [];

            if (context.DebugMode)
            {
                context.Log.Write("READY", ConsoleMessages.Get("configurationLoadedReady"));
            }

            if (!context.DebugMode)
            {
                Console.WriteLine(string.Format(ConsoleMessages.Get("configLoadedIn"), phase2Sw.Elapsed.ToString(@"mm\:ss")));
            }
        }
        else
        {
            if (PipelineFeatureGates.NeedsPropertyScan(context))
            {
                context.PropertyScan = PropertyScanner.Scan(context);
                context.LoadedPropertyNames = context.PropertyScan.EligibleProperties;
            }
            else
            {
                context.LoadedPropertyNames = [];
            }
        }

        Console.WriteLine();
        return 0;
    }

    /// <summary>JSON config + template list bootstrap. Belongs in Phase 2 for every path that reaches processing (RDA or copy).</summary>
    private static void LoadProcessingPrerequisites(PipelineContext context)
    {
        AssetProcessorConfiguration.LoadRegionalIngredientsConfig(context);
        AssetProcessorConfiguration.LoadAppSettingsConfig(context);
        context.AssetTemplatesList = TemplateBootstrapService.BootstrapTemplates(
            context,
            context.DetectedGameType,
            context.CustomTemplateFile,
            context.AutoTemplates);
    }

    #endregion

    #region Phase 3 - Asset Extraction

    private static int ExecutePhase3AssetExtraction(
        PipelineContext context,
        string singleAssetGuid,
        string gameType,
        string gameOutputRoot,
        out string[] mainOutputFiles,
        out string[] baseAssetGuidFiles)
    {
        mainOutputFiles = [];
        baseAssetGuidFiles = [];

        if (!OutputDirectoryManager.TryPrepareAssetOutputDirectory(context))
        {
            return 1;
        }

        XmlDocument xmlSourceFile = new();
        xmlSourceFile.LoadXml(File.ReadAllText(Path.Combine(context.SourceXmlFolder, "assets.xml"), Encoding.UTF8));
        if (xmlSourceFile.DocumentElement == null)
        {
            context.Log.Write("ERROR", ConsoleMessages.Get("assetsXmlDocumentElementLoadFailed"), always: true);
            return 1;
        }
        AssetExtractor.ExtractAssets(context, xmlSourceFile, singleAssetGuid, gameType);
        Console.WriteLine();

        if (!string.IsNullOrEmpty(singleAssetGuid))
        {
            if (!Directory.EnumerateFiles(context.AssetOut, "*.xml", SearchOption.AllDirectories).Any())
            {
                TryCleanEmptySingleAssetOutput(context);
                return 1;
            }
            else
            {
                string normalizedGameType = GameTypeDetector.IsAnno117(gameType)
                    ? GameTypeDetector.Anno117
                    : GameTypeDetector.Anno1800;
                RenameSingleAssetOutputFolder(context, normalizedGameType);
            }
        }

        mainOutputFiles = AssetProcessorFileSystem.FileList(context.AssetOut);
        string baseAssetGuidDir = OutputDirectoryManager.GetBaseAssetGuidStagingPath(context);
        baseAssetGuidFiles = Directory.Exists(baseAssetGuidDir) ? AssetProcessorFileSystem.FileList(baseAssetGuidDir) : [];
        if (!string.IsNullOrEmpty(singleAssetGuid) && mainOutputFiles.Length == 0 && baseAssetGuidFiles.Length == 0)
        {
            return 1;
        }

        return 0;
    }

    #endregion

    /// <summary>Phase 2B: split source templates.xml (--split-templates). Only needs Phase 1 source_xml.</summary>
    private static int ExecutePhase2SplitTemplates(PipelineContext context, string gameType)
    {
        if (!context.AssetSplitTemplates)
        {
            return 0;
        }

        WritePhaseHeader(context, "phase3SplitTemplatesLabel");
        if (!OutputDirectoryManager.TryPrepareTemplateSplitDirectory(context, gameType))
        {
            return 1;
        }

        if (!context.DebugMode)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("splittingTemplates"), gameType));
        }

        TemplateSplitterService.SplitTemplatesIntoFolders(context, context.SourceXmlFolder, context.GameOutputRoot, gameType);
        Console.WriteLine();
        return 0;
    }

    private static long CountSplitTemplateFiles(PipelineContext context, string gameType)
    {
        string templateDir = Path.Combine(context.GameOutputRoot, "output_templates_" + gameType);
        return Directory.Exists(templateDir) ? Directory.GetFiles(templateDir, "*.xml").Length : 0;
    }

    private static void TryCleanEmptySingleAssetOutput(PipelineContext context)
    {
        try
        {
            if (Directory.Exists(context.SingleAssetOutputRoot))
            {
                Directory.Delete(context.SingleAssetOutputRoot, true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugCleanSingleGuidOutputFailed"), ex.Message));
        }
    }

    private static void RenameSingleAssetOutputFolder(PipelineContext context, string normalizedGameType)
    {
        // After single GUID extraction, read the display name from the output XML file
        // and rename the output folder to include the asset name.
        string assetName = "Unknown Asset";
        try
        {
            string[] outputFiles = Directory.GetFiles(context.AssetOut, "*.xml", SearchOption.AllDirectories);
            if (outputFiles.Length > 0)
            {
                string fileName = Path.GetFileNameWithoutExtension(outputFiles[0]);
                int bracketStart = fileName.IndexOf(" - [", StringComparison.Ordinal);
                if (bracketStart >= 0)
                {
                    int nameStart = bracketStart + 4;
                    int nameEnd = fileName.LastIndexOf(']');
                    if (nameEnd > nameStart)
                    {
                        assetName = fileName[nameStart..nameEnd].Trim();
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugReadAssetNameFromOutputFailed"), ex.Message));
        }

        context.SingleAssetDisplayName = assetName;
        string safeGuid = AssetTextSanitizer.SanitizeFileNamePart(context.SingleAssetGuid, 40);
        string safeName = AssetTextSanitizer.SanitizeFileNamePart(assetName, 80);
        string newFolderName = $"{safeGuid} - {safeName}";

        string gameOutputRoot = context.GameOutputRoot;
        string singleGuidXmlRoot = Path.Combine(gameOutputRoot, "single_guid_output_xml_" + normalizedGameType);
        string newXmlPath = Path.Combine(singleGuidXmlRoot, newFolderName);
        string newModPath = Path.Combine(gameOutputRoot, "single_guid_mods", newFolderName);

        if (!newXmlPath.Equals(context.SingleAssetOutputRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(context.SingleAssetOutputRoot))
            {
                if (Directory.Exists(newXmlPath))
                {
                    Directory.Delete(newXmlPath, true);
                }

                if (!OutputDirectoryManager.EnsureSingleGuidXmlContainer(context, normalizedGameType))
                {
                    return;
                }

                Directory.Move(context.SingleAssetOutputRoot, newXmlPath);
            }
            context.SingleAssetOutputRoot = newXmlPath;
            context.AssetOut = newXmlPath;
        }

        context.SingleAssetModOutputRoot = newModPath;
        if (!string.IsNullOrWhiteSpace(context.SingleAssetDisplayName))
        {
            context.Log.Write("SINGLE", string.Format(ConsoleMessages.Get("singleGuidAsset"), context.SingleAssetGuid, context.SingleAssetDisplayName), always: true);
            context.Log.Write("INFO", string.Format(ConsoleMessages.Get("singleGuidXmlOutput"), newXmlPath), always: true);
        }
    }

    private static void RefreshGuidFileIndex(PipelineContext context)
    {
        if (!context.AssetFix)
        {
            return;
        }

        context.GuidIndex = new GuidFileIndex();
        context.GuidIndex.Build(AssetProcessorFileSystem.CollectGuidIndexFilePaths(context), context);
        if (context.DebugMode)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugGuidIndexRefreshedAfterDeps"), context.GuidIndex.Count.ToString("N0")));
        }
    }

    private static void WritePhaseHeader(PipelineContext context, string messageKey)
    {
        string label = ConsoleMessages.Get(messageKey);
        if (context.DebugMode)
        {
            context.Log.Write("PHASE", label);
        }
        else
        {
            Console.WriteLine($"\n{label}");
        }
    }

    private static void WriteDetectedGameBuild(PipelineContext context)
    {
        if (context.DetectedGameBuild is null)
        {
            return;
        }

        string message = string.Format(ConsoleMessages.Get("gameBuildDetected"), context.DetectedGameBuild.ToDisplayString());
        if (context.DebugMode)
        {
            context.Log.Write("INFO", message, always: true);
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    private static void LogFlowStep(PipelineContext context, string step)
    {
        if (!context.DebugMode)
        {
            return;
        }

        context.Log.Debug(string.Format(ConsoleMessages.Get("debugFlowStep"), step));
    }

    private static int FinishPipelineRun(PipelineContext context, System.Diagnostics.Stopwatch totalSw, int assetCount, bool succeeded)
    {
        if (totalSw.IsRunning)
        {
            totalSw.Stop();
        }

        OutputDirectoryManager.TryDeleteFixerScratchFile(context.BaseOutputDir);

        if (!context.DebugMode)
        {
            Console.WriteLine();
            Console.WriteLine(ConsoleMessages.Get("extractionCompleteHeader"));
            Console.WriteLine(string.Format(ConsoleMessages.Get("processedAssetsSummary"), assetCount.ToString("N0")));
            Console.WriteLine(string.Format(ConsoleMessages.Get("totalTimeSummary"), totalSw.Elapsed.ToString(@"mm\:ss")));
            Console.WriteLine();
        }
        else
        {
            context.Log.Write("COMPLETE", ConsoleMessages.Get("extractionCompleteHeader").Trim(), always: true);
            context.Log.Write("INFO", string.Format(ConsoleMessages.Get("processedAssetsSummary"), assetCount.ToString("N0")), always: true);
            context.Log.Write("INFO", string.Format(ConsoleMessages.Get("totalTimeSummary"), totalSw.Elapsed.ToString(@"mm\:ss")), always: true);
        }

        context.Log.Write("COMPLETE", ConsoleMessages.Get("assetExtractionSuccess"), always: true);
        PipelineIssueReporter.WriteSummary(context, succeeded);
        return succeeded ? 0 : 1;
    }
}
