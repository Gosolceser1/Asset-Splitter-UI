namespace AssetProcessor;

/// <summary>Writes per-export <c>MODDING-GUIDE.md</c>, short per-mod README files, and the export summary README.</summary>
internal static partial class ModReadmeWriter
{
    public const string ProjectHomeUrl = "https://github.com/Gosolceser1/Asset-Splitter-UI";
    public const string ProjectReleasesUrl = "https://github.com/Gosolceser1/Asset-Splitter-UI/releases";

    private static IReadOnlyList<string> GetBuildFootprintLines(PipelineContext context, Func<string, string> t, bool includeGeneratedAt)
    {
        var lines = new List<string>();
        string gameBuild = GeneratedXmlFootprint.BuildGameBuildDescription(context);
        if (!string.IsNullOrWhiteSpace(gameBuild))
        {
            lines.Add(string.Format(t("readmeGameBuildLabel"), gameBuild));
        }
        string buildTime = GeneratedXmlFootprint.BuildGameBuildTimeDescription(context);
        if (!string.IsNullOrWhiteSpace(buildTime))
        {
            lines.Add(string.Format(t("readmeGameBuildTimeLabel"), buildTime));
        }
        if (includeGeneratedAt && lines.Count > 0)
        {
            lines.Add(string.Format(t("readmeGeneratedLocalTimeLabel"), GeneratedXmlFootprint.FormatGeneratedLocalTime(DateTimeOffset.Now)));
        }

        lines.Add(string.Format(t("readmeToolAttribution"), ProjectHomeUrl));
        return lines;
    }

    /// <summary>Writes <c>AnnoAssets/README.md</c> so people who only have the output folder know where to get the tool.</summary>
    public static void WriteAnnoAssetsReadme(string annoAssetsRoot)
    {
        if (string.IsNullOrWhiteSpace(annoAssetsRoot))
        {
            return;
        }

        try
        {
            _ = Directory.CreateDirectory(annoAssetsRoot);
            Func<string, string> t = key => ConsoleMessages.Get(key);
            List<string> lines =
            [
                t("annoAssetsReadmeTitle"),
                "",
                t("annoAssetsReadmeIntro"),
                "",
                t("annoAssetsReadmeDownloadHeader"),
                "",
                t("annoAssetsReadmeDownloadIntro"),
                "",
                string.Format(t("annoAssetsReadmeDownloadProject"), ProjectHomeUrl),
                string.Format(t("annoAssetsReadmeDownloadReleases"), ProjectReleasesUrl),
                "",
                t("annoAssetsReadmeLayoutHeader"),
                "",
            ];
            lines.AddRange(t("annoAssetsReadmeLayoutBody").Replace("\r\n", "\n").Split('\n'));
            lines.Add("");
            WriteUtf8Lines(Path.Combine(annoAssetsRoot, "README.md"), lines);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Best effort — do not fail the pipeline if README cannot be written.
        }
    }

    private static void InsertBlockBefore(List<string> lines, string anchor, IReadOnlyList<string> block)
    {
        if (block.Count == 0)
        {
            return;
        }
        int insertAt = lines.IndexOf(anchor);
        if (insertAt < 0)
        {
            insertAt = lines.Count;
        }
        else if (insertAt > 0 && string.IsNullOrEmpty(lines[insertAt - 1]))
        {
            insertAt--;
        }
        var insertLines = new List<string>(block) { "" };
        lines.InsertRange(insertAt, insertLines);
    }

    private static string EscapeXmlText(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static void WriteUtf8Lines(string path, IReadOnlyList<string> lines)
    {
        File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string DisplayValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(none)" : value;

    private static string NormalizeAssetLanguage(string? value)
    {
        string language = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(language))
        {
            return string.Empty;
        }
        language = Path.GetFileNameWithoutExtension(language);
        if (language.StartsWith("texts_", StringComparison.OrdinalIgnoreCase))
        {
            language = language[6..];
        }
        if (language.StartsWith("console_", StringComparison.OrdinalIgnoreCase))
        {
            language = language[8..];
        }
        language = language.Replace('-', '_');
        return language switch
        {
            "en" => "english",
            "de" => "german",
            "fr" => "french",
            "it" => "italian",
            "es" => "spanish",
            "pl" => "polish",
            "ru" => "russian",
            "ja" => "japanese",
            "jp" => "japanese",
            "ko" => "korean",
            "kr" => "korean",
            "cs" => "czech",
            "pt" => "portuguese",
            "pt_br" => "brazilian",
            "br" => "brazilian",
            "es_mx" => "mexican",
            "mx" => "mexican",
            "zh" or "zh_cn" or "cn" => "chinese",
            "zh_tw" or "tw" => "tchinese",
            _ => language
        };
    }

    private static string GetAnnoLanguageKey(string assetLanguage) =>
        GameLanguageKeys.GetAnnoKeyOrEnglish(assetLanguage);
}
