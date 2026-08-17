using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace AssetProcessor;

public static partial class AssetModPackageExporter
{
    private static void WriteModLoaderAssetFile(PipelineContext context, string sourceFile, string destinationPath, string guid)
    {
        XmlDocument modDocument = CreateModLoaderAssetDocument(sourceFile, guid);
        OutputDirectoryManager.EnsureDirectoryExists(Path.GetDirectoryName(destinationPath));

        if (!File.Exists(destinationPath))
        {
            GeneratedXmlFootprint.Save(modDocument, destinationPath, context);
            return;
        }

        var existingDocument = new XmlDocument { PreserveWhitespace = true };
        XmlDocumentLoadRetry.Load(existingDocument, destinationPath);
        XmlNode? existingRoot = existingDocument.SelectSingleNode("/ModOps") ?? throw new InvalidOperationException("Existing mod assets.xml is missing ModOps root: " + destinationPath);
        XmlNodeList? modOps = modDocument.SelectNodes("/ModOps/ModOp");
        if (modOps is null || modOps.Count == 0)
        {
            throw new InvalidOperationException("Source asset did not produce ModOp entries: " + sourceFile);
        }

        foreach (XmlNode modOp in modOps)
        {
            existingRoot.AppendChild(existingDocument.ImportNode(modOp, true));
        }

        GeneratedXmlFootprint.Save(existingDocument, destinationPath, context);
    }

    private static XmlDocument CreateModLoaderAssetDocument(string sourceFile, string guid)
    {
        var xml = new XmlDocument { PreserveWhitespace = true };
        XmlDocumentLoadRetry.Load(xml, sourceFile);

        if (xml.SelectSingleNode("/ModOps/ModOp") is not null)
        {
            return xml;
        }

        XmlNode? assetNode = xml.SelectSingleNode("//Asset")
            ?? throw new InvalidOperationException("Asset node missing in " + sourceFile);

        if (string.IsNullOrWhiteSpace(guid))
        {
            guid = assetNode.SelectSingleNode(".//Values/Standard/GUID")?.InnerText.Trim() ?? "";
        }

        return CreateModOpsDocument(assetNode, guid);
    }

    private static XmlDocument CreateModOpsDocument(XmlNode assetNode, string guid)
    {
        XmlDocument document = new();
        XmlElement modOpsElement = document.CreateElement("ModOps");
        document.AppendChild(modOpsElement);

        XmlElement modOpElement = document.CreateElement("ModOp");
        modOpElement.SetAttribute("GUID", guid);
        modOpElement.SetAttribute("Type", "Replace");
        modOpElement.SetAttribute("Path", "/");
        modOpsElement.AppendChild(modOpElement);
        modOpElement.AppendChild(document.ImportNode(assetNode, true));
        return document;
    }
}
