using System.Xml;

namespace AssetProcessor;

internal static class AssetXmlStructureNormalizer
{
    public static void RemoveVectorElements(XmlNode root)
    {
        XmlNode? modOpNode = root.SelectSingleNode("//ModOps/ModOp");
        if (modOpNode is null)
            return;

        XmlNodeList? vectorElements = modOpNode.SelectNodes("//Item/VectorElement");
        if (vectorElements is null)
            return;

        foreach (XmlNode vectorElement in vectorElements)
            vectorElement.ParentNode?.RemoveChild(vectorElement);
    }

    public static void SetModOpGuidAttribute(XmlDocument document, XmlNode root)
    {
        XmlNode? modOpNode = root.SelectSingleNode("//ModOps/ModOp");
        XmlNode? guidNode = modOpNode?.SelectSingleNode("//Standard/GUID");
        if (modOpNode is null || guidNode is null)
            return;

        XmlAttribute attribute = document.CreateAttribute("GUID");
        attribute.Value = guidNode.InnerText;
        modOpNode.Attributes?.SetNamedItem(attribute);
    }
}
