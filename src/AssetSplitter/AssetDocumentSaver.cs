using System.Xml;

namespace AssetProcessor;

internal static class AssetDocumentSaver
{
    public static void SaveMergedAsset(PipelineContext context, XmlDocument document, XmlNode modOpNode, string outputPath, bool noModOpsWrap)
    {
        if (!noModOpsWrap)
        {
            GeneratedXmlFootprint.Save(document, outputPath, context);
            return;
        }

        SaveAssetOnlyOrFallback(context, document, modOpNode.SelectSingleNode("Asset"), outputPath);
    }

    public static void SaveExtractedAsset(PipelineContext context, XmlDocument document, string outputPath, bool noModOpsWrap)
    {
        if (!noModOpsWrap)
        {
            GeneratedXmlFootprint.Save(document, outputPath, context);
            return;
        }

        XmlNode? modOpNode =
            document.DocumentElement?.SelectSingleNode("//ModOps/ModOp") ??
            document.DocumentElement?.SelectSingleNode("/ModOps/ModOp");
        SaveAssetOnlyOrFallback(context, document, modOpNode?.SelectSingleNode("Asset"), outputPath);
    }

    private static void SaveAssetOnlyOrFallback(PipelineContext context, XmlDocument sourceDocument, XmlNode? assetNode, string outputPath)
    {
        if (assetNode is null)
        {
            GeneratedXmlFootprint.Save(sourceDocument, outputPath, context);
            return;
        }

        XmlDocument outputDocument = new();
        outputDocument.AppendChild(outputDocument.ImportNode(assetNode, true));
        GeneratedXmlFootprint.Save(outputDocument, outputPath, context);
    }
}
