using System.Xml;

namespace AssetProcessor;

/// <summary>Phase 7 orchestration: format extracted XML (cleanup, comments, template folders).</summary>
public static class FormattingService
{
    private const string GoldCoinGuid = "1010017";
    private const string TimberGuid = "1010207";
    private const string PlanksGuid = "1010218";
    private static readonly string[] BaseCostGuids = [GoldCoinGuid, TimberGuid, PlanksGuid];

    private readonly record struct FormatFileContext(
        PipelineContext Context,
        bool AllowMove,
        bool IsAnno1800,
        string[] BaseAfricanCosts,
        string[] BaseDefaultCosts,
        PropertyScanResult PropertyScan,
        InheritedIndexCommentCache InheritedIndexCache,
        string ProgressVerb,
        int TotalFiles,
        System.Collections.Concurrent.ConcurrentQueue<(string FilePath, string TemplateName)> PendingTemplateMoves);

    public static int FormatXml(
        PipelineContext context,
        string[] xmlFilePaths,
        PropertyScanResult propertyScan,
        string gameType = "anno1800",
        bool allowMove = true,
        bool skipInitialMessage = false,
        string progressVerb = "Processing")
    {
        bool isAnno1800 = gameType.Equals("anno1800", StringComparison.OrdinalIgnoreCase);
        if (!skipInitialMessage)
        {
            WriteFormattingStart(context, xmlFilePaths.Length, isAnno1800);
        }

        string[] africanIngredients = AssetProcessorConfiguration.GetAfricanIngredients(context.RegionalIngredientsConfig, context);
        string[] defaultIngredients = AssetProcessorConfiguration.GetDefaultIngredients(context.RegionalIngredientsConfig, context);
        string[] baseAfricanCosts = BaseCostGuids.Concat(africanIngredients ?? Array.Empty<string>()).ToArray();
        string[] baseDefaultCosts = BaseCostGuids.Concat(defaultIngredients ?? Array.Empty<string>()).ToArray();
        int processedCount = 0;
        Lock progressLock = new();
        var inheritedIndexCache = new InheritedIndexCommentCache(context);
        var fileCtx = new FormatFileContext(
            context,
            allowMove,
            isAnno1800,
            baseAfricanCosts,
            baseDefaultCosts,
            propertyScan,
            inheritedIndexCache,
            progressVerb,
            xmlFilePaths.Length,
            new System.Collections.Concurrent.ConcurrentQueue<(string FilePath, string TemplateName)>());
        System.Threading.Tasks.Parallel.ForEach(
            xmlFilePaths,
            new System.Threading.Tasks.ParallelOptions
            {
                MaxDegreeOfParallelism = PipelineParallelism.ForXmlFileIo(context),
                CancellationToken = context.CancellationToken,
            },
            xmlFilePath => FormatSingleFile(fileCtx, xmlFilePath, ref processedCount, progressLock));

        // Template-folder moves run after the parallel pass: moving a parent while another worker
        // still needs it (InheritedIndex comments load parents from their GUID-index path) made the
        // load fail, cached null and dropped comments for every child of that parent at random.
        while (fileCtx.PendingTemplateMoves.TryDequeue(out (string FilePath, string TemplateName) move))
        {
            MoveToTemplateFolder(context, move.FilePath, move.TemplateName, allowMove);
        }

        // Do not emit a second progress operation ("Formatting...") here — that double-counted
        // completed units in the GUI tracker and jumped the bar to 100% before staging finished.
        if (xmlFilePaths.Length > 0)
        {
            context.ProgressReporter.ReportProgress(
                AssetProgressFormatter.FromAssetFileStem(progressVerb, "complete"),
                xmlFilePaths.Length.ToString(),
                xmlFilePaths.Length.ToString());
        }

        WriteFormattingComplete(context, processedCount, isAnno1800);
        if (context.DebugMode)
        {
            context.DebugStats.WriteParentCacheSummary(context.Log);
        }

        return context.Issues.HasErrors ? 1 : 0;
    }

    public static void AnnotateFilesWithGuidComments(
        PipelineContext context,
        string[] filePaths,
        PropertyScanResult propertyScan) =>
        AssetXmlCommentAnnotator.AnnotateFilesWithGuidComments(context, filePaths, propertyScan);

    public static bool ShouldReportProgress(PipelineContext context, int currentIndex, int totalFiles) =>
        DeveloperTrace.ShouldEmitProgressLine(context, currentIndex, totalFiles);

