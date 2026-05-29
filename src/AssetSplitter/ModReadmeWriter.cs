namespace AssetProcessor;



/// <summary>Writes per-export <c>MODDING-GUIDE.md</c>, short per-mod README files, and the export summary README.</summary>

internal static class ModReadmeWriter

{

    public const string ModdingGuideFileName = "MODDING-GUIDE.md";



    public static void WriteModdingGuide(string outputRoot, string gameType, PipelineContext context)

    {

        bool isAnno117 = gameType.Equals("anno117", StringComparison.OrdinalIgnoreCase);

        string gameDisplayName = isAnno117 ? "Anno 117" : "Anno 1800";

        string assetPath = isAnno117

            ? "data/base/config/export/assets.xml"

            : "data/config/export/main/asset/assets.xml";

        string assetLang = NormalizeAssetLanguage(context.AssetLanguage);

        Func<string, string> t = key => ConsoleMessages.GetForLanguage(key, assetLang);

        string extractionLanguage = GetAnnoLanguageKey(assetLang);



        string[] gameSpecificLines = isAnno117

            ? [t("readmeAnno117Install"), t("readmeAnno117Modinfo"), t("readmeAnno117GameSetup"), t("readmeAnno117AssetsPath"), t("readmeAnno117ShortModOps"), t("readmeAnno117LegacyModOps"), t("readmeAnno117Profile"), t("readmeAnno117FolderDisable"), t("readmeAnno117EnableNew"), t("readmeAnno117Deps"), t("readmeAnno117Log"), t("readmeAnno117HotReload")]

            : [t("readmeAnno1800Install"), t("readmeAnno1800Fallback"), t("readmeAnno1800AssetsPath"), t("readmeAnno1800LegacyModOps"), t("readmeAnno1800PathNote"), t("readmeAnno1800AnnoField"), t("readmeAnno1800Activation"), t("readmeAnno1800Zip"), t("readmeAnno1800Deps"), t("readmeAnno1800Log"), t("readmeAnno1800NoHotReload")];



        var lines = new List<string>

        {

            t("readmeGuideTitle"),

            "",

            t("readmeGuideIntro"),

            "",

            string.Format(t("readmeGuideLanguageNote"), extractionLanguage),

            "",

            t("readmeGuideQuickStartHeader"),

            "",

            t("readmeGuideQuickStep1"),

            string.Format(t("readmeGuideQuickStep2"), gameDisplayName),

            t("readmeGuideQuickStep3"),

            t("readmeGuideQuickStep4"),

            t("readmeGuideQuickStep5"),

            "",

            string.Format(t("readmeGameRulesHeader"), gameDisplayName),

            "",

        };

        lines.AddRange(gameSpecificLines);

        lines.AddRange(

        [

            "",

            t("readmeGuideFolderLayoutHeader"),

            "",

            t("readmeGuideFolderLayoutBody"),

            "",

            t("readmeShapeHeader"),

            "",

            "```text",

            "your-mod-folder/",

            "  modinfo.json",

            "  README.md",

            "  data/",

            isAnno117 ? "    base/config/export/assets.xml" : "    config/export/main/asset/assets.xml",

            "```",

            "",

            string.Format(t("readmeEditFile"), assetPath),

            "",

            t("readmeModdingBasicsHeader"),

            "",

            t("readmeBasics1"),

            t("readmeBasics2"),

            t("readmeBasics3"),

            t("readmeBasics4"),

            t("readmeBasics5"),

            t("readmeBasics6"),

            t("readmeBasics7"),

            t("readmeBasics8"),

            "",

            t("readmeTestingHeader"),

            "",

            t("readmeTesting1"),

            t("readmeTesting2"),

            isAnno117 ? t("readmeTesting3_117") : t("readmeTesting3_1800"),

            t("readmeTesting4"),

            "",

            t("readmeGuidePublishHeader"),

            "",

            t("readmeGuidePublish1"),

            t("readmeGuidePublish2"),

            t("readmeGuidePublish3"),

            t("readmeGuidePublish4"),

            "",

            t("readmeNotesHeader"),

            "",

            t("readmeNotes1"),

            t("readmeNotes2"),

            t("readmeNotes3"),

            "",

            t("readmeWhatIsHeader"),

            "",

            t("readmeWhatIsAsset"),

            t("readmeWhatIsGuid"),

            t("readmeWhatIsTemplate"),

            t("readmeWhatIsRda"),

            t("readmeWhatIsModLoader"),

            t("readmeWhatIsModOp"),

            t("readmeWhatIsModinfo"),

            t("readmeWhatIsXPath"),

            t("readmeWhatIsModOpsWrapper"),

            t("readmeWhatIsFlow"),

            "",

            t("readmeXPathHeader"),

            "",

            t("readmeXPathIntro"),

            t("readmeXPathTree"),

            "",

            t("readmeXPathExamples"),

            "",

            t("readmeModOpsRefHeader"),

            "",

            t("readmeModOpsRefIntro"),

            "",

            string.Format(t("readmeModOpsRefReplace"), "123456", "Example asset"),

            "",

            string.Format(t("readmeModOpsRefMerge"), "123456", "Example asset"),

            "",

            string.Format(t("readmeModOpsRefAdd"), "123456"),

            "",

            string.Format(t("readmeModOpsRefRemove"), "123456"),

            "",

            string.Format(t("readmeModOpsRefAddNextSibling"), "123456"),

            "",

            string.Format(t("readmeModOpsRefAddPrevSibling"), "123456"),

            "",

            t("readmeModOpsRefAsset"),

            "",

            t("readmeModOpsAdvHeader"),

            "",

            t("readmeModOpsAdvIntro"),

            t("readmeModOpsAdvGroup"),

            t("readmeModOpsAdvInclude"),

            t("readmeModOpsAdvCondition"),

            t("readmeModOpsAdvModItem"),

            t("readmeModOpsAdvModValue"),

            "",

            t("readmeReferencesHeader"),

            "",

        ]);

        lines.AddRange(GetFullReferenceLines(t));

        lines.Add("");



        WriteUtf8Lines(Path.Combine(outputRoot, ModdingGuideFileName), lines);

    }



