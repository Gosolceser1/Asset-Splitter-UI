namespace AssetProcessor;

/// <summary>CLI pipeline sequencer. Phase work lives in focused helper types.</summary>
public static class PipelineOrchestrator
{
    private const int CancelledExitCode = 130;

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
            int templateResult = PipelineBootstrap.HandleTemplateCommand(args);
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

        var context = PipelineBootstrap.CreateContext(options);
        int initializationResult = PipelineBootstrap.InitializePipeline(context, options, out string gameType, out string gameOutputRoot, out bool autoTemplates);
        if (initializationResult != 0)
        {
            return initializationResult;
        }

        using var cancel = PipelineCancellation.Attach(context);
        using IDisposable? runLock = OutputRunLock.TryAcquire(context);
        if (runLock is null)
        {
            return 1;
        }

        try
        {
            return RunInitializedPipeline(context, options, gameType, gameOutputRoot);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
            context.Log.WriteLocalized(ConsoleMessageKind.Warning, "pipelineCancelled", always: true);
            return CancelledExitCode;
        }
    }

    private static int RunInitializedPipeline(
        PipelineContext context,
        AssetProcessorCommandLineOptions options,
        string gameType,
        string gameOutputRoot)
    {
        if (!context.DebugMode)
        {
            BackendI18nLine.Write("pipelineStarting");
            if (!Console.IsOutputRedirected)
            {
                PipelineRunSupport.WriteDetectedGameBuild(context);
            }
        }
        else
        {
            PipelineRunSupport.LogFlowStep(context, "Pipeline start -> validate paths/options -> run phases in strict order");
            PipelineRunSupport.WriteDetectedGameBuild(context);
        }

        var totalSw = System.Diagnostics.Stopwatch.StartNew();

        PipelineRunSupport.LogFlowStep(context, "Step 1: Phase 1 RDA extraction");
        bool forcePhase1Refresh = options.SourceExtractionOnly || PipelineFeatureGates.IsSourceExtractionOnlyRun(context);
        long phase1PlanUnits = Phase1SourceAcquisition.EstimatePhase1PlanUnits(context, gameType, forcePhase1Refresh);
        if (phase1PlanUnits > 0)
        {
            Console.WriteLine($"[PLAN] {phase1PlanUnits}");
        }

        context.ThrowIfCancellationRequested();
        int phase1Result = Phase1SourceAcquisition.ExecutePhase1RdaExtraction(context, gameType, gameOutputRoot, forcePhase1Refresh);
        if (phase1Result != 0)
        {
            return phase1Result;
        }

        if (context.AddComments
            && context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "commentsRequireLanguage", always: true);
            return 1;
        }

        if (options.SourceExtractionOnly || PipelineFeatureGates.IsSourceExtractionOnlyRun(context))
        {
            PipelineRunSupport.LogFlowStep(context, "Step 2: validate Phase 1 output (source extraction only — no processing flags)");
            if (!File.Exists(Path.Combine(context.SourceXmlFolder, "assets.xml")))
            {
                context.Log.WriteLocalized(ConsoleMessageKind.Error, "xmlFileNotFound", always: true, "assets.xml");
                return 1;
            }

            Console.WriteLine(ProcessingRunPolicy.Phase1OnlyMarker);
            Console.WriteLine();
            context.Log.WriteLocalized(ConsoleMessageKind.Ok, "sourceExtractionComplete", always: true);
            Console.WriteLine();

            return PipelineRunSupport.FinishPipelineRun(context, totalSw, assetCount: 0, succeeded: true);
        }

        OutputRunCache.Decision outputDecision = OutputRunCache.Evaluate(context, gameType, options.ForceFullRebuild || context.ForceFullRebuild);
        ApplyOutputCacheDecision(context, outputDecision);
        if (outputDecision.Mode == OutputRunCache.RunMode.Complete)
        {
            context.OutputCacheCompleteHit = true;
            bool cleanedOrphans = false;
            if (outputDecision.Presence is { } presence
                && (presence.OrphanXmlRelativePaths.Count > 0 || presence.OrphanModRelativePaths.Count > 0))
            {
                OutputRunCache.DeleteOrphans(context, presence);
                cleanedOrphans = true;
            }

            if (outputDecision.GameDiff is not null || cleanedOrphans || outputDecision.SideTrees?.SourceXml is not null)
            {
                // Build fingerprint moved but hashes match, orphan cleanup, and/or source tree
                // digest accepted without rebuild — refresh stamp digests.
                if (outputDecision.Reason == OutputRunCache.MissReason.HashesMissing)
                {
                    context.Log.WriteLocalized(ConsoleMessageKind.Ok, "outputCacheHashBackfill", always: true);
                }
                else if (outputDecision.SideTrees?.SourceXml is not null && outputDecision.GameDiff is null && !cleanedOrphans)
                {
                    context.Log.WriteLocalized(ConsoleMessageKind.Ok, "outputCacheSourceAccepted", always: true);
                    OutputRunCache.WriteSideTreeMismatchDetails(
                        context.Log,
                        outputDecision.SideTrees,
                        context.DetectedGameType);
                }
                else if (outputDecision.GameDiff is not null)
                {
                    context.Log.WriteLocalized(ConsoleMessageKind.Ok, "outputCacheGameDiffNone", always: true);
                }
                else
                {
                    context.Log.WriteLocalized(
                        ConsoleMessageKind.Ok,
                        "outputCacheComplete",
                        always: true,
                        string.IsNullOrWhiteSpace(outputDecision.BuildDisplay)
                            ? (context.DetectedGameBuild?.ToDisplayString() ?? "")
                            : outputDecision.BuildDisplay,
                        outputDecision.AssetCount);
                }

                OutputRunCache.WriteStampAndInventory(context, gameType, context.Log);
            }
            else
            {
                context.Log.WriteLocalized(
                    ConsoleMessageKind.Ok,
                    "outputCacheComplete",
                    always: true,
                    string.IsNullOrWhiteSpace(outputDecision.BuildDisplay)
                        ? (context.DetectedGameBuild?.ToDisplayString() ?? "")
                        : outputDecision.BuildDisplay,
                    outputDecision.AssetCount);
            }

            return PipelineRunSupport.FinishPipelineRun(context, totalSw, outputDecision.AssetCount, succeeded: true);
        }

        if (outputDecision.Mode == OutputRunCache.RunMode.DiffRepair
            && outputDecision.GameDiff is { RebuildGuids.Count: 0 })
        {
            // Only removals — stamp refresh after deletes already applied.
            OutputRunCache.WriteStampAndInventory(context, gameType, context.Log);
            context.OutputCacheCompleteHit = true;
            return PipelineRunSupport.FinishPipelineRun(
                context,
                totalSw,
                outputDecision.AssetCount,
                succeeded: true);
        }

        PipelineRunSupport.LogFlowStep(context, "Step 2: Phase 2 config + dictionaries + property scan");
        context.ThrowIfCancellationRequested();
        int phase2ResultFull = Phase2ConfigLoading.ExecutePhase2ConfigLoading(context);
        if (phase2ResultFull != 0)
        {
            return phase2ResultFull;
        }

        PipelineRunSupport.LogFlowStep(context, "Step 3: Phase 3 asset extraction");
        PipelineRunSupport.WritePhaseHeader(context, "phase3Label");
        context.ThrowIfCancellationRequested();
        int phase3Result = Phase3AssetExtraction.ExecutePhase3AssetExtraction(context, options.SingleAssetGuid, gameType, gameOutputRoot, out string[]? mainOutputFiles, out string[]? baseAssetGuidFiles);
        if (phase3Result != 0)
        {
            return phase3Result;
        }

        // Phase 3 extract units were announced via [PLAN+] before the extract loop.
        // Plan only the work still ahead (phases 4–8) from actual output file counts.
        long guidIndexWork = PipelineFeatureGates.NeedsGuidIndex(context) ? Math.Max(mainOutputFiles.Length, 1) : 0;
        if (context.PreserveOutputDirectories && context.RepairOnlyGuids is null)
        {
            // Mod-only repair: GuidIndex still builds over the full tree.
            guidIndexWork = PipelineFeatureGates.NeedsGuidIndex(context)
                ? AssetProcessorFileSystem.FileList(context.AssetOutputDir).Length
                : 0;
        }

        long templateMergeWork = PipelineFeatureGates.NeedsTemplateMerge(context) ? mainOutputFiles.Length : 0;
        long dependencyWork = context.FixDependencies
            ? baseAssetGuidFiles.Count(file => !file.Contains("PaMSy", StringComparison.OrdinalIgnoreCase))
            : 0;
        long formattingWork = PipelineFeatureGates.NeedsXmlEnrichment(context)
            ? mainOutputFiles.Length + (context.FixDependencies ? baseAssetGuidFiles.Length : 0)
            : 0;
        long templateAnnotateWork = context.SplitTemplates && context.AddComments
            ? Phase2ConfigLoading.CountSplitTemplateFiles(context, gameType)
            : 0;
        long assetModWork = context.CreateAssetMods
            ? (context.RepairPackageGuids?.Count
               ?? mainOutputFiles.Length + baseAssetGuidFiles.Length)
            : 0;
        long plannedWork = guidIndexWork
                         + templateMergeWork
                         + dependencyWork
                         + formattingWork
                         + templateAnnotateWork
                         + assetModWork;
        Console.WriteLine($"[PLAN+] {plannedWork}");
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
            PipelineRunSupport.LogFlowStep(context, ConsoleMessages.Get("debugFlowSplitOnlyComplete"));
            OutputRunCache.TryFlushFlatXmlContentChangedDiffs(context);
            return PipelineRunSupport.FinishPipelineRun(context, totalSw, mainOutputFiles.Length, succeeded: true);
        }


        if (PipelineFeatureGates.NeedsGuidIndex(context))
        {
            PipelineRunSupport.LogFlowStep(context, "Step 4: Phase 4 GUID file index");
            PipelineRunSupport.WritePhaseHeader(context, "phase4GuidIndexLabel");
            if (context.DebugMode)
            {
                context.Log.Debug(ConsoleMessages.Get("debugPhaseGuidIndex"));
            }

            context.ThrowIfCancellationRequested();
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
                BackendI18nLine.Write("guidIndexComplete", guidSw.Elapsed.ToString(@"mm\:ss"));
            }
        }
        else
        {
            PipelineRunSupport.LogFlowStep(context, ConsoleMessages.Get("debugFlowPhase4Skipped"));
        }

        bool skipEnrichForModOnlyRepair = context.PreserveOutputDirectories
            && mainOutputFiles.Length == 0
            && baseAssetGuidFiles.Length == 0
            && context.RepairPackageGuids is { Count: > 0 };

        if (!skipEnrichForModOnlyRepair && PipelineFeatureGates.NeedsTemplateMerge(context))
        {
            PipelineRunSupport.LogFlowStep(context, "Step 5: Phase 5 template inheritance merge");
            PipelineRunSupport.WritePhaseHeader(context, "phase5TemplateMergeLabel");
            context.ThrowIfCancellationRequested();
            int phase5Result = TemplateMergeOrchestrator.Execute(context, gameType, mainOutputFiles);
            if (phase5Result != 0)
            {
                return phase5Result;
            }
        }
        else
        {
            PipelineRunSupport.LogFlowStep(context, ConsoleMessages.Get("debugFlowPhase5Skipped"));
        }

        if (!skipEnrichForModOnlyRepair && context.FixDependencies)
        {
            PipelineRunSupport.LogFlowStep(context, "Step 6: Phase 6 dependency resolution (-f)");
            PipelineRunSupport.WritePhaseHeader(context, "phase6DependencyResolutionLabel");
            context.ThrowIfCancellationRequested();
            DependencyResolutionOrchestrator.Execute(context, baseAssetGuidFiles);
            PipelineRunSupport.RefreshGuidFileIndex(context);
        }
        else
        {
            PipelineRunSupport.LogFlowStep(context, "Step 6: skipped (no -f flag)");
        }

        if (PipelineFeatureGates.NeedsFormatting(context))
        {
            string formattingPhaseKey = PipelineFeatureGates.IsModExportOnlyRun(context)
                ? "phase8AssetModPackages"
                : "phase7FormattingLabel";
            PipelineRunSupport.LogFlowStep(context, PipelineFeatureGates.IsModExportOnlyRun(context)
                ? "Step 7: Phase 8 mod packages (mod-export-only)"
                : "Step 7: Phase 7 formatting (+ Phase 8 mod packages when enabled)");
            PipelineRunSupport.WritePhaseHeader(context, formattingPhaseKey);
            context.ThrowIfCancellationRequested();
            int formattingResult = FinalFormattingOrchestrator.Execute(context, gameType, totalSw);
            if (totalSw.IsRunning)
            {
                totalSw.Stop();
            }

            PipelineIssueReporter.WriteSummary(context, formattingResult == 0);
            return PipelineRunSupport.ResolveExitCode(context, formattingResult);
        }

        PipelineRunSupport.LogFlowStep(context, ConsoleMessages.Get("debugFlowPhase7Skipped"));
        // Split-only / no Phase 7: extract output is final — flush any content-changed XML diffs.
        OutputRunCache.TryFlushFlatXmlContentChangedDiffs(context);
        return PipelineRunSupport.FinishPipelineRun(context, totalSw, mainOutputFiles.Length, succeeded: true);
    }

    private static void ApplyOutputCacheDecision(PipelineContext context, OutputRunCache.Decision decision)
    {
        if (decision.Mode == OutputRunCache.RunMode.Complete)
        {
            return;
        }

        if (decision.Mode == OutputRunCache.RunMode.Full)
        {
            string reason = decision.Reason switch
            {
                OutputRunCache.MissReason.ForceFullRebuild => ConsoleMessages.Get("outputCacheForceFullRebuild"),
                OutputRunCache.MissReason.NotEligible => "not eligible for output cache",
                OutputRunCache.MissReason.BuildUnknown => "game build unknown",
                OutputRunCache.MissReason.StampMissing => "no output stamp",
                OutputRunCache.MissReason.StampInvalid => "invalid output stamp",
                OutputRunCache.MissReason.InventoryMissing => "no output inventory",
                OutputRunCache.MissReason.BuildChanged => "game build changed",
                OutputRunCache.MissReason.OptionsChanged => "processing options changed",
                OutputRunCache.MissReason.GameTypeMismatch => "game type mismatch",
                OutputRunCache.MissReason.SchemaMismatch => "output schema changed",
                OutputRunCache.MissReason.SourceAssetsUnavailable => "source assets.xml unavailable",
                OutputRunCache.MissReason.SideTreeMismatch => ConsoleMessages.Get("outputCacheSideTreeMismatchReason"),
                _ => decision.Reason.ToString(),
            };

            if (decision.Reason == OutputRunCache.MissReason.ForceFullRebuild)
            {
                context.Log.WriteLocalized(ConsoleMessageKind.Info, "outputCacheForceFullRebuild", always: true);
            }
            else if (decision.Reason != OutputRunCache.MissReason.NotEligible
                     && decision.Reason != OutputRunCache.MissReason.StampMissing)
            {
                context.Log.WriteLocalized(ConsoleMessageKind.Info, "outputCacheFullRebuild", always: true, reason);
            }

            if (decision.SideTrees is { IsOk: false } fullSideTrees)
            {
                OutputRunCache.WriteSideTreeMismatchDetails(context.Log, fullSideTrees, context.DetectedGameType);
            }

            return;
        }

        if (decision.Mode == OutputRunCache.RunMode.DiffRepair)
        {
            OutputRunCache.GameDiffResult diff = decision.GameDiff
                ?? throw new InvalidOperationException("DiffRepair decision requires game diff results.");

            context.PreserveOutputDirectories = true;
            context.ForceResplitTemplates = decision.ForceResplitTemplates
                || (decision.SideTrees?.Templates is not null);
            OutputRunCache.DeleteRemovedEntries(context, diff);
            // Changed / descendant rebuilds must remove prior XML (and mod folders) first;
            // otherwise extract reserves a -2 path and leaves stale template copies behind.
            OutputRunCache.DeleteReplacedEntries(context, diff);

            HashSet<string> rebuild = new(diff.RebuildGuids, StringComparer.OrdinalIgnoreCase);
            // Empty set (not null) if only removals already applied — skip extract of all assets.
            context.RepairOnlyGuids = rebuild.Count > 0
                ? rebuild
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            context.RepairPackageGuids = rebuild.Count > 0
                ? new HashSet<string>(rebuild, StringComparer.OrdinalIgnoreCase)
                : null;
            string diffMessageKey = decision.Reason == OutputRunCache.MissReason.HashesMissing
                ? "outputCacheHashBackfillDiff"
                : decision.Reason == OutputRunCache.MissReason.SideTreeMismatch
                    ? "outputCacheSourceDiff"
                    : "outputCacheGameDiff";
            context.Log.WriteLocalized(
                ConsoleMessageKind.Ok,
                diffMessageKey,
                always: true,
                diff.Added.Count,
                diff.Changed.Count,
                diff.Removed.Count,
                rebuild.Count);
            if (decision.SideTrees is { IsOk: false } diffSideTrees)
            {
                OutputRunCache.WriteSideTreeMismatchDetails(context.Log, diffSideTrees, context.DetectedGameType);
            }

            IReadOnlyList<OutputRunCache.RepairDetailItem> diffDetails =
                OutputRunCache.BuildDiffRepairDetails(diff, context);
            OutputRunCache.StoreRepairGuidMessageKeys(context, diffDetails);
            OutputRunCache.WriteRepairDetailLines(context.Log, diffDetails);
            return;
        }

        // Presence Repair
        OutputRunCache.PresenceResult presence = decision.Presence
            ?? throw new InvalidOperationException("Repair decision requires presence results.");

        context.PreserveOutputDirectories = true;
        context.ForceResplitTemplates = decision.ForceResplitTemplates
            || (decision.SideTrees?.Templates is not null);
        HashSet<string> xmlGuids = new(
            presence.MissingXml.Select(static e => e.Guid).Where(static g => g.Length > 0),
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> packageGuids = new(xmlGuids, StringComparer.OrdinalIgnoreCase);
        foreach (OutputRunCache.InventoryEntry entry in presence.MissingMod)
        {
            if (!string.IsNullOrWhiteSpace(entry.Guid))
            {
                packageGuids.Add(entry.Guid);
            }
        }

        bool templatesOnlyRepair = decision.SideTrees?.OnlyTemplatesFailed == true
            && xmlGuids.Count == 0
            && packageGuids.Count == 0;

        // Empty set (not null) for mod-only / templates-only repair: ExtractAssets skips all GUIDs
        // instead of re-extracting everything.
        context.RepairOnlyGuids = xmlGuids.Count > 0
            ? xmlGuids
            : (packageGuids.Count > 0 || templatesOnlyRepair
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : null);
        context.RepairPackageGuids = packageGuids.Count > 0 ? packageGuids : null;

        if (templatesOnlyRepair)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Ok, "outputCacheRepairTemplates", always: true);
        }
        else
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Ok, "outputCacheRepair", always: true, packageGuids.Count);
        }

        if (decision.SideTrees is { IsOk: false } repairSideTrees)
        {
            OutputRunCache.WriteSideTreeMismatchDetails(context.Log, repairSideTrees, context.DetectedGameType);
        }

        IReadOnlyList<OutputRunCache.RepairDetailItem> presenceDetails =
            OutputRunCache.BuildPresenceRepairDetails(presence, context);
        OutputRunCache.StoreRepairGuidMessageKeys(context, presenceDetails);
        OutputRunCache.WriteRepairDetailLines(context.Log, presenceDetails);
        // Snapshot content-changed files before delete so post-write logging can show old vs new lines.
        OutputRunCache.CaptureContentChangedSnapshots(context, presence);
        // Empty/corrupt inventory XML still on disk must be removed before re-extract
        // (same -2.xml / stale-mod risk as DiffRepair ReplacedEntries).
        OutputRunCache.DeletePresenceRebuildEntries(context, presence);
        OutputRunCache.DeleteOrphans(context, presence);
    }
}
