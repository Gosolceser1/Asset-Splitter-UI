using System.Xml;

namespace AssetProcessor;

internal static class XmlNodeText
{
    public static string GetValue(XmlNode node, string xpath)
    {
        return node.SelectSingleNode(xpath)?.InnerText ?? "";
    }
}