    public static void WriteShortReadme(

        string modRoot,

        string outputRoot,

        string gameType,

        AssetModMetadata metadata,

        string templateName,

        bool singleAssetMode,

        PipelineContext context)

    {

        bool isAnno117 = gameType.Equals("anno117", StringComparison.OrdinalIgnoreCase);

        string assetPath = isAnno117

            ? "data/base/config/export/assets.xml"

            : "data/config/export/main/asset/assets.xml";

        string modFolderName = Path.GetFileName(modRoot);

        string parentFolderName = Path.GetFileName(Path.GetDirectoryName(modRoot) ?? "");

        string gameDisplayName = isAnno117 ? "Anno 117" : "Anno 1800";

        int annoVersion = isAnno117 ? 8 : 7;

        string internalName = DisplayValue(metadata.InternalName);

        string pathHint = DisplayValue(metadata.PathHint);

        string guideLink = GetModdingGuideRelativeLink(modRoot, outputRoot);

        string assetLang = NormalizeAssetLanguage(context.AssetLanguage);

        Func<string, string> t = key => ConsoleMessages.GetForLanguage(key, assetLang);

        string exampleName = EscapeXmlText($"{metadata.DisplayName} (modded)");



        var lines = new List<string>

        {

            $"# {metadata.Guid} — {metadata.DisplayName}",

            "",

            t("readmeShortIntro"),

            "",

            t("readmeShortInstallHeader"),

            "",

            isAnno117 ? t("readmeShortInstall117") : t("readmeShortInstall1800"),

            "",

        };



        if (!singleAssetMode)

        {

            lines.Add(string.Format(t("readmeShortBrowsingNote"), parentFolderName, modFolderName));

            lines.Add("");

        }



        lines.AddRange(

        [

            t("readmeShortQuickStartHeader"),

            "",

            string.Format(t("readmeShortStep1"), modFolderName),

            t("readmeShortStep2"),

            string.Format(t("readmeShortStep3"), assetPath),

            t("readmeShortStep4"),

            isAnno117 ? t("readmeShortStep5_117") : t("readmeShortStep5_1800"),

            "",

            t("readmeAssetHeader"),

            "",

            string.Format(t("readmeGuidLabel"), metadata.Guid),

            string.Format(t("readmeDisplayNameLabel"), metadata.DisplayName),

            string.Format(t("readmeInternalNameLabel"), internalName),

            string.Format(t("readmeTemplateLabel"), templateName),

            string.Format(t("readmePathHintLabel"), pathHint),

            string.Format(t("readmeGameLabel"), gameDisplayName),

            string.Format(t("readmeAnnoVersionLabel"), annoVersion),

            "",

            t("readmeFileToEditHeader"),

            "",

            string.Format(t("readmeEditFile"), assetPath),

            "",

            t("readmeEditDescription"),

            "",

            t("readmeShortTryHeader"),

            "",

            t("readmeShortTryIntro"),

            "",

            string.Format(t("readmeShortTryMergeExample"), metadata.Guid, exampleName),

            "",

            t("readmeShortTryMergeNote"),

            "",

            t("readmeShortPublishHeader"),

            "",

            t("readmeShortModinfoNote"),

            t("readmeShortFailNameNote"),

            "",

            t("readmeShortHelpHeader"),

            "",

            string.Format(t("readmeShortLearnMore"), guideLink, ModdingGuideFileName),

            "",

        ]);

        lines.AddRange(GetShortReferenceLines(t));

        lines.Add("");



        WriteUtf8Lines(Path.Combine(modRoot, "README.md"), lines);

    }