    private static void WriteFormattingStart(PipelineContext context, int fileCount, bool isAnno1800)
    {
        if (context.DebugMode)
        {
            var features = new List<string>();
            if (PipelineFeatureGates.NeedsEnrichmentCleanup(context))
            {
                features.Add(ConsoleMessages.Get("formatFeatureVectorCleanup").Trim());
                if (isAnno1800)
                {
                    features.Add(ConsoleMessages.Get("formatFeatureRegionalIngredients").Trim());
                }
            }

            if (context.AddComments)
            {
                features.Add(ConsoleMessages.Get("formatFeatureComments").Trim());
            }

            if (context.CreateTemplateFolders)
            {
                features.Add(ConsoleMessages.Get("formatFeatureFolders").Trim());
            }

            string featureText = features.Count > 0 ? string.Join(", ", features) : ConsoleMessages.Get("formatFeatureFlaggedOnly");
            context.Log.WriteLocalized(ConsoleMessageKind.Format, "formatProcessingFiles", always: true, fileCount.ToString("N0"), featureText);
            context.Log.Debug(context.AddComments
                ? ConsoleMessages.Get("debugPhase7AddingComments")
                : ConsoleMessages.Get("debugPhase7SkippingComments"));
            context.Log.Debug(context.CreateTemplateFolders
                ? ConsoleMessages.Get("debugStep4OrganizingFolders")
                : ConsoleMessages.Get("debugStep4KeepingMainDirectory"));
            return;
        }

        Console.WriteLine();
        BackendI18nLine.Write("finalProcessingFiles", fileCount.ToString("N0"));

        string applying = string.Empty;
        if (PipelineFeatureGates.NeedsEnrichmentCleanup(context))
        {
            applying = ConsoleMessages.Get("applyingXmlCleanup");
            if (isAnno1800)
            {
                applying += ConsoleMessages.Get("applyingRegionalIngredients");
            }
        }

        if (context.AddComments)
        {
            applying += ConsoleMessages.Get("applyingTranslations");
        }

        if (context.CreateTemplateFolders)
        {
            applying += ConsoleMessages.Get("applyingFolderOrganization");
        }

        if (!string.IsNullOrEmpty(applying))
        {
            Console.WriteLine(applying.Trim());
        }

        if (Console.IsOutputRedirected)
        {
            BackendI18nLine.Write("formattingAssets");
        }
        else
        {
            Console.Write(ConsoleMessages.Get("formattingAssets"));
        }
    }

