using System.Xml;

namespace AssetProcessor;

/// <summary>Merges template or parent-asset properties into ModOp XML (Phases 4 and 5).</summary>
public static class TemplateMergeService
{
    /// <summary>
    /// Merges leaf properties from <paramref name="xmlFixFile"/> into <paramref name="xmlBaseFile"/> and saves the result.
    /// </summary>
    public static void FixFile(
        PipelineContext context,
        XmlDocument xmlBaseFile,
        XmlDocument xmlFixFile,
        string outputXmlPath,
        MergeTraceKind traceKind = MergeTraceKind.TemplateInheritance)
    {
        XmlNode? baseRoot = xmlBaseFile.DocumentElement;
        XmlNode? fixRoot = xmlFixFile.DocumentElement;
        if (baseRoot is null || fixRoot is null)
        {
            return;
        }

        XmlNode? assetNode = fixRoot.SelectSingleNode("//Asset");
        XmlNode? baseModOp = baseRoot.SelectSingleNode("//ModOps/ModOp");
        XmlNode? fixModOp = fixRoot.SelectSingleNode("//ModOps/ModOp");
        if (assetNode == null || baseModOp == null || fixModOp == null)
        {
            return;
        }

        bool logXpathDetail = context.ShouldLogMergeXpathDetail(traceKind);

        List<string> xpaths = XmlLeafPathBuilder.Build(assetNode);
        if (logXpathDetail)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugMergeBuiltXpaths"), xpaths.Count, outputXmlPath));
        }

        var parentNodeCache = new Dictionary<string, XmlNode?>(StringComparer.OrdinalIgnoreCase);

        var parentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string xpath in xpaths)
        {
            if (xpath.Contains("BaseAssetGUID"))
            {
                continue;
            }

            int lastSlash = xpath.LastIndexOf('/');
            if (lastSlash > 0)
            {
                parentPaths.Add(xpath[..lastSlash]);
            }
        }

        if (logXpathDetail)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugMergeIdentifiedParentPaths"), parentPaths.Count));
        }

        foreach (string parentPath in parentPaths)
        {
            try
            {
                XmlNode? parentNode = baseModOp.SelectSingleNode(parentPath);
                parentNodeCache[parentPath] = parentNode;
            }
            catch (Exception ex)
            {
                if (context.DebugMode)
                {
                    context.Log.Debug(string.Format(ConsoleMessages.Get("debugMergeFailedSelectPath"), parentPath, ex.Message));
                }
            }
        }

        int mergedNodes = 0;
        foreach (string xpath in xpaths)
        {
            if (xpath.Contains("BaseAssetGUID"))
            {
                continue;
            }

            XmlNode? sourceNode = fixModOp.SelectSingleNode(xpath);
            if (sourceNode == null)
            {
                continue;
            }

            AssetXmlPathEditor.EnsurePathExistsOptimized(
                xmlBaseFile,
                baseModOp,
                xpath,
                outputXmlPath,
                parentNodeCache,
                context.DebugMode,
                AssetProcessorConsole.WriteColoredMessage);
            AssetXmlPathEditor.UpsertNode(
                xmlBaseFile,
                xmlFixFile,
                baseModOp,
                sourceNode,
                xpath,
                context.AssetComments);

            mergedNodes++;
        }

        if (logXpathDetail)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugMergeCompleted"), mergedNodes, Path.GetFileName(outputXmlPath)));
        }

        AssetXmlStructureNormalizer.SetModOpGuidAttribute(xmlBaseFile, baseRoot);
        AssetDocumentSaver.SaveMergedAsset(context, xmlBaseFile, baseModOp, outputXmlPath, !context.AssetModOpsWrap);
    }
}
