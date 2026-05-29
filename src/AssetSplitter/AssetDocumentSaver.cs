using System.Xml;

namespace AssetProcessor;

internal static class AssetDocumentSaver
{
    public static void SaveMergedAsset(XmlDocument document, XmlNode modOpNode, string outputPath, bool noModOpsWrap)
    {
        if (!noModOpsWrap)
        {
            document.Save(outputPath);
            return;
        }

        SaveAssetOnlyOrFallback(document, modOpNode.SelectSingleNode("Asset"), outputPath);
    }

    public static void SaveExtractedAsset(XmlDocument document, string outputPath, bool noModOpsWrap)
    {
        if (!noModOpsWrap)
        {
            document.Save(outputPath);
            return;
        }

        XmlNode? modOpNode =
            document.DocumentElement?.SelectSingleNode("//ModOps/ModOp") ??
            document.DocumentElement?.SelectSingleNode("/ModOps/ModOp");
        SaveAssetOnlyOrFallback(document, modOpNode?.SelectSingleNode("Asset"), outputPath);
    }

    private static void SaveAssetOnlyOrFallback(XmlDocument sourceDocument, XmlNode? assetNode, string outputPath)
    {
        if (assetNode is null)
        {
            sourceDocument.Save(outputPath);
            return;
        }

        XmlDocument outputDocument = new();
        outputDocument.AppendChild(outputDocument.ImportNode(assetNode, true));
        outputDocument.Save(outputPath);
    }
}
