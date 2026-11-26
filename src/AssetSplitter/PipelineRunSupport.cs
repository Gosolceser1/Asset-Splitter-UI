namespace AssetProcessor;

internal static class PipelineRunSupport
{
    /// <summary>
    /// Elapsed time as mm:ss, or h:mm:ss for runs of an hour or more (<c>mm\:ss</c> alone wrapped,
    /// so a 1h05m run was reported as 05:00).
    /// </summary>
    internal static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalHours >= 1
            ? string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{(int)elapsed.TotalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}")
            : elapsed.ToString(@"mm\:ss", System.Globalization.CultureInfo.InvariantCulture);

    internal static void RefreshGuidFileIndex(PipelineContext context)
    {
        if (!context.FixDependencies)
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

    /// <summary>Maps ConsoleMessages phase keys to UI <c>consoleMessages.backendPhase*</c> resource keys.</summary>
    private static readonly Dictionary<string, string> PhaseHeaderUiKeys = new(StringComparer.Ordinal)
    {
        ["phase1Label"] = "consoleMessages.backendPhase1Label",
        ["phase2Label"] = "consoleMessages.backendPhase2Label",
        ["phase3Label"] = "consoleMessages.backendPhase3Label",
        ["phase3SplitTemplatesLabel"] = "consoleMessages.backendPhase3SplitLabel",
        ["phase4GuidIndexLabel"] = "consoleMessages.backendPhase4GuidIndexLabel",
        ["phase5TemplateMergeLabel"] = "consoleMessages.backendPhase5MergeLabel",
        ["phase6DependencyResolutionLabel"] = "consoleMessages.backendPhase6DepsLabel",
        ["phase7FormattingLabel"] = "consoleMessages.backendPhase7FormatLabel",
        ["phase8AssetModPackages"] = "consoleMessages.backendPhase8ModsLabel",
    };

    internal static void WritePhaseHeader(PipelineContext context, string messageKey)
    {
        string label = ConsoleMessages.Get(messageKey);
        if (context.DebugMode)
        {
            context.Log.Write(ConsoleMessageKind.Phase, label);
            if (Console.IsOutputRedirected && PhaseHeaderUiKeys.TryGetValue(messageKey, out string? uiKey))
            {
                Console.WriteLine(BackendI18nLine.Format(uiKey));
            }

            return;
        }

        if (Console.IsOutputRedirected && PhaseHeaderUiKeys.TryGetValue(messageKey, out string? redirectedUiKey))
        {
            Console.WriteLine();
            Console.WriteLine(BackendI18nLine.Format(redirectedUiKey));
            return;
        }

        Console.WriteLine($"\n{label}");
    }

    internal static void WriteDetectedGameBuild(PipelineContext context)
    {
        if (context.DetectedGameBuild is null)
        {
            return;
        }

        string display = context.DetectedGameBuild.ToDisplayString();
        string message = string.Format(ConsoleMessages.Get("gameBuildDetected"), display);
        if (context.DebugMode)
        {
            context.Log.Write(ConsoleMessageKind.Info, message, always: true);
        }
        else if (Console.IsOutputRedirected)
        {
            // No dedicated UI key yet — keep English for ExactBackendKeys / raw display.
            Console.WriteLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    internal static void LogFlowStep(PipelineContext context, string step)
    {
        if (!context.DebugMode)
        {
            return;
        }

        context.Log.Debug(string.Format(ConsoleMessages.Get("debugFlowStep"), step));
    }

    internal static int ResolveExitCode(PipelineContext context, int orchestratorExitCode) =>
        orchestratorExitCode != 0 ? orchestratorExitCode : (context.Issues.HasErrors ? 1 : 0);

    internal static int FinishPipelineRun(PipelineContext context, System.Diagnostics.Stopwatch totalSw, int assetCount, bool succeeded)
    {
        succeeded = succeeded && !context.Issues.HasErrors;

        if (totalSw.IsRunning)
        {
            totalSw.Stop();
        }

        OutputDirectoryManager.TryDeleteFixerScratchFile(context.BaseOutputDir);

        if (succeeded
            && !context.OutputCacheCompleteHit
            && !string.IsNullOrWhiteSpace(context.DetectedGameType))
        {
            OutputRunCache.WriteStampAndInventory(context, context.DetectedGameType, context.Log);
        }

        if (!context.DebugMode)
        {
            Console.WriteLine();
            BackendI18nLine.Write("extractionCompleteHeader");
            BackendI18nLine.Write("processedAssetsSummary", assetCount.ToString("N0"));
            BackendI18nLine.Write("totalTimeSummary", PipelineRunSupport.FormatElapsed(totalSw.Elapsed));
            Console.WriteLine();
        }
        else
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Complete, "extractionCompleteHeader", always: true);
            context.Log.WriteLocalized(ConsoleMessageKind.Info, "processedAssetsSummary", always: true, assetCount.ToString("N0"));
            context.Log.WriteLocalized(ConsoleMessageKind.Info, "totalTimeSummary", always: true, PipelineRunSupport.FormatElapsed(totalSw.Elapsed));
        }

        // UI key is successExtractionComplete; backend ConsoleMessages key is assetExtractionSuccess.
        if (succeeded)
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

        PipelineIssueReporter.WriteSummary(context, succeeded);
        return ResolveExitCode(context, succeeded ? 0 : 1);
    }
}
