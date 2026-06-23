using System.Xml;

namespace AssetProcessor;

internal static class XmlLeafPathBuilder
{
    public static List<string> Build(XmlNode node)
    {
        List<string> results = new(1024);
        Collect(node, string.Empty, results);
        return results;
    }

    private static void Collect(XmlNode node, string currentPathPrefix, List<string> leafPaths)
    {
        string currentPath = currentPathPrefix + GetPathSegment(node);
        bool hasElementChildren = false;
        bool hasText = false;

        foreach (XmlNode childNode in node.ChildNodes)
        {
            if (childNode.NodeType == XmlNodeType.Element)
            {
                hasElementChildren = true;
                Collect(childNode, currentPath + "/", leafPaths);
            }
            else if (childNode.NodeType == XmlNodeType.Text || childNode.NodeType == XmlNodeType.CDATA)
            {
                hasText = true;
            }
        }

        if (!hasElementChildren && hasText)
        {
            leafPaths.Add(currentPath);
        }
    }

    private static string GetPathSegment(XmlNode node)
    {
        int sameNameIndex = 1;
        for (XmlNode? sibling = node.PreviousSibling; sibling is not null; sibling = sibling.PreviousSibling)
        {
            if (sibling.NodeType == XmlNodeType.Element && sibling.Name.Equals(node.Name, StringComparison.Ordinal))
            {
                sameNameIndex++;
            }
        }

        return sameNameIndex > 1 ? $"{node.Name}[{sameNameIndex}]" : node.Name;
    }
}
