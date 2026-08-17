namespace AssetProcessor;

internal static class PipelineBootstrap
{
    internal static PipelineContext CreateContext(AssetProcessorCommandLineOptions options)
    {
        bool singleAssetMode = !string.IsNullOrWhiteSpace(options.SingleAssetGuid);
        var context = new PipelineContext
        {
            AssetRoot = options.AssetRoot,
            BaseOutputDir = options.BaseOutputDir,
            AssetLanguage = options.AssetLanguage,
            ReadmeLanguage = options.ReadmeLanguage,
            CustomFixlistFile = options.CustomFixlistFile,
            AddComments = options.AddComments,
            FixDependencies = options.FixDependencies,
            CreateTemplateFolders = singleAssetMode ? false : options.CreateTemplateFolders,
            ModOpsWrap = options.ModOpsWrap,
            NoDefaultProperties = options.NoDefaultProperties,
            SplitTemplates = !singleAssetMode && options.SplitTemplates,
            CreateAssetMods = options.CreateAssetMods,
            DebugMode = options.DebugMode,
            ForceFullRebuild = options.ForceFullRebuild,
            SingleAssetGuid = options.SingleAssetGuid
        };
        context.Issues.Clear();
        context.ProgressReporter.Initialize(context);
        return context;
    }

    internal static int InitializePipeline(
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
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "gameNotDetected", always: true);
            context.Log.WriteLocalized(ConsoleMessageKind.Warning, "gameDirectoryNotFound", always: true, context.AssetRoot);
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

        if (CloudSyncedPathDetector.IsLikelyCloudSynced(context.BaseOutputDir)
            || CloudSyncedPathDetector.IsLikelyCloudSynced(context.AnnoAssetsRoot)
            || CloudSyncedPathDetector.IsLikelyCloudSynced(gameOutputRoot))
        {
            context.Log.Write(ConsoleMessageKind.Warning, ConsoleMessages.Get("cloudSyncedOutputWarning"), always: true);
        }

        return 0;
    }

    internal static int HandleTemplateCommand(string[] args)
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
            BackendI18nLine.Write("templateUpdateSucceeded", count);
            return 0;
        }

        BackendI18nLine.Write("templateUpdateFailed");
        return 1;
    }

    internal static void WriteDebugArguments(PipelineContext context, string[] args, AssetProcessorCommandLineOptions options)
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
            context.AddComments,
            context.FixDependencies,
            context.CreateTemplateFolders,
            context.ModOpsWrap,
            context.SplitTemplates,
            context.CreateAssetMods));
        if (!string.IsNullOrEmpty(options.SingleAssetGuid))
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugExtractedGuid"), options.SingleAssetGuid));
        }
    }

    internal static string FormatDebugFlags(PipelineContext context) =>
        string.Join(' ', new[]
        {
            context.AddComments ? "-c" : null,
            context.FixDependencies ? "-f" : null,
            context.CreateTemplateFolders ? "-t" : null,
            context.DebugMode ? "-d" : null,
            context.ModOpsWrap ? null : "--no-modops-wrap",
            context.NoDefaultProperties ? "--no-default-properties" : null,
            context.SplitTemplates ? "--split-templates" : null,
            context.CreateAssetMods ? "--create-asset-mods" : null,
        }.Where(static flag => flag is not null));
}
