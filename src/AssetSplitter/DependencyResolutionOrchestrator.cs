using System.Xml;

namespace AssetProcessor;

/// <summary>Phase 6 orchestration: resolves BaseAssetGUID parent chains into child asset ModOp XML.</summary>
public static class DependencyResolutionOrchestrator
{
    public static void Execute(PipelineContext context, string[] baseAssetGuidFiles)
    {
        if (!context.FixDependencies)
        {
            return;
        }

        string[] baseAssetFilesToResolve = [.. baseAssetGuidFiles.Where(file => !file.Contains("PaMSy", StringComparison.OrdinalIgnoreCase))];
        Console.WriteLine();
        BackendI18nLine.Write("resolvingDepsFor", baseAssetFilesToResolve.Length.ToString("N0"));
        context.Log.WriteLocalized(ConsoleMessageKind.Fix, "mergingInheritedProperties", always: true);

        context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepStartingResolution"), baseAssetFilesToResolve.Length));

        if (context.GuidIndex is null) { context.Log.WriteLocalized(ConsoleMessageKind.Error, "guidIndexNull", always: true); return; }

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
        var pendingFiles = new HashSet<string>(baseAssetFilesToResolve, StringComparer.OrdinalIgnoreCase);
        var resolvedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolvingGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string baseAssetFilePath in baseAssetFilesToResolve)
        {
            context.ThrowIfCancellationRequested();
            resolvedCount++;
            try
            {
                ResolveSingleFile(
                    context,
                    cache,
                    baseAssetFilePath,
                    resolvedCount,
                    baseAssetFilesToResolve.Length,
                    pendingFiles,
                    resolvedFiles,
                    resolvingGuids);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One bad file (save failure, malformed parent, ...) must not abort the whole phase.
                context.Issues.ReportMergeAssetFailed(baseAssetFilePath, ex.Message);
                context.Log.WriteLocalized(ConsoleMessageKind.Error, "mergeAssetFailed", always: true, baseAssetFilePath, ex.Message);
            }
        }

        sw.Stop();
        context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepComplete"), resolvedCount));

        // Ensure final 100% progress marker
        if (baseAssetFilesToResolve.Length > 0)
        {
            context.ProgressReporter.ReportProgress(
                string.Format(ConsoleMessages.Get("dependenciesResolvedComplete"), baseAssetFilesToResolve.Length.ToString("N0")),
                baseAssetFilesToResolve.Length.ToString(),
                baseAssetFilesToResolve.Length.ToString());
        }

        cache.WriteSummary(context.Log);
        cache.Clear();

        OutputDirectoryManager.TryRemoveEmptyStagingDirectory(context);

