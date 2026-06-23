namespace AssetProcessor;

/// <summary>Expected bare filenames for <c>source_xml_{game}/</c> after Phase 1 (RDA or copy).</summary>
internal static class SourceXmlCatalog
{
    public static IReadOnlyList<string> GetExpectedFileNames(string gameType, string assetLanguage)
    {
        bool anno117 = GameTypeDetector.IsAnno117(gameType);
        var files = new List<string>
        {
            "assets.xml",
            "properties.xml",
            "templates.xml",
            "datasets.xml",
            "texts_english.xml",
        };

        if (anno117)
        {
            files.Add("properties-meta.xml");
            files.Add("audio_generated.xml");
        }
        else
        {
            files.Add("properties-toolone.xml");
        }

        if (!string.IsNullOrWhiteSpace(assetLanguage)
            && !assetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase)
            && !assetLanguage.Equals("texts_english.xml", StringComparison.OrdinalIgnoreCase)
            && !files.Contains(assetLanguage, StringComparer.OrdinalIgnoreCase))
        {
            files.Add(assetLanguage);
        }

        return files;
    }

    public static IReadOnlyList<string> GetOptionalFileNames(string gameType)
    {
        if (!GameTypeDetector.IsAnno117(gameType))
        {
            return [];
        }

        return [TextMetadataDictionaryLoader.MetadataFileName];
    }
}
