using System.Xml;

namespace AssetProcessor;

internal static class AssetXmlPathEditor
{
    public static void EnsurePathExistsOptimized(
        XmlDocument targetDocument,
        XmlNode targetRoot,
        string xpath,
        string outputXmlPath,
        Dictionary<string, XmlNode?> parentNodeCache,
        bool debugMode,
        Action<string, string> writeMessage)
    {
        int lastSlash = xpath.LastIndexOf('/');
        if (lastSlash <= 0)
            return;

        string parentPath = xpath[..lastSlash];
        if (parentNodeCache.TryGetValue(parentPath, out XmlNode? cachedParent) && cachedParent != null)
            return;

        string[] segments = xpath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        XmlNode current = targetRoot;
        int maxIndex = Math.Max(0, segments.Length - 1);

        for (int i = 0; i < maxIndex; i++)
        {
            string segment = segments[i];
            if (string.IsNullOrEmpty(segment))
                continue;

            XmlNode? next = SelectPathSegment(current, segment, outputXmlPath, debugMode, writeMessage);
            if (next is null)
            {
                next = targetDocument.CreateElement(GetElementName(segment));
                current.AppendChild(next);
            }

            current = next ?? current;
        }

        parentNodeCache[parentPath] = current;
    }

    public static void UpsertNode(
        XmlDocument targetDocument,
        XmlDocument sourceDocument,
        XmlNode targetRoot,
        XmlNode sourceNode,
        string xpath,
        bool addInheritanceComments)
    {
        string[] segments = xpath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return;

        string parentPath = segments.Length == 1 ? "" : string.Join("/", segments, 0, segments.Length - 1);
        if (string.IsNullOrEmpty(parentPath))
            return;

        XmlNode? parentNode = targetRoot.SelectSingleNode(parentPath);
        if (parentNode == null)
            return;

        XmlNode newElement = targetDocument.CreateElement(sourceNode.Name);
        newElement.InnerText = sourceNode.InnerText;

        XmlNode? existing = parentNode.SelectSingleNode(sourceNode.Name);
        if (existing != null)
        {
            parentNode.ReplaceChild(newElement, existing);
            return;
        }

        parentNode.AppendChild(newElement);
        if (addInheritanceComments)
            TryAddInheritanceComment(targetDocument, sourceDocument, newElement);
    }

    private static XmlNode? SelectPathSegment(
        XmlNode current,
        string segment,
        string outputXmlPath,
        bool debugMode,
        Action<string, string> writeMessage)
    {
        try
        {
            return current.SelectSingleNode(segment);
        }
        catch (Exception ex)
        {
            if (debugMode)
                writeMessage($"[ERROR] XPath query failed for segment '{segment}' in file {outputXmlPath}: {ex.Message}", "ERROR");
            return null;
        }
    }

    private static XmlNode? SelectCreatedElement(XmlNode current, string elementName)
    {
        try
        {
            return current.SelectSingleNode(elementName + "[last()]");
        }
        catch
        {
            return null;
        }
    }

    private static string GetElementName(string segment)
    {
        int bracketPosition = segment.IndexOf('[', StringComparison.Ordinal);
        return bracketPosition > 0 ? segment[..bracketPosition] : segment;
    }

    private static void TryAddInheritanceComment(XmlDocument targetDocument, XmlDocument sourceDocument, XmlNode newElement)
    {
        try
        {
            string baseGuid = targetDocument.DocumentElement != null ? XmlNodeText.GetValue(targetDocument.DocumentElement, "//Standard/GUID") : "";
            string targetGuid = sourceDocument.DocumentElement != null ? XmlNodeText.GetValue(sourceDocument.DocumentElement, "//Standard/GUID") : "";

            if (!string.IsNullOrEmpty(baseGuid) && baseGuid != targetGuid && newElement.ParentNode != null)
            {
                XmlComment comment = targetDocument.CreateComment($" Inherited from BaseAsset GUID {baseGuid} ");
                newElement.ParentNode.InsertAfter(comment, newElement);
            }
        }
        catch
        {
            // Preserve legacy behavior: comment failures must not break extraction.
        }
    }
}