    public static void WriteExportSummary(

        string outputRoot,

        string gameType,

        int created,

        int skipped,

        bool singleAssetMode)

    {

        string gameDisplayName = gameType.Equals("anno117", StringComparison.OrdinalIgnoreCase) ? "Anno 117" : "Anno 1800";

        var lines = new List<string>

        {

            ConsoleMessages.Get("readmeSummaryTitle"),

            "",

            string.Format(ConsoleMessages.Get("readmeSummaryGame"), gameDisplayName),

            string.Format(ConsoleMessages.Get("readmeSummaryCreated"), created.ToString("N0")),

            string.Format(ConsoleMessages.Get("readmeSummarySkipped"), skipped.ToString("N0")),

            "",

            ConsoleMessages.Get("readmeSummaryIntro"),

            "",

            ConsoleMessages.Get("readmeSummaryGuide"),

        };



        if (!singleAssetMode)

        {

            lines.Add("");

            lines.Add(ConsoleMessages.Get("readmeSummaryIndex"));

            lines.Add("");

            lines.Add(ConsoleMessages.Get("readmeSummaryWarning"));

        }

        else

        {

            lines.Add("");

            lines.Add(ConsoleMessages.Get("readmeSummarySingle"));

        }



        lines.Add("");

        WriteUtf8Lines(Path.Combine(outputRoot, "README.md"), lines);

    }



    public static string GetIndexTitle(string templateFolder) =>

        string.Format(ConsoleMessages.Get("readmeIndexTitle"), templateFolder);



    public static string GetIndexBrowseLine() =>

        string.Format(ConsoleMessages.Get("readmeIndexBrowseLine"), ModdingGuideFileName);



    public static IReadOnlyList<string> GetIndexTableHeader() =>

    [

        ConsoleMessages.Get("readmeIndexColGuid"),

        ConsoleMessages.Get("readmeIndexColDisplayName"),

        ConsoleMessages.Get("readmeIndexColInternalName"),

        ConsoleMessages.Get("readmeIndexColPathHint"),

        ConsoleMessages.Get("readmeIndexColFolder"),

    ];



    private static string GetModdingGuideRelativeLink(string modRoot, string outputRoot)

    {

        string relative = Path.GetRelativePath(modRoot, Path.Combine(outputRoot, ModdingGuideFileName));

        return relative.Replace('\\', '/');

    }



    private static IEnumerable<string> GetFullReferenceLines(Func<string, string> t) =>

    [

        t("readmeRefModLoader"),

        t("readmeRefFileStructure"),

        t("readmeRefModinfo"),

        t("readmeRefModOps"),

        t("readmeRefModOpsBasics"),

        t("readmeRefDebugging"),

        t("readmeRefGuidRanges"),

        t("readmeRefGuidLookup"),

    ];



    private static IEnumerable<string> GetShortReferenceLines(Func<string, string> t) =>

    [

        t("readmeRefModLoader"),

        t("readmeRefModOpsBasics"),

        t("readmeRefGuidLookup"),

    ];



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

            return string.Empty;



        language = Path.GetFileNameWithoutExtension(language);

        if (language.StartsWith("texts_", StringComparison.OrdinalIgnoreCase))

            language = language[6..];

        if (language.StartsWith("console_", StringComparison.OrdinalIgnoreCase))

            language = language[8..];



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



    private static readonly Dictionary<string, string> GameLanguageToAnnoKey = new(StringComparer.OrdinalIgnoreCase)

    {

        ["english"] = "English", ["german"] = "German", ["french"] = "French",

        ["italian"] = "Italian", ["spanish"] = "Spanish", ["polish"] = "Polish",

        ["russian"] = "Russian", ["japanese"] = "Japanese", ["korean"] = "Korean",

        ["chinese"] = "Chinese (Simplified)", ["tchinese"] = "Chinese (Traditional)",

        ["czech"] = "Czech", ["portuguese"] = "Portuguese", ["brazilian"] = "Brazilian Portuguese",

        ["mexican"] = "Mexican Spanish", ["simplified_chinese"] = "Chinese (Simplified)",

        ["traditional_chinese"] = "Chinese (Traditional)"

    };



    private static string GetAnnoLanguageKey(string assetLanguage) =>

        GameLanguageToAnnoKey.TryGetValue(assetLanguage, out string? key) ? key : "English";

}

