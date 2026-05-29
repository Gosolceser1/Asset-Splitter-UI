using System.Xml;

namespace AssetProcessor;

public static class PipelineOrchestrator
{
    private const string Anno1800RdaPaths = "data/config/export/main/asset/properties.xml;data/config/export/main/asset/properties-toolone.xml;data/config/export/main/asset/templates.xml;data/config/export/main/asset/assets.xml;data/config/export/main/asset/datasets.xml;data/config/gui/texts_";
    private const string Anno117RdaPaths = "data/base/config/export/properties.xml;data/base/config/export/properties-meta.xml;data/base/config/export/templates.xml;data/base/config/export/assets.xml;data/base/config/game/datasets.xml;data/base/config/export/audio_generated.xml;data/base/config/gui/texts_";
    #region Initialization & Setup

    public static int Run(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        ConsoleMessages.SetLanguage(AssetProcessorCommandLineOptions.GetConsoleLanguage(args));
        if (!Console.IsOutputRedirected)
            AssetProcessorConsole.ShowStartupBanner(
                ConsoleMessages.Get("bannerTitle"),
                ConsoleMessages.Get("bannerSubtitle"));

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
                return 0;
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
        int initializationResult = InitializePipeline(context, options, out var gameType, out var gameOutputRoot, out var autoTemplates);
        if (initializationResult != 0) return initializationResult;

        if (!context.DebugMode)
            Console.WriteLine(ConsoleMessages.Get("pipelineStarting"));
        else
            LogFlowStep(context, "Pipeline start -> validate paths/options -> run phases in strict order");

        var totalSw = System.Diagnostics.Stopwatch.StartNew();

        LogFlowStep(context, "Step 1: Phase 1 RDA extraction");
        int phase1Result = ExecutePhase1RdaExtraction(context, gameType, gameOutputRoot, autoTemplates, options.CustomTemplateFile);
        if (phase1Result != 0) return phase1Result;

        LogFlowStep(context, "Step 2: Phase 2 config + dictionaries + property scan");
        int phase2Result = ExecutePhase2ConfigLoading(context);
        if (phase2Result != 0) return phase2Result;

        if (context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            LogFlowStep(context, "Phase-1-only run complete (asset language = none). Stop after extraction.");
            return 0;
        }

        LogFlowStep(context, "Step 3: Phase 3 asset extraction");
        WritePhaseHeader(context, "phase3Label");
        int phase3Result = ExecutePhase3AssetExtraction(context, options.SingleAssetGuid, gameType, gameOutputRoot, out var mainOutputFiles, out var baseAssetGuidFiles);
        if (phase3Result != 0) return phase3Result;

        // After extraction, we know the full scope of remaining work.
        // Emit a plan line so the UI can show accurate determinate progress.
        long extractedWork = mainOutputFiles.Length + baseAssetGuidFiles.Length;
        long guidIndexWork = mainOutputFiles.Length;
        long templateMergeWork = mainOutputFiles.Length;
        long dependencyWork = context.AssetFix
            ? baseAssetGuidFiles.Count(file => !file.Contains("PaMSy", StringComparison.Ordinal))
            : 0;
        long formattingWork = mainOutputFiles.Length + baseAssetGuidFiles.Length;
        long assetModWork = context.CreateAssetMods
            ? mainOutputFiles.Length + baseAssetGuidFiles.Length
            : 0;
        long plannedWork = extractedWork
                         + guidIndexWork
                         + templateMergeWork
                         + dependencyWork
                         + formattingWork
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

        // Build shared GUID-to-file index once after extraction; used by dependency resolution and formatting phases
        LogFlowStep(context, "Step 4: Build GUID file index");
        WritePhaseHeader(context, "phase4GuidIndexLabel");
        if (context.DebugMode)
            context.Log.Debug(ConsoleMessages.Get("debugPhaseGuidIndex"));

        var guidSw = System.Diagnostics.Stopwatch.StartNew();
        context.GuidIndex = new GuidFileIndex();
        context.GuidIndex.Build(AssetProcessorFileSystem.CollectGuidIndexFilePaths(context.AssetOut), context);
        guidSw.Stop();

        if (context.DebugMode)
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugGuidIndexComplete"), guidSw.Elapsed.ToString(@"mm\:ss"), context.GuidIndex?.Count.ToString("N0") ?? "0"));
        else
            Console.WriteLine(string.Format(ConsoleMessages.Get("guidIndexComplete"), guidSw.Elapsed.ToString(@"mm\:ss")));

        #region Phase 4 - Template Processing

        LogFlowStep(context, "Step 5: Template inheritance merge");
        WritePhaseHeader(context, "phase5TemplateMergeLabel");
        int phase4Result = TemplateMergeOrchestrator.Execute(context, gameType, mainOutputFiles);
        if (phase4Result != 0) return phase4Result;

        LogFlowStep(context, "Step 6: Dependency resolution");
        WritePhaseHeader(context, "phase6DependencyResolutionLabel");
        DependencyResolutionOrchestrator.Execute(context, baseAssetGuidFiles);

        totalSw.Stop();
        LogFlowStep(context, "Step 7: Final formatting + optional asset mod packages");
        WritePhaseHeader(context, "phase7FormattingLabel");
        int formattingResult = FinalFormattingOrchestrator.Execute(context, gameType, totalSw.Elapsed);
        PipelineIssueReporter.WriteSummary(context, formattingResult == 0);
        return formattingResult;

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
            CustomFixlistFile = options.CustomFixlistFile,
            AssetComments = options.AssetComments,
            AssetFix = options.AssetFix,
            AssetTemplates = singleAssetMode ? false : options.AssetTemplates || options.CreateAssetMods,
            AssetModOpsWrap = options.CreateAssetMods ? true : options.AssetModOpsWrap,
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
            WriteDebugArguments(context, Environment.GetCommandLineArgs(), options);

        gameType = GameTypeDetector.DetectFromPath(context.AssetRoot);
        if (!OutputDirectoryManager.EnsureGameDirectoryExists(context))
        {
            gameOutputRoot = "";
            return 1;
        }

        if (!OutputDirectoryManager.TryPrepareOutputDirectories(context, gameType, out gameOutputRoot))
            return 1;

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
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugExtractedGuid"), options.SingleAssetGuid));
            return;
        }

