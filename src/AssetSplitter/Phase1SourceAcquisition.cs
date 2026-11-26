namespace AssetProcessor;

internal static class Phase1SourceAcquisition
{
    internal static int ExecutePhase1RdaExtraction(
        PipelineContext context,
        string gameType,
        string gameOutputRoot,
        bool forceRefresh = false)
    {
        bool forceExtract = forceRefresh || PipelineFeatureGates.IsSourceExtractionOnlyRun(context);
        string sourceXmlPath = Path.Combine(gameOutputRoot, "source_xml_" + gameType);

        bool directSourceFilesAvailable = HasDirectSourceFiles(context);
        SourceXmlCache.MissReason reuseMiss = SourceXmlCache.MissReason.None;
        bool canReuseSource = !directSourceFilesAvailable
            && Directory.Exists(context.AssetRoot)
            && !forceExtract
            && SourceXmlCache.CanReuse(
                sourceXmlPath,
                gameType,
                context.AssetLanguage,
                context.DetectedGameBuild,
                out reuseMiss);

        if (canReuseSource)
        {
            context.SourceXmlFolder = sourceXmlPath + Path.DirectorySeparatorChar;
            string display = context.DetectedGameBuild?.ToDisplayString() ?? "";
            if (context.DebugMode)
            {
                context.Log.Write(ConsoleMessageKind.Phase, ConsoleMessages.Get("phase1Label"));
            }
            else
            {
                PipelineRunSupport.WritePhaseHeader(context, "phase1Label");
            }

            context.Log.WriteLocalized(ConsoleMessageKind.Ok, "sourceXmlCacheReused", always: true, display);
            return ValidatePhase1SourceFiles(context, gameType) ? 0 : 1;
        }

        // Content-changed but required files still present and well-formed: keep on-disk source
        // (do not wipe + RDA). Output cache will DiffRepair / Complete from the new extractable hashes.
        // Corrupt required files must not be kept — fall through to RDA re-extract.
        string? corruptSourceFile = null;
        bool keepContentChangedSource = !forceExtract
            && !directSourceFilesAvailable
            && reuseMiss == SourceXmlCache.MissReason.ContentChanged
            && SourceXmlCache.AreRequiredFilesWellFormed(
                sourceXmlPath,
                gameType,
                context.AssetLanguage,
                out corruptSourceFile);

        if (keepContentChangedSource)
        {
            context.SourceXmlFolder = sourceXmlPath + Path.DirectorySeparatorChar;
            string display = context.DetectedGameBuild?.ToDisplayString() ?? "";
            if (context.DebugMode)
            {
                context.Log.Write(ConsoleMessageKind.Phase, ConsoleMessages.Get("phase1Label"));
            }
            else
            {
                PipelineRunSupport.WritePhaseHeader(context, "phase1Label");
            }

            context.Log.WriteLocalized(ConsoleMessageKind.Info, "sourceXmlCacheContentChanged", always: true, display);
            LogSourceXmlContentChangedPaths(context, sourceXmlPath);
            SourceXmlCache.WriteStamp(sourceXmlPath, gameType, context.DetectedGameBuild, context.Log);
            return ValidatePhase1SourceFiles(context, gameType) ? 0 : 1;
        }

        bool sourceCorrupt = !forceExtract
            && !directSourceFilesAvailable
            && (reuseMiss == SourceXmlCache.MissReason.FilesCorrupt
                || (reuseMiss == SourceXmlCache.MissReason.ContentChanged && corruptSourceFile is not null));
        if (sourceCorrupt)
        {
            if (corruptSourceFile is null
                && !SourceXmlCache.AreRequiredFilesIntact(
                    sourceXmlPath,
                    gameType,
                    context.AssetLanguage,
                    out corruptSourceFile))
            {
                // failedFileName already set
            }

            string display = context.DetectedGameBuild?.ToDisplayString() ?? "";
            context.Log.WriteLocalized(
                ConsoleMessageKind.Warning,
                "sourceXmlCacheCorrupt",
                always: true,
                display,
                corruptSourceFile ?? "?");
        }

        if (!directSourceFilesAvailable && Directory.Exists(context.AssetRoot))
        {
            Console.WriteLine();
            if (context.DebugMode)
            {
                context.Log.Write(ConsoleMessageKind.Phase, ConsoleMessages.Get("phase1Label"));
                context.Log.Write(ConsoleMessageKind.Rda, ConsoleMessages.Get("extractingFromRdaCompressed"));
                context.Log.Write(ConsoleMessageKind.Info, ConsoleMessages.Get("rdaFilesContainAssets"));
                if (forceExtract)
                {
                    context.Log.Debug(ConsoleMessages.Get("debugSourceXmlCacheForceRefresh"));
                }
            }
            else
            {
                PipelineRunSupport.WritePhaseHeader(context, "phase1Label");
                BackendI18nLine.Write("extractingGameData");
            }

            if (!forceExtract && reuseMiss == SourceXmlCache.MissReason.ContentChanged && !sourceCorrupt)
            {
                string display = context.DetectedGameBuild?.ToDisplayString() ?? "";
                context.Log.WriteLocalized(ConsoleMessageKind.Info, "sourceXmlCacheContentChanged", always: true, display);
                LogSourceXmlContentChangedPaths(context, sourceXmlPath);
            }
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
                        context.Log.Write(ConsoleMessageKind.Rda, ConsoleMessages.Get("rdaAnno117Detected"));
                        context.Log.Write(ConsoleMessageKind.Info, ConsoleMessages.Get("rdaExtractingAnno117CoreFiles"));
                    }
                    RdaArchiveExtractor.Extract(context, context.AssetRoot, PipelineFeatureGates.GetRdaExtractFilter(gameType), sourceXmlPath, gameType);
                }
                else
                {
                    if (context.DebugMode)
                    {
                        context.Log.Write(ConsoleMessageKind.Rda, ConsoleMessages.Get("rdaAnno1800Detected"));
                        context.Log.Write(ConsoleMessageKind.Info, ConsoleMessages.Get("rdaExtractingAnno1800CoreFiles"));
                    }
                    RdaArchiveExtractor.Extract(context, context.AssetRoot, PipelineFeatureGates.GetRdaExtractFilter(gameType), sourceXmlPath, gameType);
                }
                if (context.DebugMode)
                {
                    context.Log.Write(ConsoleMessageKind.Ok, ConsoleMessages.Get("rdaDecompressionSuccessful"));
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                context.Log.WriteLocalized(ConsoleMessageKind.Error, "rdaExtractionFailed", always: true, ex.Message);
                return 1;
            }

