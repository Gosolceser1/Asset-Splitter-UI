namespace AssetProcessor;

/// <summary>Expected bare filenames for <c>source_xml_{game}/</c> after Phase 1 (RDA or copy).</summary>
public static class SourceXmlCatalog
{
    /// <summary>
    /// Structural source XML required to treat an extracted <c>source_xml_*</c> folder as complete.
    /// Excludes translation files (<c>texts_*.xml</c>) so UI detection can succeed before language extract.
    /// </summary>
    public static IReadOnlyList<string> GetStructuralRequiredFileNames(string gameType)
    {
        if (GameTypeDetector.IsAnno117(gameType))
        {
            return
            [
                "assets.xml",
                "properties.xml",
                "properties-meta.xml",
                "templates.xml",
                "datasets.xml",
                "audio_generated.xml",
            ];
        }

        return
        [
            "assets.xml",
            "properties.xml",
            "properties-toolone.xml",
            "templates.xml",
            "datasets.xml",
        ];
    }

    public static IReadOnlyList<string> GetExpectedFileNames(string gameType, string assetLanguage)
    {
        var files = new List<string>(GetStructuralRequiredFileNames(gameType))
        {
            "texts_english.xml",
        };

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
