namespace AssetProcessor;

/// <summary>Writes per-export <c>MODDING-GUIDE.md</c>, short per-mod README files, and the export summary README.</summary>
internal static partial class ModReadmeWriter
{
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
        InsertBlockBefore(lines, t("readmeFileToEditHeader"), GetBuildFootprintLines(context, t, includeGeneratedAt: true));
        lines.AddRange(GetShortReferenceLines(t));
        lines.Add("");
        WriteUtf8Lines(Path.Combine(modRoot, "README.md"), lines);
    }
}
