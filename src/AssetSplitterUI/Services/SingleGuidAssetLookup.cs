using System.Xml;

namespace AssetSplitterUI.Services;

public enum SingleGuidLookupStatus
{
    Empty,
    Invalid,
    SourceXmlMissing,
    Found,
    NotFound,
    Error
}

public sealed record SingleGuidLookupResult(SingleGuidLookupStatus Status, string? AssetName = null, string? TemplateName = null);

public static class SingleGuidAssetLookup
{
    public static SingleGuidLookupResult Find(string? outputPath, string? gamePath, string? selectedGameType, string? language, string guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
        {
            return new SingleGuidLookupResult(SingleGuidLookupStatus.Empty);
        }

        if (!guid.All(char.IsDigit))
        {
            return new SingleGuidLookupResult(SingleGuidLookupStatus.Invalid);
        }

        string? assetsFile = ExtractedAssetSourceLocator.FindAssetsXml(outputPath, gamePath, selectedGameType);
        if (string.IsNullOrEmpty(assetsFile))
        {
            return new SingleGuidLookupResult(SingleGuidLookupStatus.SourceXmlMissing);
        }

        try
        {
            return ScanAssetsXml(assetsFile, guid, language);
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException or System.IO.DirectoryNotFoundException)
        {
            UILogger.Warning(nameof(SingleGuidAssetLookup), "Failed to scan assets.xml for single GUID");
            UILogger.Debug(nameof(SingleGuidAssetLookup), ex);
            return new SingleGuidLookupResult(SingleGuidLookupStatus.Error);
        }
    }

    private static SingleGuidLookupResult ScanAssetsXml(string assetsFile, string guid, string? language)
    {
        using var reader = XmlReader.Create(assetsFile, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        });

        while (reader.ReadToFollowing("Asset"))
        {
            using XmlReader assetReader = reader.ReadSubtree();
            if (TryReadAsset(assetReader, guid, out string assetName, out string templateName, out string textKey))
            {
                string translatedName = ResolveTranslatedName(assetsFile, language, textKey, guid);
                if (!string.IsNullOrWhiteSpace(translatedName))
                {
                    assetName = translatedName;
                }

                return new SingleGuidLookupResult(SingleGuidLookupStatus.Found, assetName, templateName);
            }

            reader.Skip();
        }

        return new SingleGuidLookupResult(SingleGuidLookupStatus.NotFound);
    }

    private static bool TryReadAsset(XmlReader reader, string guid, out string assetName, out string templateName, out string textKey)
    {
        bool inStandard = false;
        bool guidMatched = false;
        assetName = "";
        templateName = "";
        textKey = "";

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.Name.Equals("Template", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(templateName))
                {
                    templateName = reader.ReadElementContentAsString().Trim();
                }
                else if (reader.Name.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                {
                    inStandard = true;
                }
                else if (inStandard && reader.Name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    assetName = reader.ReadElementContentAsString().Trim();
                }
                else if (reader.Name.Equals("OasisId", StringComparison.OrdinalIgnoreCase)
                    || reader.Name.Equals("LineID", StringComparison.OrdinalIgnoreCase)
                    || reader.Name.Equals("LineId", StringComparison.OrdinalIgnoreCase))
                {
                    textKey = reader.ReadElementContentAsString().Trim();
                }
                else if (inStandard && reader.Name.Equals("GUID", StringComparison.OrdinalIgnoreCase))
                {
                    string currentGuid = reader.ReadElementContentAsString().Trim();
                    if (currentGuid.Equals(guid, StringComparison.OrdinalIgnoreCase))
                    {
                        guidMatched = true;
                    }
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Name.Equals("Standard", StringComparison.OrdinalIgnoreCase))
            {
                inStandard = false;
            }
        }

        if (guidMatched && string.IsNullOrEmpty(assetName))
        {
            assetName = guid;
        }

        return guidMatched;
    }

    private static string ResolveTranslatedName(string assetsFile, string? language, string textKey, string guid)
    {
        if (string.IsNullOrWhiteSpace(textKey) && string.IsNullOrWhiteSpace(guid))
        {
            return "";
        }

        string? sourceFolder = Path.GetDirectoryName(assetsFile);
        if (string.IsNullOrEmpty(sourceFolder))
        {
            return "";
        }

        string languageFile = Path.Combine(sourceFolder, GetLanguageFileName(language));
        if (!File.Exists(languageFile))
        {
            languageFile = Directory.EnumerateFiles(sourceFolder, "texts_*.xml")
                .FirstOrDefault(ExtractedAssetSourceLocator.IsGameLanguageFile) ?? "";
        }

        if (string.IsNullOrEmpty(languageFile))
        {
            return "";
        }

        using var reader = XmlReader.Create(languageFile, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        });

        string directGuidValue = "";
        string textKeyValue = "";

        while (reader.ReadToFollowing("Text"))
        {
            using XmlReader textReader = reader.ReadSubtree();
            string key = "";
            string value = "";
            bool skippedRootTextElement = false;
            while (textReader.Read())
            {
                if (textReader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (textReader.Name.Equals("LineId", StringComparison.OrdinalIgnoreCase)
                    || textReader.Name.Equals("GUID", StringComparison.OrdinalIgnoreCase))
                {
                    key = textReader.ReadElementContentAsString().Trim();
                }
                else if (textReader.Name.Equals("Text", StringComparison.OrdinalIgnoreCase))
                {
                    if (!skippedRootTextElement)
                    {
                        skippedRootTextElement = true;
                        continue;
                    }

                    value = textReader.ReadElementContentAsString().Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(guid) && key.Equals(guid, StringComparison.OrdinalIgnoreCase))
            {
                directGuidValue = value;
                break;
            }

            if (!string.IsNullOrWhiteSpace(textKey) && key.Equals(textKey, StringComparison.OrdinalIgnoreCase))
            {
                textKeyValue = value;
            }

            reader.Skip();
        }

        return !string.IsNullOrWhiteSpace(directGuidValue) ? directGuidValue : textKeyValue;
    }

    private static string GetLanguageFileName(string? language)
    {
        string normalized = string.IsNullOrWhiteSpace(language) ? "english" : language.Trim().ToLowerInvariant();
        if (normalized.StartsWith("texts_", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["texts_".Length..];
        }

        if (normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^".xml".Length];
        }

        normalized = normalized
            .Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal)
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);

        normalized = normalized switch
        {
            "brazilian_portuguese" => "brazilian",
            "mexican_spanish" => "mexican",
            "chinese_simplified" => "chinese",
            "traditional_chinese" => "tchinese",
            "traditionalchinese" => "tchinese",
            _ => normalized
        };

        return "texts_" + normalized + ".xml";
    }
}
