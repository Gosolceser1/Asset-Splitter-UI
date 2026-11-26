namespace AssetProcessor;

/// <summary>Writes per-export <c>MODDING-GUIDE.md</c>, short per-mod README files, and the export summary README.</summary>
internal static partial class ModReadmeWriter
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
        // Guide text follows --readme-lang; the "extraction language" fact stays the game language.
        string readmeLang = NormalizeAssetLanguage(context.ReadmeLanguage);
        Func<string, string> t = key => ConsoleMessages.GetForLanguage(key, readmeLang);
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
        InsertBlockBefore(lines, t("readmeGuideQuickStartHeader"), GetBuildFootprintLines(context, t, includeGeneratedAt: true));
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
        t("readmeRefAssetSplitter"),
    ];

    private static IEnumerable<string> GetShortReferenceLines(Func<string, string> t) =>
    [
        t("readmeRefModLoader"),
        t("readmeRefModOpsBasics"),
        t("readmeRefGuidLookup"),
        t("readmeRefAssetSplitter"),
    ];
}
