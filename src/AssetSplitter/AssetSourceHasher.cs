using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace AssetProcessor;

/// <summary>
/// Stable content hashes for source <c>&lt;Asset&gt;</c> nodes in <c>assets.xml</c>,
/// used to diff game updates without comparing enriched output files.
/// </summary>
internal static class AssetSourceHasher
{
    public static string HashAssetNode(XmlNode assetNode)
    {
        ArgumentNullException.ThrowIfNull(assetNode);

        // Clone into a whitespace-stripped document so insignificant formatting
        // differences in the game dump do not flip hashes.
        var normalized = new XmlDocument { PreserveWhitespace = false };
        XmlNode imported = normalized.ImportNode(assetNode, deep: true);
        _ = normalized.AppendChild(imported);

        byte[] bytes = Encoding.UTF8.GetBytes(normalized.OuterXml);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Builds guid → sourceHash for assets that the extractor would process
    /// (has BaseAssetGUID or template is in the used-template set).
    /// </summary>
    public static Dictionary<string, string> BuildExtractableHashMap(
        XmlDocument assetsXml,
        string gameType,
        IReadOnlyCollection<string>? templatesUsed = null)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        XmlNode? documentElement = assetsXml.DocumentElement;
        if (documentElement is null)
        {
            return map;
        }

        string[] templates = templatesUsed is { Count: > 0 }
            ? [.. templatesUsed]
            : TemplateLoader.LoadTemplates(gameType);
        var templateSet = new HashSet<string>(templates, StringComparer.OrdinalIgnoreCase);

        foreach (XmlNode node in EnumerateAssetNodes(documentElement))
        {
            if (!IsExtractable(node, templateSet))
            {
                continue;
            }

            string guid = XmlNodeText.GetValue(node, "Values/Standard/GUID");
            if (guid.Length == 0)
            {
                continue;
            }

            map[guid] = HashAssetNode(node);
        }

        return map;
    }

    /// <summary>parent GUID → child GUIDs that declare BaseAssetGUID = parent.</summary>
    public static Dictionary<string, List<string>> BuildParentToChildrenMap(XmlDocument assetsXml)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        XmlNode? documentElement = assetsXml.DocumentElement;
        if (documentElement is null)
        {
            return map;
        }

        foreach (XmlNode node in EnumerateAssetNodes(documentElement))
        {
            string guid = XmlNodeText.GetValue(node, "Values/Standard/GUID");
            string parent = XmlNodeText.GetValue(node, "BaseAssetGUID");
            if (guid.Length == 0 || parent.Length == 0)
            {
                continue;
            }

            if (!map.TryGetValue(parent, out List<string>? children))
            {
                children = [];
                map[parent] = children;
            }

            children.Add(guid);
        }

        return map;
    }

    public static HashSet<string> ExpandDescendantClosure(
        IEnumerable<string> seeds,
        IReadOnlyDictionary<string, List<string>> parentToChildren)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        foreach (string seed in seeds)
        {
            if (seed.Length == 0 || !result.Add(seed))
            {
                continue;
            }

            queue.Enqueue(seed);
        }

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            if (!parentToChildren.TryGetValue(current, out List<string>? children))
            {
                continue;
            }

            foreach (string child in children)
            {
                if (result.Add(child))
                {
                    queue.Enqueue(child);
                }
            }
        }

        return result;
    }

    public static bool IsExtractable(XmlNode assetNode, HashSet<string> templateSet)
    {
        string templateName = XmlNodeText.GetValue(assetNode, "Template");
        string baseGuid = XmlNodeText.GetValue(assetNode, "BaseAssetGUID");
        return !string.IsNullOrEmpty(baseGuid) || templateSet.Contains(templateName);
    }

    private static IEnumerable<XmlNode> EnumerateAssetNodes(XmlNode documentElement)
    {
        XmlNodeList? nested = documentElement.SelectNodes("//Assets/Asset");
        if (nested is not null)
        {
            foreach (XmlNode node in nested)
            {
                yield return node;
            }
        }

        if (documentElement.Name == "Asset")
        {
            yield return documentElement;
            yield break;
        }

        XmlNodeList? direct = documentElement.SelectNodes("./Asset");
        if (direct is null)
        {
            yield break;
        }

        foreach (XmlNode node in direct)
        {
            yield return node;
        }
    }
}