    private static void WriteFormattingComplete(PipelineContext context, int processedCount, bool isAnno1800)
    {
        if (!context.DebugMode)
        {
            BackendI18nLine.Write("filesFormattedCount", processedCount.ToString("N0"));
            return;
        }

        context.Log.WriteLocalized(ConsoleMessageKind.Complete, "finalProcessingCompleted");
        if (PipelineFeatureGates.NeedsEnrichmentCleanup(context))
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Info, "vectorCleanupApplied", always: true, processedCount.ToString("N0"));
            if (isAnno1800)
            {
                context.Log.Write(ConsoleMessageKind.Info, ConsoleMessages.Get("regionalIngredientsApplied"));
            }
        }
        else
        {
            context.Log.Write(ConsoleMessageKind.Info, ConsoleMessages.Get("vectorCleanupSkippedSplitOnly"));
        }

        context.Log.Write(
            ConsoleMessageKind.Info,
            string.Format(
                ConsoleMessages.Get("translationCommentsSummary"),
                context.AddComments ? ConsoleMessages.Get("addedCFlag") : ConsoleMessages.Get("skippedNoCFlag")));
        context.Log.Write(
            ConsoleMessageKind.Info,
            string.Format(
                ConsoleMessages.Get("templateOrganizationSummary"),
                context.CreateTemplateFolders ? ConsoleMessages.Get("appliedTFlag") : ConsoleMessages.Get("skippedNoTFlag")));
        context.DebugStats.WriteFormattingSummary(context.Log);
    }

    private static void FormatSingleFile(FormatFileContext ctx, string xmlFilePath, ref int processedCount, Lock progressLock)
    {
        try
        {
            if (ctx.Context.DebugMode)
            {
                ctx.Context.Log.Debug(string.Format(
                    ConsoleMessages.Get("debugFormatProcessingFile"),
                    Path.GetFileName(xmlFilePath)));
            }

            XmlDocument xmlDoc = new();
            try
            {
                int currentFileIndex;
                lock (progressLock)
                {
                    processedCount++;
                    currentFileIndex = processedCount;
                }

                try
                {
                    XmlDocumentLoadRetry.Load(xmlDoc, xmlFilePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
                {
                    ctx.Context.Log.WriteLocalized(ConsoleMessageKind.Error, "failedToLoadXmlFile", always: true, xmlFilePath);
                    ctx.Context.Log.Debug(string.Format(ConsoleMessages.Get("debugXmlLoadError"), ex.Message));
                    ctx.Context.Issues.ReportFormatFileFailed(xmlFilePath, ex.Message);
                    return;
                }

                if (ShouldReportProgress(ctx.Context, currentFileIndex, ctx.TotalFiles))
                {
                    string fileName = Path.GetFileNameWithoutExtension(xmlFilePath);
                    string displayName = AssetProcessorFileSystem.ExtractDisplayName(fileName);
                    string? progressTemplate = ctx.Context.DebugMode
                        ? null
                        : AssetProcessorFileSystem.TryReadTemplateFromAssetXml(xmlDoc);
                    string formatProgress = ctx.Context.DebugMode
                        ? string.Format(ConsoleMessages.Get("processingAssetProgress"), displayName)
                        : AssetProgressFormatter.FromAssetFileStem(ctx.ProgressVerb, fileName, progressTemplate);
                    ctx.Context.ProgressReporter.ReportProgress(
                        formatProgress,
                        currentFileIndex.ToString(),
                        ctx.TotalFiles.ToString());
                }

                XmlNode? documentElement = xmlDoc.DocumentElement;
                if (PipelineFeatureGates.NeedsEnrichmentCleanup(ctx.Context))
                {
                    int vectorElementsRemoved = AssetRegionalCostFormatter.RemoveVectorElements(documentElement);
                    if (vectorElementsRemoved > 0)
                    {
                        ctx.Context.Log.Debug(string.Format(ConsoleMessages.Get("debugVectorElementsRemoved"), vectorElementsRemoved));
                    }
                }

                XmlNode? templateNode = xmlDoc.SelectSingleNode("//Asset/Template");
                string templateName = templateNode?.InnerText ?? "Unknown";
                if (templateNode is null)
                {
                    ctx.Context.DebugStats.RecordFormatMissingTemplateNode();
                    if (ctx.Context.DebugMode)
                    {
                        ctx.Context.Log.Debug(string.Format(
                            ConsoleMessages.Get("debugFormatNoTemplateNode"),
                            Path.GetFileName(xmlFilePath)));
                    }
                }

                if (ctx.IsAnno1800 && PipelineFeatureGates.NeedsEnrichmentCleanup(ctx.Context))
                {
                    AssetRegionalCostFormatter.ApplyRegionalIngredients(
                        ctx.Context,
                        xmlDoc,
                        ctx.BaseAfricanCosts,
                        ctx.BaseDefaultCosts);
                }

                if (ctx.Context.AddComments && documentElement is not null)
                {
                    int commentsAdded = AssetXmlCommentAnnotator.AddTranslationComments(
                        ctx.Context,
                        xmlDoc,
                        documentElement,
                        ctx.PropertyScan,
                        ctx.InheritedIndexCache);
                    if (commentsAdded > 0)
                    {
                        ctx.Context.DebugStats.RecordFormatComments(commentsAdded);
                        if (ctx.Context.DebugMode)
                        {
                            ctx.Context.Log.Debug(string.Format(ConsoleMessages.Get("debugTranslatedCommentsAdded"), commentsAdded));
                        }
                    }
                }

                GeneratedXmlFootprint.Save(xmlDoc, xmlFilePath, ctx.Context);
                if (ctx.Context.CreateTemplateFolders && ctx.AllowMove)
                {
                    ctx.PendingTemplateMoves.Enqueue((xmlFilePath, templateName));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
            {
                ctx.Context.Issues.ReportUnexpectedFileError(ex.Message);
                ctx.Context.Log.Write(ConsoleMessageKind.Error, string.Format(ConsoleMessages.Get("unexpectedFileProcessingError"), ex.Message), always: true);
                ctx.Context.Log.Debug($"[DEBUG] Stack trace: {ex.StackTrace}");
            }
        }
        catch (Exception ex)
        {
            ctx.Context.Issues.ReportFormatFileFailed(xmlFilePath, ex.Message);
            ctx.Context.Log.Write(ConsoleMessageKind.Error, string.Format(ConsoleMessages.Get("formatSingleFileFailed"), xmlFilePath, ex.Message));
        }
    }

    private static void MoveToTemplateFolder(PipelineContext context, string xmlFilePath, string templateName, bool allowMove)
    {
        if (!context.CreateTemplateFolders || !allowMove)
        {
            return;
        }

        context.DebugStats.RecordFormatTemplateMove();
        if (context.DebugMode)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugStep4MovingFileToTemplateFolder"), templateName));
        }

        try
        {
            string destDir = OutputDirectoryManager.EnsureTemplateSubfolder(context, templateName);
            string destFile = Path.Combine(destDir, Path.GetFileName(xmlFilePath));
            string sourceFull = Path.GetFullPath(xmlFilePath);
            string destFull = Path.GetFullPath(destFile);
            if (sourceFull.Equals(destFull, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Repair / DiffRepair / re-format over an existing template layout must replace,
            // not WARN and leave a flat orphan beside the old template copy.
            if (File.Exists(destFull))
            {
                File.Delete(destFull);
            }

            File.Move(sourceFull, destFull);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            context.Issues.ReportMoveToTemplateFolderFailed(ex.Message);
            context.Log.WriteLocalized(ConsoleMessageKind.Warning, "couldNotMoveToTemplateFolder", always: true, ex.Message);
        }
    }
}