            if (context.RdaArchiveReadFailures.Count > 0)
            {
                // Files may have come from older archives than the newest patch. Do not stamp the
                // tree as a valid extraction for this build so the next run extracts again, and
                // surface it as an error instead of silently producing stale output.
                string failedArchives = string.Join(", ", context.RdaArchiveReadFailures);
                context.Issues.ReportFatal(
                    PipelineIssueCodes.RdaExtractionFailed,
                    "RDA archive(s) could not be read; extracted source XML may come from older archives: " + failedArchives,
                    "An archive in the game's maindata folder is damaged, locked or in an unsupported format.",
                    "Verify the game files in your launcher and run again.",
                    failedArchives);
            }
            else
            {
                SourceXmlCache.WriteStamp(sourceXmlPath, gameType, context.DetectedGameBuild, context.Log);
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
                    context.Log.WriteLocalized(ConsoleMessageKind.Warning, "couldNotCopyToSourceXml", always: true, filename, ex.Message);
                }
            }
            context.SourceXmlFolder = sourceXmlDirectory + Path.DirectorySeparatorChar;
            context.Log.WriteLocalized(ConsoleMessageKind.Ok, "sourceFilesCopied", always: true, sourceXmlDirectory);
            SourceXmlCache.WriteStamp(sourceXmlDirectory, gameType, context.DetectedGameBuild, context.Log);
        }
        return ValidatePhase1SourceFiles(context, gameType) ? 0 : 1;
    }

    internal static long EstimatePhase1PlanUnits(PipelineContext context, string gameType, bool forceRefresh = false)
    {
        bool forceExtract = forceRefresh || PipelineFeatureGates.IsSourceExtractionOnlyRun(context);
        if (HasDirectSourceFiles(context))
        {
            return 1;
        }

        if (!Directory.Exists(context.AssetRoot))
        {
            return 0;
        }

        string mainDataPath = Path.Combine(context.AssetRoot, "maindata");
        if (!Directory.Exists(mainDataPath))
        {
            return 0;
        }

        string sourceXmlPath = Path.Combine(context.GameOutputRoot, "source_xml_" + gameType);
        if (!forceExtract && !string.IsNullOrWhiteSpace(context.GameOutputRoot))
        {
            if (SourceXmlCache.CanReuse(
                    sourceXmlPath,
                    gameType,
                    context.AssetLanguage,
                    context.DetectedGameBuild,
                    out SourceXmlCache.MissReason estimateMiss))
            {
                return 0;
            }

            // Content-changed with complete, well-formed files: keep disk; no RDA plan units.
            if (estimateMiss == SourceXmlCache.MissReason.ContentChanged
                && SourceXmlCache.AreRequiredFilesWellFormed(
                    sourceXmlPath,
                    gameType,
                    context.AssetLanguage,
                    out _))
            {
                return 0;
            }
        }

        if (gameType.Contains("117", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return AssetProcessorFileSystem.FileList(mainDataPath, "data*.rda").Length;
    }

    internal static bool HasDirectSourceFiles(PipelineContext context)
    {
        if (!File.Exists(Path.Combine(context.AssetRoot, "assets.xml"))
            || !File.Exists(Path.Combine(context.AssetRoot, "properties.xml")))
        {
            return false;
        }

        return context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase)
            || File.Exists(Path.Combine(context.AssetRoot, context.AssetLanguage));
    }

    internal static bool ValidatePhase1SourceFiles(PipelineContext context, string gameType)
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

    private static void LogSourceXmlContentChangedPaths(PipelineContext context, string sourceXmlPath)
    {
        IReadOnlyList<string> paths = SourceXmlCache.ListContentChangedPaths(sourceXmlPath);
        if (paths.Count == 0)
        {
            return;
        }

        const int limit = 20;
        int listed = Math.Min(paths.Count, limit);
        for (int i = 0; i < listed; i++)
        {
            context.Log.WriteLocalized(
                ConsoleMessageKind.Info,
                "contentChangedDiffPathChanged",
                always: true,
                paths[i]);
        }

        int omitted = paths.Count - listed;
        if (omitted > 0)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Info, "contentChangedDiffPathMore", always: true, omitted);
        }
    }
}
