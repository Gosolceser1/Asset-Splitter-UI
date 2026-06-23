namespace AssetProcessor;

using System.Collections.Concurrent;
using System.Xml;

public static class TranslationRegistry
{
    public static string Translate(PipelineContext context, string key)
    {
        if (TryGet(context.Translator, key, out string? value))
        {
            return value;
        }

        if (TryGet(context.MetadataNames, key, out value))
        {
            return value;
        }

        if (context.LineIdContext?.TryGetLabel(key, out value) == true)
        {
            return value;
        }

        if (TryGet(context.AssetNames, key, out value))
        {
            return value;
        }

        return string.Empty;
    }

    public static string TranslateElement(PipelineContext context, XmlNode element, string key)
    {
        string translated = Translate(context, key);
        if (!string.IsNullOrWhiteSpace(translated))
        {
            return translated;
        }

        if (element.Name.Equals("RefGuidGamepad", StringComparison.OrdinalIgnoreCase)
            || element.Name.Equals("RefGuid", StringComparison.OrdinalIgnoreCase))
        {
            return TryResolveAdjacentHintText(context, element) ?? string.Empty;
        }

        if (element.Name.Equals("PortraitName", StringComparison.OrdinalIgnoreCase))
        {
            XmlDocument? doc = element.OwnerDocument;
            if (doc?.DocumentElement is not null)
            {
                string portraitGuid = XmlNodeText.GetValue(doc.DocumentElement, ".//Standard/GUID");
                if (!string.IsNullOrEmpty(portraitGuid)
                    && context.LineIdContext?.TryGetPortraitLabel(portraitGuid, out string? portraitLabel) == true)
                {
                    return portraitLabel;
                }
            }
        }

        return string.Empty;
    }

    private static bool TryGet(ConcurrentDictionary<string, string> dictionary, string key, out string value)
    {
        if (dictionary.TryGetValue(key, out string? found) && found is not null)
        {
            value = found;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static string? TryResolveAdjacentHintText(PipelineContext context, XmlNode element)
    {
        XmlNode? parent = element.ParentNode;
        if (parent is null)
        {
            return null;
        }

        foreach (XmlNode sibling in parent.ChildNodes)
        {
            if (sibling.NodeType != XmlNodeType.Element
                || !sibling.Name.Equals("HintText", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string hintKey = sibling.InnerText.Trim();
            if (string.IsNullOrEmpty(hintKey))
            {
                continue;
            }

            string hintText = Translate(context, hintKey);
            if (!string.IsNullOrWhiteSpace(hintText) && hintText.Length >= 2)
            {
                return hintText;
            }
        }

        return null;
    }
}
