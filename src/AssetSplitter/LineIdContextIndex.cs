using System.Xml;

namespace AssetProcessor;

/// <summary>
/// Resolves LineIds missing from language files using asset graph context (participants, portraits, singleton dev names).
/// </summary>
public sealed class LineIdContextIndex
{
    private readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal);

    private static readonly HashSet<string> SingletonNameFallbackProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "HeaderText", "BodyText", "AddSummaryText", "OverrideBodyText", "InfoPanelDescription",
        "PortraitName", "HintText", "Subtitle", "AudioText", "Text", "ObjectiveText",
        "ExecutionPlaceFullText", "MonumentHeadline", "InfoPanelHeadline",
    };

    public static LineIdContextIndex? TryBuild(PipelineContext context)
    {
        if (!GameTypeDetector.IsAnno117(context.DetectedGameType))
        {
            return null;
        }

        string assetsPath = AssetProcessorFileSystem.CombineSourceXml(context, "assets.xml");
        if (!File.Exists(assetsPath))
        {
            return null;
        }

        if (context.DebugMode)
        {
            context.Log.Write(ConsoleMessageKind.Trans, ConsoleMessages.Get("lineIdContextBuilding"));
        }

        XmlDocument doc = new();
        try
        {
            XmlDocumentLoadRetry.Load(doc, assetsPath);
        }
        catch (Exception ex)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Warning, "lineIdContextLoadFailed", always: true, ex.Message);
            return null;
        }

        var index = new LineIdContextIndex();
        var lineIdCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var assets = new List<AssetRow>();

        XmlNodeList? assetNodes = doc.DocumentElement?.SelectNodes("//Asset");
        if (assetNodes is null)
        {
            return index;
        }

        foreach (XmlNode assetNode in assetNodes)
        {
            XmlNode? values = assetNode.SelectSingleNode("Values");
            if (values is null)
            {
                continue;
            }

            string guid = XmlNodeText.GetValue(values, "Standard/GUID");
            if (string.IsNullOrEmpty(guid))
            {
                continue;
            }

            string standardName = XmlNodeText.GetValue(values, "Standard/Name");
            string oasisId = XmlNodeText.GetValue(values, "Text/OasisId");
            string portraitGuid = XmlNodeText.GetValue(values, "Participant/Portrait");
            string template = XmlNodeText.GetValue(assetNode, "Template");
            string displayName = AssetNameRegistry.ResolveAssetName(context, oasisId, standardName, template);

            assets.Add(new AssetRow(guid, standardName, oasisId, portraitGuid, displayName, values));

            foreach (XmlNode elem in EnumerateLeafElements(values))
            {
                string value = (elem.InnerText ?? "").Trim();
                if (!LineIdValue.IsNegativeLineId(value))
                {
                    continue;
                }

                lineIdCounts.TryGetValue(value, out int count);
                lineIdCounts[value] = count + 1;
            }
        }

        foreach (AssetRow row in assets)
        {
            if (!string.IsNullOrEmpty(row.OasisId)
                && LineIdValue.IsNegativeLineId(row.OasisId)
                && !HasDictionaryLabel(context, row.OasisId)
                && !string.IsNullOrEmpty(row.DisplayName))
            {
                index.TryAddLabel(row.OasisId, row.DisplayName);
            }

            if (!string.IsNullOrEmpty(row.PortraitGuid)
                && !string.IsNullOrEmpty(row.DisplayName))
            {
                index.TryAddLabel("guid:" + row.PortraitGuid, row.DisplayName);
            }
        }

        foreach (AssetRow row in assets)
        {
            foreach (XmlNode elem in EnumerateLeafElements(row.Values))
            {
                string value = (elem.InnerText ?? "").Trim();
                if (!LineIdValue.IsNegativeLineId(value))
                {
                    continue;
                }

                if (HasDictionaryLabel(context, value) || index._labels.ContainsKey(value))
                {
                    continue;
                }

                if (!SingletonNameFallbackProperties.Contains(elem.Name))
                {
                    continue;
                }

                if (lineIdCounts.TryGetValue(value, out int count) && count == 1
                    && !string.IsNullOrEmpty(row.StandardName))
                {
                    index.TryAddLabel(value, row.StandardName);
                }
            }
        }

        if (context.DebugMode)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Complete, "lineIdContextComplete", always: true, index._labels.Count.ToString("N0"));
        }

        return index;
    }

    public bool TryGetLabel(string key, out string label)
    {
        if (_labels.TryGetValue(key, out string? found))
        {
            label = found;
            return true;
        }

        label = string.Empty;
        return false;
    }

    public bool TryGetPortraitLabel(string portraitAssetGuid, out string label) =>
        TryGetLabel("guid:" + portraitAssetGuid, out label);

    private void TryAddLabel(string key, string label)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Length < 2)
        {
            return;
        }

        _labels.TryAdd(key, label.Trim());
    }

    private static bool HasDictionaryLabel(PipelineContext context, string lineId) =>
        context.Translator.ContainsKey(lineId) || context.MetadataNames.ContainsKey(lineId);

    private readonly record struct AssetRow(
        string Guid,
        string StandardName,
        string OasisId,
        string PortraitGuid,
        string DisplayName,
        XmlNode Values);

    private static IEnumerable<XmlNode> EnumerateLeafElements(XmlNode root)
    {
        XmlNodeList? nodes = root.SelectNodes(".//*");
        if (nodes is null)
        {
            yield break;
        }

        foreach (XmlNode node in nodes)
        {
            if (node.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (node.InnerXml.Contains('<', StringComparison.Ordinal))
            {
                continue;
            }

            yield return node;
        }
    }
}
