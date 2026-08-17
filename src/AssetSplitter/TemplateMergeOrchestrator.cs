using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace AssetProcessor;

public static class TemplateMergeOrchestrator
{
    public static int Execute(PipelineContext context, string gameType, string[] mainOutputFiles)
    {
        var propertyCache = new TemplatePropertyCache((msg, type) => context.Log.Write(type, msg));
        XmlDocument propertiesDoc = new();
        XmlDocument templatesDoc = new();
        string propertiesPath = Path.Combine(context.SourceXmlFolder, "properties.xml");
        string templatesPath = Path.Combine(context.SourceXmlFolder, "templates.xml");
        try
        {
            XmlDocumentLoadRetry.Load(propertiesDoc, propertiesPath);
            XmlDocumentLoadRetry.Load(templatesDoc, templatesPath);
        }
        catch (XmlException)
        {
            BackendI18nLine.Write("errorLoadingDocuments");
            return 1;
        }
        XmlNode? templatesRoot = templatesDoc.DocumentElement;
        XmlNode? propertiesRoot = propertiesDoc.DocumentElement;

        if (templatesRoot is null || propertiesRoot is null)
        {
            BackendI18nLine.Write("errorLoadingDocuments");
            return 1;
        }

        if (context.DebugMode)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Merge, "mergeCheckingTemplateInheritance", always: true, mainOutputFiles.Length.ToString("N0"));
            context.Log.Write(ConsoleMessageKind.Info, ConsoleMessages.Get("mergeOnlyFixlistTemplates"));
        }
        else
        {
            Console.WriteLine();
            BackendI18nLine.Write("inheritingTemplateProperties", mainOutputFiles.Length.ToString("N0"));
            BackendI18nLine.Write("templateFixlistNote");
            BackendI18nLine.Write("mergeTemplates");
        }

        string[] currentFixlist = TemplateLoader.LoadFixlist(gameType, context.CustomFixlistFile);
        HashSet<string> fixlistSet = new(currentFixlist, StringComparer.OrdinalIgnoreCase);
        propertyCache.Initialize(templatesRoot, gameType, currentFixlist, context.DebugMode);

        var defaultPropertiesIndex = BuildDefaultPropertiesIndex(propertiesRoot);

        int mergedCount = 0;
        int actuallyMergedCount = 0;
        Lock templateMergeLock = new();

        Parallel.ForEach(
            mainOutputFiles,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = PipelineParallelism.ForXmlFileIo(context),
                CancellationToken = context.CancellationToken,
            },
            assetFilePath =>
            {
                int currentFileIndex;
                lock (templateMergeLock)
                {
                    mergedCount++;
                    currentFileIndex = mergedCount;
                }

                if (FormattingService.ShouldReportProgress(context, currentFileIndex, mainOutputFiles.Length))
                {
                    string currentFile = Path.GetFileNameWithoutExtension(assetFilePath);
                    string displayName = AssetProcessorFileSystem.ExtractDisplayName(currentFile);
                    string? templateName = context.DebugMode ? null : AssetProcessorFileSystem.TryReadTemplateFromAssetFile(assetFilePath);
                    string mergeProgress = context.DebugMode
                        ? string.Format(ConsoleMessages.Get("mergingAssetProgress"), displayName)
                        : AssetProgressFormatter.FromAssetFileStem("Merging", currentFile, templateName);
                    context.ProgressReporter.ReportProgress(mergeProgress, currentFileIndex.ToString(), mainOutputFiles.Length.ToString());
                }

                try
                {
                    if (!TryMergeAssetFile(
                            context,
                            assetFilePath,
                            fixlistSet,
                            defaultPropertiesIndex,
                            gameType,
                            propertyCache,
                            templateMergeLock,
                            ref actuallyMergedCount))
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    context.Issues.ReportMergeAssetFailed(assetFilePath, ex.Message);
                    context.Log.WriteLocalized(ConsoleMessageKind.Error, "mergeAssetFailed", always: true, assetFilePath, ex.Message);
                }
            });

        context.ProgressReporter.ReportProgress(
            ConsoleMessages.Get("mergingProgress"),
            mainOutputFiles.Length.ToString(),
            mainOutputFiles.Length.ToString());

        if (context.DebugMode)
        {
            context.DebugStats.WriteTemplateMergeSummary(context.Log);
            context.Log.WriteLocalized(ConsoleMessageKind.Complete, "templateInheritanceComplete", always: true, actuallyMergedCount, mainOutputFiles.Length);
        }
        else
        {
            BackendI18nLine.Write("mergedTemplatesCount", actuallyMergedCount.ToString("N0"));
        }

        return context.Issues.HasErrors ? 1 : 0;
    }

    private static Dictionary<string, XmlNode> BuildDefaultPropertiesIndex(XmlNode propertiesRoot)
    {
        var index = new Dictionary<string, XmlNode>(StringComparer.OrdinalIgnoreCase);
        XmlNodeList? defaultValuesNodes = propertiesRoot.SelectNodes("//DefaultValues/*");
        if (defaultValuesNodes is not null)
        {
            foreach (XmlNode node in defaultValuesNodes)
            {
                index[node.Name] = node;
            }
        }
        return index;
    }

    private static bool TryMergeAssetFile(
        PipelineContext context,
        string assetFilePath,
        HashSet<string> fixlistSet,
        Dictionary<string, XmlNode> defaultPropertiesIndex,
        string gameType,
        TemplatePropertyCache propertyCache,
        Lock templateMergeLock,
        ref int actuallyMergedCount)
    {
        string templateName = ReadTemplateNameFromFile(assetFilePath);
        if (string.IsNullOrEmpty(templateName) || !fixlistSet.Contains(templateName))
        {
            if (!string.IsNullOrEmpty(templateName))
            {
                context.DebugStats.RecordTemplateMergeSkipped();
                if (context.DebugMode)
                {
                    context.Log.Debug(string.Format(ConsoleMessages.Get("debugTemplateSkippedNotInFixlist"), templateName));
                }
            }

            return false;
        }

        XmlDocument assetDoc = new();
        XmlDocumentLoadRetry.Load(assetDoc, assetFilePath);

        XmlNode? documentElement = assetDoc.DocumentElement;
        if (documentElement == null)
        {
            return false;
        }

        XmlNode? guidNode = documentElement.SelectSingleNode("//Asset/Values/Standard/GUID")
            ?? documentElement.SelectSingleNode("Values/Standard/GUID")
            ?? documentElement.SelectSingleNode("//Values/Standard/GUID");
        if (guidNode == null)
        {
            return false;
        }

        string assetGuid = guidNode.InnerText;
        if (context.DebugMode)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugMergeProcessingFile"), Path.GetFileName(assetFilePath), templateName));
        }

        context.DebugStats.RecordTemplateMergeApplied();

        XmlDocument mergedDoc = new();
        string fileName = Path.GetFileName(assetFilePath);
        string outputPath = Path.Combine(context.AssetOutputDir, fileName);

        var mergedModOps = mergedDoc.CreateElement("ModOps");
        mergedDoc.AppendChild(mergedModOps);
        var mergedModOp = mergedDoc.CreateElement("ModOp");
        mergedModOp.SetAttribute("GUID", assetGuid);
        mergedModOp.SetAttribute("Type", "Replace");
        mergedModOp.SetAttribute("Path", "/");
        mergedModOps.AppendChild(mergedModOp);
        var mergedAsset = mergedDoc.CreateElement("Asset");
        mergedModOp.AppendChild(mergedAsset);
        var mergedTemplate = mergedDoc.CreateElement("Template");
        mergedTemplate.InnerText = templateName;
        mergedAsset.AppendChild(mergedTemplate);
        var mergedValues = mergedDoc.CreateElement("Values");
        mergedAsset.AppendChild(mergedValues);

        AppendCachedTemplateProperties(context, mergedDoc, mergedValues, templateName, gameType, propertyCache, templateMergeLock);
        ApplyDefaultProperties(context, mergedDoc, mergedValues, defaultPropertiesIndex, templateMergeLock);

        TemplateMergeService.FixFile(context, mergedDoc, assetDoc, outputPath);

        lock (templateMergeLock)
        {
            actuallyMergedCount++;
        }

        if (context.DebugMode && actuallyMergedCount % 50 == 0)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("fullInheritanceAppliedTo"), templateName));
        }

        return true;
    }

    private static string ReadTemplateNameFromFile(string assetFilePath)
    {
        try
        {
            var doc = XmlDocumentLoadRetry.LoadXDocument(assetFilePath);
            var templateElement = doc.Descendants("Template").FirstOrDefault();
            if (templateElement != null)
            {
                return templateElement.Value;
            }
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException or NotSupportedException)
        {
            // XDocument.Load failed — fall through to legacy parser
        }

        using StreamReader reader = new(assetFilePath, Encoding.UTF8, true, 512);
        string header = reader.ReadToEnd();
        int templateStart = header.IndexOf("<Template>");
        if (templateStart <= 0)
        {
            return "";
        }

        int templateEnd = header.IndexOf("</Template>", templateStart);
        if (templateEnd <= templateStart)
        {
            return "";
        }

        return header.Substring(templateStart + 10, templateEnd - templateStart - 10);
    }

    private static void AppendCachedTemplateProperties(
        PipelineContext context,
        XmlDocument mergedDoc,
        XmlNode valuesNode,
        string templateName,
        string gameType,
        TemplatePropertyCache propertyCache,
        Lock sharedXmlLock)
    {
        if (!propertyCache.TryGetRawProperties(templateName, out XElement? cachedProps) || cachedProps is null)
        {
            if (context.DebugMode)
            {
                context.Log.WriteLocalized(ConsoleMessageKind.Warning, "templateNotFoundInCache", always: true, templateName);
            }

            return;
        }

        // Serialize CreateReader/ToXmlNode on shared cached XElements under Parallel.ForEach.
        lock (sharedXmlLock)
        {
            if (GameTypeDetector.IsAnno117(gameType))
            {
                if (propertyCache.TryGetFilteredAnno117Properties(templateName, out IReadOnlyList<XNode>? filtered)
                    && filtered is not null)
                {
                    foreach (XNode node in filtered)
                    {
                        if (node is XElement element)
                        {
                            valuesNode.AppendChild(mergedDoc.ImportNode(element.ToXmlNode(), true));
                        }
                    }
                }

                return;
            }

            foreach (XNode node in cachedProps.Nodes())
            {
                if (node is XElement element)
                {
                    valuesNode.AppendChild(mergedDoc.ImportNode(element.ToXmlNode(), true));
                }
            }
        }
    }

    private static void ApplyDefaultProperties(
        PipelineContext context,
        XmlDocument mergedDoc,
        XmlNode valuesNode,
        Dictionary<string, XmlNode> defaultPropertiesIndex,
        Lock sharedXmlLock)
    {
        if (context.NoDefaultProperties || !valuesNode.HasChildNodes)
        {
            return;
        }

        foreach (XmlNode propertyGroup in valuesNode.ChildNodes)
        {
            if (!defaultPropertiesIndex.TryGetValue(propertyGroup.Name, out XmlNode? defaultValues))
            {
                continue;
            }

            var existingChildNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode child in propertyGroup.ChildNodes)
            {
                existingChildNames.Add(child.Name);
            }

            List<XmlNode> nodesToAdd = [];
            // Shared properties.xml nodes are not thread-safe under Parallel.ForEach.
            lock (sharedXmlLock)
            {
                foreach (XmlNode defaultProperty in defaultValues.ChildNodes)
                {
                    if (!existingChildNames.Contains(defaultProperty.Name))
                    {
                        nodesToAdd.Add(mergedDoc.ImportNode(defaultProperty, true));
                    }
                }
            }

            foreach (XmlNode nodeToAdd in nodesToAdd)
            {
                propertyGroup.AppendChild(nodeToAdd);
            }
        }
    }
}