        if (!context.DebugMode)
        {
            BackendI18nLine.Write("dependencyResolutionComplete", resolvedCount.ToString("N0"), PipelineRunSupport.FormatElapsed(sw.Elapsed));
            context.Log.WriteLocalized(ConsoleMessageKind.Complete, "dependenciesResolvedComplete", always: true, resolvedCount.ToString("N0"));
        }
    }

    private static void ResolveSingleFile(
        PipelineContext context,
        DependencyResolutionCache cache,
        string baseAssetFilePath,
        int resolvedCount,
        int totalCount,
        HashSet<string> pendingFiles,
        HashSet<string> resolvedFiles,
        HashSet<string> resolvingGuids)
    {
        if (!resolvedFiles.Add(baseAssetFilePath))
        {
            return;
        }

        string fileName = Path.GetFileName(baseAssetFilePath);
        string fullFileName = Path.GetFileNameWithoutExtension(fileName);
        string displayName = AssetProcessorFileSystem.ExtractDisplayName(fullFileName);
        string outputPath = Path.Combine(context.AssetOutputDir, fileName);

        XmlDocument childAssetDoc = new();
        try
        {
            XmlDocumentLoadRetry.Load(childAssetDoc, baseAssetFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or ArgumentException or NotSupportedException)
        {
            context.Issues.ReportDependencyChildLoadFailed(baseAssetFilePath, ex.Message, displayName);
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "issueDependencyChildLoadFailedMessage", always: true, baseAssetFilePath, ex.Message);
            return;
        }

        XmlNode? baseAssetGuidNode = childAssetDoc.DocumentElement?.SelectSingleNode("//Asset/BaseAssetGUID");
        if (baseAssetGuidNode == null)
        {
            if (DeveloperTrace.ShouldReportProgress(context, resolvedCount, totalCount, normalInterval: 50))
            {
                string? scanTemplate = context.DebugMode
                    ? null
                    : AssetProcessorFileSystem.TryReadTemplateFromAssetXml(childAssetDoc);
                string scanProgress = context.DebugMode
                    ? string.Format(ConsoleMessages.Get("scanningAssetProgress"), displayName)
                    : AssetProgressFormatter.FromAssetFileStem("Scanning", fullFileName, scanTemplate);
                context.ProgressReporter.ReportProgress(scanProgress, resolvedCount.ToString(), totalCount.ToString());
            }

            context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepNoBaseAssetGuid"), displayName));
            return;
        }

        if (DeveloperTrace.ShouldReportProgress(context, resolvedCount, totalCount, normalInterval: 50))
        {
            string? templateName = context.DebugMode
                ? null
                : AssetProcessorFileSystem.TryReadTemplateFromAssetXml(childAssetDoc);
            string resolveProgress = context.DebugMode
                ? string.Format(ConsoleMessages.Get("resolvingAssetProgress"), displayName, baseAssetGuidNode.InnerText)
                : AssetProgressFormatter.FromAssetFileStem("Resolving", fullFileName, templateName);
            context.ProgressReporter.ReportProgress(
                resolveProgress,
                resolvedCount.ToString(),
                totalCount.ToString());
        }

        if (context.DebugMode && resolvedCount % 80 == 0)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("depResolutionLine"), displayName, baseAssetGuidNode.InnerText));
        }

        string parentGuid = baseAssetGuidNode.InnerText;
        context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepResolving"), displayName, parentGuid));

        ResolveParentChainIfNeeded(
            context,
            cache,
            parentGuid,
            resolvedCount,
            totalCount,
            pendingFiles,
            resolvedFiles,
            resolvingGuids);

        XmlDocument? parentAssetDoc = cache.GetOrLoadParentAsset(parentGuid, displayName, baseAssetFilePath);
        if (parentAssetDoc?.CloneNode(true) is XmlDocument parentCopy)
        {
            TemplateMergeService.FixFile(context, parentCopy, childAssetDoc, outputPath, MergeTraceKind.DependencyParentMerge);
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugDepSuccessfullyMerged"), displayName));
            if (File.Exists(outputPath))
            {
                OutputDirectoryManager.TryRemoveMergedStagingFile(context, baseAssetFilePath);
                TrackRepairOutputMove(context, baseAssetFilePath, outputPath);
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

    /// <summary>
    /// Repair runs format only <see cref="PipelineContext.RepairOutputFiles"/>. The merged child now
    /// lives at <paramref name="outputPath"/> and its staging copy was removed, so swap the path;
    /// otherwise Phase 7 never formats (comments, cleanup, template folders) the merged asset.
    /// </summary>
    private static void TrackRepairOutputMove(PipelineContext context, string stagingPath, string outputPath)
    {
        if (context.RepairOnlyGuids is null)
        {
            return;
        }

        List<string> files = context.RepairOutputFiles;
        files.RemoveAll(path => string.Equals(path, stagingPath, StringComparison.OrdinalIgnoreCase));
        if (!files.Contains(outputPath, StringComparer.OrdinalIgnoreCase))
        {
            files.Add(outputPath);
        }
    }

    private static void ResolveParentChainIfNeeded(
        PipelineContext context,
        DependencyResolutionCache cache,
        string parentGuid,
        int resolvedCount,
        int totalCount,
        HashSet<string> pendingFiles,
        HashSet<string> resolvedFiles,
        HashSet<string> resolvingGuids)
    {
        if (string.IsNullOrWhiteSpace(parentGuid) || context.GuidIndex is null)
        {
            return;
        }

        if (!resolvingGuids.Add(parentGuid))
        {
            return;
        }

        try
        {
            string? parentPath = context.GuidIndex.Find(parentGuid);
            if (string.IsNullOrEmpty(parentPath) || !pendingFiles.Contains(parentPath) || resolvedFiles.Contains(parentPath))
            {
                return;
            }

            ResolveSingleFile(
                context,
                cache,
                parentPath,
                resolvedCount,
                totalCount,
                pendingFiles,
                resolvedFiles,
                resolvingGuids);
        }
        finally
        {
            resolvingGuids.Remove(parentGuid);
        }
    }
}
