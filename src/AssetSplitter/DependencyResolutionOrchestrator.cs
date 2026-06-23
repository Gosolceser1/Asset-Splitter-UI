using System.Xml;

namespace AssetProcessor;

/// <summary>Phase 5 orchestration: resolves BaseAssetGUID parent chains into child asset ModOp XML.</summary>
public static class DependencyResolutionOrchestrator
{
    public static void Execute(PipelineContext context, string[] baseAssetGuidFiles)
    {
        if (!context.AssetFix)
        {
            return;
        }

        string[] baseAssetFilesToResolve = [.. baseAssetGuidFiles.Where(file => !file.Contains("PaMSy", StringComparison.OrdinalIgnoreCase))];
        Console.WriteLine($"\n{ConsoleMessages.Get("resolvingDepsFor").Replace("{0}", baseAssetFilesToResolve.Length.ToString("N0"))}");
        context.Log.Write("FIX", ConsoleMessages.Get("mergingInheritedProperties"), always: true);

        context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepStartingResolution"), baseAssetFilesToResolve.Length));

        if (context.GuidIndex is null) { context.Log.Write("ERROR", ConsoleMessages.Get("guidIndexNull"), always: true); return; }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var cache = new DependencyResolutionCache(
            context.GuidIndex,
            context,
            context.Issues,
            AssetProcessorConsole.WriteColoredMessage,
            context.DebugMode,
            context.DebugStats);
        cache.Initialize();

        int resolvedCount = 0;
        foreach (string baseAssetFilePath in baseAssetFilesToResolve)
        {
            resolvedCount++;
            ResolveSingleFile(context, cache, baseAssetFilePath, resolvedCount, baseAssetFilesToResolve.Length);
        }

        sw.Stop();
        context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepComplete"), resolvedCount));

        // Ensure final 100% progress marker
        if (baseAssetFilesToResolve.Length > 0)
        {
            context.ProgressReporter.OutputFixer(
                string.Format(ConsoleMessages.Get("dependenciesResolvedComplete"), baseAssetFilesToResolve.Length.ToString("N0")),
                baseAssetFilesToResolve.Length.ToString(),
                baseAssetFilesToResolve.Length.ToString());
        }

        cache.WriteSummary(context.Log);
        cache.Clear();

        OutputDirectoryManager.TryRemoveEmptyStagingDirectory(context);

        if (!context.DebugMode)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("dependencyResolutionComplete"), resolvedCount.ToString("N0"), sw.Elapsed.ToString(@"mm\:ss")));
            context.Log.Write("COMPLETE",
                string.Format(ConsoleMessages.Get("dependenciesResolvedComplete"), resolvedCount.ToString("N0")),
                always: true);
        }
    }

    private static void ResolveSingleFile(PipelineContext context, DependencyResolutionCache cache, string baseAssetFilePath, int resolvedCount, int totalCount)
    {
        string fileName = Path.GetFileName(baseAssetFilePath);
        string fullFileName = Path.GetFileNameWithoutExtension(fileName);
        string displayName = AssetProcessorFileSystem.ExtractDisplayName(fullFileName);
        string outputPath = Path.Combine(context.AssetOut, fileName);

        XmlDocument childAssetDoc = new();
        try
        {
            childAssetDoc.Load(baseAssetFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or ArgumentException or NotSupportedException)
        {
            context.Issues.ReportDependencyChildLoadFailed(baseAssetFilePath, ex.Message, displayName);
            context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("issueDependencyChildLoadFailedMessage"), baseAssetFilePath, ex.Message), always: true);
            return;
        }

        XmlNode? baseAssetGuidNode = childAssetDoc.DocumentElement?.SelectSingleNode("//Asset/BaseAssetGUID");

        if (DeveloperTrace.ShouldReportProgress(context, resolvedCount, totalCount, normalInterval: 50))
        {
            if (baseAssetGuidNode != null)
            {
                string? templateName = context.DebugMode
                    ? null
                    : AssetProcessorFileSystem.TryReadTemplateFromAssetXml(childAssetDoc);
                string resolveProgress = context.DebugMode
                    ? string.Format(ConsoleMessages.Get("resolvingAssetProgress"), displayName, baseAssetGuidNode.InnerText)
                    : AssetProgressFormatter.FromAssetFileStem("Resolving", fullFileName, templateName);
                context.ProgressReporter.OutputFixer(
                    resolveProgress,
                    resolvedCount.ToString(),
                    totalCount.ToString());
            }
            else
            {
                string? scanTemplate = context.DebugMode
                    ? null
                    : AssetProcessorFileSystem.TryReadTemplateFromAssetXml(childAssetDoc);
                string scanProgress = context.DebugMode
                    ? string.Format(ConsoleMessages.Get("scanningAssetProgress"), displayName)
                    : AssetProgressFormatter.FromAssetFileStem("Scanning", fullFileName, scanTemplate);
                context.ProgressReporter.OutputFixer(scanProgress, resolvedCount.ToString(), totalCount.ToString());
            }
        }

        if (context.DebugMode && resolvedCount % 80 == 0 && baseAssetGuidNode != null)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("depResolutionLine"), displayName, baseAssetGuidNode.InnerText));
        }

        if (baseAssetGuidNode == null)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepNoBaseAssetGuid"), displayName));
            return;
        }

        string parentGuid = baseAssetGuidNode.InnerText;
        context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepResolving"), displayName, parentGuid));

        XmlDocument? parentAssetDoc = cache.GetOrLoadParentAsset(parentGuid, displayName, baseAssetFilePath);
        if (parentAssetDoc?.CloneNode(true) is XmlDocument parentCopy)
        {
            TemplateMergeService.FixFile(context, parentCopy, childAssetDoc, outputPath, MergeTraceKind.DependencyParentMerge);
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepSuccessfullyMerged"), displayName));
            if (File.Exists(outputPath))
            {
                OutputDirectoryManager.TryRemoveMergedStagingFile(context, baseAssetFilePath);
                string assetGuid = childAssetDoc.DocumentElement is not null
                    ? XmlNodeText.GetValue(childAssetDoc.DocumentElement, "//Values/Standard/GUID")
                    : "";
                if (!string.IsNullOrEmpty(assetGuid))
                {
                    context.GuidIndex?.UpdatePath(assetGuid, outputPath);
                }
            }
        }
        else
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepCouldNotLoadParent"), parentGuid, displayName));
        }
    }
}
