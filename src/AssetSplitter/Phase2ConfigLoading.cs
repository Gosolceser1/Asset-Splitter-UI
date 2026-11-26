namespace AssetProcessor;

internal static class Phase2ConfigLoading
{
    internal static int ExecutePhase2ConfigLoading(PipelineContext context)
    {
        if (!File.Exists(Path.Combine(context.SourceXmlFolder, "assets.xml")))
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "xmlFileNotFound", always: true, "assets.xml");
            return 1;
        }
        if (!context.AssetLanguage.Equals("none") && !File.Exists(Path.Combine(context.SourceXmlFolder, context.AssetLanguage)))
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "xmlFileNotFound", always: true, Path.Combine(context.SourceXmlFolder, context.AssetLanguage));
            return 1;
        }
        Console.WriteLine("");
        if (context.AddComments
            && context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "commentsRequireLanguage", always: true);
            return 1;
        }

        PipelineRunSupport.WritePhaseHeader(context, "phase2Label");
        LoadProcessingPrerequisites(context);

        int splitTemplatesResult = ExecutePhase2SplitTemplates(context, context.DetectedGameType);
        if (splitTemplatesResult != 0)
        {
            return splitTemplatesResult;
        }

        if (context.DebugMode)
        {
            context.Log.Write(ConsoleMessageKind.Config, ConsoleMessages.Get("loadingGameTemplatesConfig"));
            context.Log.Write(ConsoleMessageKind.Info, ConsoleMessages.Get("convertingRawXmlToMods"));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugPhase2SourceXml"), context.SourceXmlFolder));
        }

        if (!context.AssetLanguage.Equals("none"))
        {
            // Expand UI overall budget before language progress lines fill it (avoids pinning at 100%).
            Console.WriteLine($"[PLAN+] {TranslationDictionaryLoader.LanguageProgressTotalUnits}");
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
                context.Log.Write(ConsoleMessageKind.Complete, ConsoleMessages.Get("configurationLoadedReady"));
            }

            if (!context.DebugMode)
            {
                BackendI18nLine.Write("configLoadedIn", phase2Sw.Elapsed.ToString(@"mm\:ss"));
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

    internal static void LoadProcessingPrerequisites(PipelineContext context)
    {
        // Called before OutputRunCache.Evaluate (so the cache diff uses the real -u / --auto-templates
        // list) and again from Phase 2; only load once per run.
        if (context.ProcessingPrerequisitesLoaded)
        {
            return;
        }

        context.ProcessingPrerequisitesLoaded = true;
        AssetProcessorConfiguration.LoadRegionalIngredientsConfig(context);
        AssetProcessorConfiguration.LoadAppSettingsConfig(context);
        context.AssetTemplatesList = TemplateBootstrapService.BootstrapTemplates(
            context,
            context.DetectedGameType,
            context.CustomTemplateFile,
            context.AutoTemplates);
    }

    internal static int ExecutePhase2SplitTemplates(PipelineContext context, string gameType)
    {
        if (!context.SplitTemplates)
        {
            return 0;
        }

        string outputBase = Path.Combine(context.GameOutputRoot, "output_templates_" + gameType);
        if (context.PreserveOutputDirectories
            && !context.ForceResplitTemplates
            && Directory.Exists(outputBase)
            && Directory.EnumerateFiles(outputBase, "*.xml", SearchOption.AllDirectories).Any())
        {
            return 0;
        }

        PipelineRunSupport.WritePhaseHeader(context, "phase3SplitTemplatesLabel");
        if (!OutputDirectoryManager.TryPrepareTemplateSplitDirectory(context, gameType))
        {
            return 1;
        }

        if (!context.DebugMode)
        {
            BackendI18nLine.Write("splittingTemplates", gameType);
        }

        TemplateSplitterService.SplitTemplatesIntoFolders(context, context.SourceXmlFolder, context.GameOutputRoot, gameType);
        Console.WriteLine();
        return 0;
    }

    internal static long CountSplitTemplateFiles(PipelineContext context, string gameType)
    {
        string templateDir = Path.Combine(context.GameOutputRoot, "output_templates_" + gameType);
        return Directory.Exists(templateDir) ? Directory.GetFiles(templateDir, "*.xml").Length : 0;
    }
}
