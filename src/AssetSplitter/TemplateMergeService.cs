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
        EnsureModOpsWrapper(xmlBaseFile);
        EnsureModOpsWrapper(xmlFixFile);

        XmlNode? baseRoot = xmlBaseFile.DocumentElement;
        XmlNode? fixRoot = xmlFixFile.DocumentElement;
        if (baseRoot is null || fixRoot is null)
        {
            return;
        }

        XmlNode? assetNode = fixRoot.SelectSingleNode("//Asset");
        XmlNode? baseModOp = baseRoot.SelectSingleNode("//ModOps/ModOp") ?? baseRoot.SelectSingleNode("ModOp");
        XmlNode? fixModOp = fixRoot.SelectSingleNode("//ModOps/ModOp") ?? fixRoot.SelectSingleNode("ModOp");
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
                context.AddComments);

            mergedNodes++;
        }

        if (logXpathDetail)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugMergeCompleted"), mergedNodes, Path.GetFileName(outputXmlPath)));
        }

        AssetXmlStructureNormalizer.SetModOpGuidAttribute(xmlBaseFile, baseRoot);
        AssetDocumentSaver.SaveMergedAsset(context, xmlBaseFile, baseModOp, outputXmlPath, !context.ModOpsWrap);
    }

    /// <summary>
    /// <see cref="FixFile"/> selects leaf paths relative to <c>ModOp</c>. Extracted files with
    /// <c>--no-modops-wrap</c> are a bare <c>Asset</c> root and must be wrapped before merge.
    /// </summary>
    private static void EnsureModOpsWrapper(XmlDocument document)
    {
        XmlElement? root = document.DocumentElement;
        if (root is null)
        {
            return;
        }

        if (root.SelectSingleNode("//ModOps/ModOp") is not null || root.SelectSingleNode("/ModOps/ModOp") is not null)
        {
            return;
        }

        if (!root.Name.Equals("Asset", StringComparison.Ordinal))
        {
            return;
        }

        XmlElement modOps = document.CreateElement("ModOps");
        XmlElement modOp = document.CreateElement("ModOp");
        document.RemoveChild(root);
        document.AppendChild(modOps);
        modOps.AppendChild(modOp);
        modOp.AppendChild(root);
    }
}
