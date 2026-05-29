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
        string propertiesXml = File.ReadAllText(Path.Combine(context.SourceXmlFolder, "properties.xml"), Encoding.UTF8);
        string templatesXml = File.ReadAllText(Path.Combine(context.SourceXmlFolder, "templates.xml"), Encoding.UTF8);
        propertiesDoc.LoadXml(propertiesXml);
        templatesDoc.LoadXml(templatesXml);
        XmlNode? templatesRoot = templatesDoc.DocumentElement;
        XmlNode? propertiesRoot = propertiesDoc.DocumentElement;

        if (templatesRoot is null || propertiesRoot is null)
        {
            Console.WriteLine(ConsoleMessages.Get("errorLoadingDocuments"));
            return 1;
        }

        if (context.DebugMode)
        {
            context.Log.Write("MERGE", string.Format(ConsoleMessages.Get("mergeCheckingTemplateInheritance"), mainOutputFiles.Length.ToString("N0")));
            context.Log.Write("INFO", ConsoleMessages.Get("mergeOnlyFixlistTemplates"));
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(string.Format(ConsoleMessages.Get("inheritingTemplateProperties"), mainOutputFiles.Length.ToString("N0")));
            Console.WriteLine(ConsoleMessages.Get("templateFixlistNote"));
            Console.WriteLine(ConsoleMessages.Get("mergeTemplates"));
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
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
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
                    context.ProgressReporter.OutputFixer(mergeProgress, currentFileIndex.ToString(), mainOutputFiles.Length.ToString());
                }

                try
                {
                    if (!TryMergeAssetFile(context, assetFilePath, fixlistSet, defaultPropertiesIndex, gameType, propertyCache, templateMergeLock, ref actuallyMergedCount))
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    context.Issues.ReportMergeAssetFailed(assetFilePath, ex.Message);
                    context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("mergeAssetFailed"), assetFilePath, ex.Message));
                }
            });

        context.ProgressReporter.OutputFixer(
            ConsoleMessages.Get("mergingProgress"),
            mainOutputFiles.Length.ToString(),
            mainOutputFiles.Length.ToString());

        if (context.DebugMode)
        {
            context.DebugStats.WriteTemplateMergeSummary(context.Log);
            context.Log.Write("COMPLETE", string.Format(ConsoleMessages.Get("templateInheritanceComplete"), actuallyMergedCount, mainOutputFiles.Length));
        }
        else
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("mergedTemplatesCount"), actuallyMergedCount.ToString("N0")));
        }

        return 0;
    }

    private static Dictionary<string, XmlNode> BuildDefaultPropertiesIndex(XmlNode propertiesRoot)
    {
        var index = new Dictionary<string, XmlNode>(StringComparer.OrdinalIgnoreCase);
        XmlNodeList? defaultValuesNodes = propertiesRoot.SelectNodes("//DefaultValues/*");
        if (defaultValuesNodes is not null)
        {
            foreach (XmlNode node in defaultValuesNodes)
                index[node.Name] = node;
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
                    context.Log.Debug(string.Format(ConsoleMessages.Get("debugTemplateSkippedNotInFixlist"), templateName));
            }

            return false;
        }

        XmlDocument assetDoc = new();
        assetDoc.Load(assetFilePath);

        XmlNode? documentElement = assetDoc.DocumentElement;
        if (documentElement == null)
            return false;

        XmlNode? guidNode = documentElement.SelectSingleNode("//Asset/Values/Standard/GUID");
        if (guidNode == null)
            return false;

        string assetGuid = guidNode.InnerText;
        if (context.DebugMode)
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugMergeProcessingFile"), Path.GetFileName(assetFilePath), templateName));

        context.DebugStats.RecordTemplateMergeApplied();

        XmlDocument mergedDoc = new();
        string fileName = Path.GetFileName(assetFilePath);
        string outputPath = Path.Combine(context.AssetOut, fileName);

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

        AppendCachedTemplateProperties(context, mergedDoc, mergedValues, templateName, gameType, propertyCache);
        ApplyDefaultProperties(context, mergedDoc, mergedValues, defaultPropertiesIndex);

        TemplateMergeService.FixFile(context, mergedDoc, assetDoc, outputPath);

        lock (templateMergeLock)
        {
            actuallyMergedCount++;
        }

        if (context.DebugMode && actuallyMergedCount % 50 == 0)
            context.Log.Debug(string.Format(ConsoleMessages.Get("fullInheritanceAppliedTo"), templateName));

        return true;
    }

    private static string ReadTemplateNameFromFile(string assetFilePath)
    {
        try
        {
            var doc = XDocument.Load(assetFilePath);
            var templateElement = doc.Descendants("Template").FirstOrDefault();
            if (templateElement != null) return templateElement.Value;
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
        TemplatePropertyCache propertyCache)
    {
        if (!propertyCache.TryGetRawProperties(templateName, out XElement? cachedProps) || cachedProps is null)
        {
            if (context.DebugMode)
                context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("templateNotFoundInCache"), templateName));

            return;
        }

        if (GameTypeDetector.IsAnno117(gameType))
        {
            if (propertyCache.TryGetFilteredAnno117Properties(templateName, out IReadOnlyList<XNode>? filtered)
                && filtered is not null)
            {
                foreach (XNode node in filtered)
                {
                    if (node is XElement element)
                    {
                        XmlNode imported = mergedDoc.ImportNode(element.ToXmlNode(), true);
                        valuesNode.AppendChild(imported);
                    }
                }
            }

            return;
        }

        foreach (XNode node in cachedProps.Nodes())
        {
            if (node is XElement element)
            {
                XmlNode imported = mergedDoc.ImportNode(element.ToXmlNode(), true);
                valuesNode.AppendChild(imported);
            }
        }
    }

    private static void ApplyDefaultProperties(PipelineContext context, XmlDocument mergedDoc, XmlNode valuesNode, Dictionary<string, XmlNode> defaultPropertiesIndex)
    {
        if (context.AssetNoDefaultProperties || !valuesNode.HasChildNodes)
        {
            return;
        }

        foreach (XmlNode propertyGroup in valuesNode.ChildNodes)
        {
            if (!defaultPropertiesIndex.TryGetValue(propertyGroup.Name, out XmlNode? defaultValues))
                continue;

            var existingChildNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode child in propertyGroup.ChildNodes)
                existingChildNames.Add(child.Name);

            List<XmlNode> nodesToAdd = [];
            foreach (XmlNode defaultProperty in defaultValues.ChildNodes)
            {
                if (!existingChildNames.Contains(defaultProperty.Name))
                {
                    XmlNode imported = mergedDoc.ImportNode(defaultProperty, true);
                    nodesToAdd.Add(imported);
                }
            }

            foreach (XmlNode nodeToAdd in nodesToAdd)
                propertyGroup.AppendChild(nodeToAdd);
        }
    }
}