        context.Log.Debug(string.Format(ConsoleMessages.Get("debugTotalArguments"), args.Length));
        for (int i = 0; i < args.Length; i++)
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugArgumentValue"), i, args[i]));

        context.Log.Debug(string.Format(
            ConsoleMessages.Get("debugFlagsApplied"),
            context.AssetComments,
            context.AssetFix,
            context.AssetTemplates,
            context.AssetModOpsWrap,
            context.AssetSplitTemplates,
            context.CreateAssetMods));
        if (!string.IsNullOrEmpty(options.SingleAssetGuid))
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugExtractedGuid"), options.SingleAssetGuid));
    }

    private static string FormatDebugFlags(PipelineContext context) =>
        string.Join(' ', new[]
        {
            context.AssetComments ? "-c" : null,
            context.AssetFix ? "-f" : null,
            context.AssetTemplates ? "-t" : null,
            "-y",
            context.DebugMode ? "-d" : null,
            context.AssetModOpsWrap ? null : "--no-modops-wrap",
            context.AssetNoDefaultProperties ? "--no-default-properties" : null,
            context.AssetSplitTemplates ? "--split-templates" : null,
            context.CreateAssetMods ? "--create-asset-mods" : null,
        }.Where(static flag => flag is not null));

    private static int ExecutePhase1RdaExtraction(
        PipelineContext context,
        string gameType,
        string gameOutputRoot,
        bool autoTemplates,
        string customTemplateFile)
    {
        string assetLangFile = string.IsNullOrEmpty(context.AssetLanguage) ? "none" : context.AssetLanguage;
        if ((!File.Exists(Path.Combine(context.AssetRoot, "assets.xml")) || !File.Exists(Path.Combine(context.AssetRoot, "properties.xml")) || !File.Exists(Path.Combine(context.AssetRoot, assetLangFile))) && Directory.Exists(context.AssetRoot))
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
            Directory.CreateDirectory(sourceXmlPath);
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
                    RdaArchiveExtractor.Extract(context, context.AssetRoot, Anno117RdaPaths, sourceXmlPath, gameType);
                }
                else
                {
                    if (context.DebugMode)
                    {
                        context.Log.Write("RDA", ConsoleMessages.Get("rdaAnno1800Detected"));
                        context.Log.Write("INFO", ConsoleMessages.Get("rdaExtractingAnno1800CoreFiles"));
                    }
                    RdaArchiveExtractor.Extract(context, context.AssetRoot, Anno1800RdaPaths, sourceXmlPath, gameType);
                }
                if (context.DebugMode)
                {
                    context.Log.Write("OK", ConsoleMessages.Get("rdaDecompressionSuccessful"));
                    Console.WriteLine();
                }

                if (!context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    AssetProcessorConfiguration.LoadRegionalIngredientsConfig(context);
                    AssetProcessorConfiguration.LoadAppSettingsConfig(context);
                    context.AssetTemplatesList = TemplateBootstrapService.BootstrapTemplates(
                        context, gameType, customTemplateFile, autoTemplates);
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
            Directory.CreateDirectory(sourceXmlDirectory);
            string[] requiredFiles =
            [
                "assets.xml",
                "properties.xml",
                "templates.xml",
                context.AssetLanguage
            ];
            foreach (string filename in requiredFiles)
            {
                string sourceFile = Path.Combine(context.AssetRoot, filename);
                string destFile = Path.Combine(sourceXmlDirectory, filename);
                if (File.Exists(sourceFile))
                {
                    try
                    {
                        File.Copy(sourceFile, destFile, true);
                    }
                    catch (Exception ex)
                    {
                        context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("couldNotCopyToSourceXml"), filename, ex.Message), always: true);
                    }
                }
            }
            context.SourceXmlFolder = sourceXmlDirectory + Path.DirectorySeparatorChar;
            context.Log.Write("OK", string.Format(ConsoleMessages.Get("sourceFilesCopied"), sourceXmlDirectory), always: true);
        }
        return 0;
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
        if (context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            if (!Console.IsOutputRedirected)
            {
                Console.WriteLine();
                context.Log.Write("OK", ConsoleMessages.Get("rdaCompleteSelectLanguage"), always: true);
                Console.WriteLine();
            }
            return 0;
        }

        WritePhaseHeader(context, "phase2Label");
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
            AssetNameRegistry.Load(context);
            context.PropertyScan = PropertyScanner.Scan(context);
            phase2Sw.Stop();

            context.LoadedPropertyNames = context.PropertyScan.EligibleProperties;

            if (context.DebugMode)
                context.Log.Write("READY", ConsoleMessages.Get("configurationLoadedReady"));

            if (!context.DebugMode)
                Console.WriteLine(string.Format(ConsoleMessages.Get("configLoadedIn"), phase2Sw.Elapsed.ToString(@"mm\:ss")));
        }
        else
        {
            context.PropertyScan = PropertyScanner.Scan(context);
            context.LoadedPropertyNames = context.PropertyScan.EligibleProperties;
        }

        Console.WriteLine();
        return 0;
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
        string baseAssetGuidDir = Path.Combine(context.AssetOut, "BaseAssetGUID");
        baseAssetGuidFiles = Directory.Exists(baseAssetGuidDir) ? AssetProcessorFileSystem.FileList(baseAssetGuidDir) : [];
        if (!string.IsNullOrEmpty(singleAssetGuid) && mainOutputFiles.Length == 0 && baseAssetGuidFiles.Length == 0)
            return 1;

        if (context.AssetSplitTemplates)
        {
            WritePhaseHeader(context, "phase3SplitTemplatesLabel");
            if (!context.DebugMode)
                Console.WriteLine(string.Format(ConsoleMessages.Get("splittingTemplates"), gameType));
            TemplateSplitterService.SplitTemplatesIntoFolders(context, context.SourceXmlFolder, gameOutputRoot, gameType);
            Console.WriteLine();
        }
        return 0;
    }

    #endregion

    private static void TryCleanEmptySingleAssetOutput(PipelineContext context)
    {
        try
        {
            if (Directory.Exists(context.SingleAssetOutputRoot))
            {
                Directory.Delete(context.SingleAssetOutputRoot, true);
                Directory.CreateDirectory(context.SingleAssetOutputRoot);
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
                        assetName = fileName[nameStart..nameEnd].Trim();
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
                    Directory.Delete(newXmlPath, true);
                Directory.CreateDirectory(Path.GetDirectoryName(newXmlPath)!);
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

    private static void WritePhaseHeader(PipelineContext context, string messageKey)
    {
        string label = ConsoleMessages.Get(messageKey);
        if (context.DebugMode)
            context.Log.Write("PHASE", label);
        else
            Console.WriteLine($"\n{label}");
    }

    private static void LogFlowStep(PipelineContext context, string step)
    {
        if (!context.DebugMode)
            return;

        context.Log.Debug(string.Format(ConsoleMessages.Get("debugFlowStep"), step));
    }
}
