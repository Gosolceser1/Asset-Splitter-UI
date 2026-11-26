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
    private sealed record GuidIndexEntry(string AssetName, string TemplateName, string TextKey);

    private sealed record GuidIndexCache(string AssetsFilePath, DateTime LastWriteUtc, Dictionary<string, GuidIndexEntry> Entries);

    private static readonly Lock CacheLock = new();
    private static GuidIndexCache? _indexCache;

    public static void InvalidateCache()
    {
        lock (CacheLock)
        {
            _indexCache = null;
        }
    }

    public static SingleGuidLookupResult Find(string? outputPath, string? gamePath, string? selectedGameType, string? language, string guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
        {
            return new SingleGuidLookupResult(SingleGuidLookupStatus.Empty);
        }

        if (!guid.All(char.IsAsciiDigit))
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
            Dictionary<string, GuidIndexEntry> index = GetOrBuildIndex(assetsFile);
            if (!index.TryGetValue(guid, out GuidIndexEntry? entry))
            {
                return new SingleGuidLookupResult(SingleGuidLookupStatus.NotFound);
            }

            string assetName = entry.AssetName;
            string translatedName = ResolveTranslatedName(assetsFile, language, entry.TextKey, guid);
            if (!string.IsNullOrWhiteSpace(translatedName))
            {
                assetName = translatedName;
            }

            return new SingleGuidLookupResult(SingleGuidLookupStatus.Found, assetName, entry.TemplateName);
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            UILogger.Warning(nameof(SingleGuidAssetLookup), "Failed to scan assets.xml for single GUID");
            UILogger.Debug(nameof(SingleGuidAssetLookup), ex);
            return new SingleGuidLookupResult(SingleGuidLookupStatus.Error);
        }
    }

    private static Dictionary<string, GuidIndexEntry> GetOrBuildIndex(string assetsFile)
    {
        DateTime lastWriteUtc = File.GetLastWriteTimeUtc(assetsFile);

        lock (CacheLock)
        {
            if (_indexCache is not null
                && _indexCache.AssetsFilePath.Equals(assetsFile, StringComparison.OrdinalIgnoreCase)
                && _indexCache.LastWriteUtc == lastWriteUtc)
            {
                return _indexCache.Entries;
            }
        }

        var entries = BuildGuidIndex(assetsFile);

        lock (CacheLock)
        {
            _indexCache = new GuidIndexCache(assetsFile, lastWriteUtc, entries);
            return entries;
        }
    }

    private static Dictionary<string, GuidIndexEntry> BuildGuidIndex(string assetsFile)
    {
        var entries = new Dictionary<string, GuidIndexEntry>(StringComparer.OrdinalIgnoreCase);

        using var reader = XmlReader.Create(assetsFile, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        });

        while (reader.ReadToFollowing("Asset"))
        {
            using XmlReader assetReader = reader.ReadSubtree();
            if (TryReadAsset(assetReader, out string guid, out string assetName, out string templateName, out string textKey))
            {
                if (string.IsNullOrEmpty(assetName))
                {
                    assetName = guid;
                }

                entries[guid] = new GuidIndexEntry(assetName, templateName, textKey);
            }

            reader.Skip();
        }

        return entries;
    }

    private static bool TryReadAsset(XmlReader reader, out string guid, out string assetName, out string templateName, out string textKey)
    {
        bool inStandard = false;
        guid = "";
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
                    guid = reader.ReadElementContentAsString().Trim();
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Name.Equals("Standard", StringComparison.OrdinalIgnoreCase))
            {
                inStandard = false;
            }
        }

        return guid.Length > 0;
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
