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
        XmlNode? parent = node.ParentNode;
        XmlNodeList? xmlNodeList = parent?.SelectNodes(node.Name);
        if (xmlNodeList is null)
            return;

        // A single call already enumerates every same-named sibling below, so only the
        // first sibling of each name group should drive collection. Without this guard,
        // every sibling re-enumerates the whole group, duplicating work and output.
        if (parent is not null && !ReferenceEquals(parent.SelectSingleNode(node.Name), node))
            return;

        string currentPath = currentPathPrefix + node.Name;
        int position = 0;

        foreach (XmlNode xmlNode in xmlNodeList)
        {
            position++;
            foreach (XmlNode childNode in xmlNode.ChildNodes)
            {
                if (childNode.Name == "#text")
                {
                    leafPaths.Add(position > 1
                        ? currentPath + "[" + position + "]/"
                        : currentPath);
                }
                else if (position > 1)
                {
                    Collect(childNode, currentPath + "[" + position + "]/", leafPaths);
                }
                else
                {
                    Collect(childNode, currentPath + "/", leafPaths);
                }
            }
        }
    }
}
