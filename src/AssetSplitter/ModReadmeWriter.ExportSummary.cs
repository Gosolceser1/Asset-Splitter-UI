namespace AssetProcessor;

/// <summary>Writes per-export <c>MODDING-GUIDE.md</c>, short per-mod README files, and the export summary README.</summary>
internal static partial class ModReadmeWriter
{
    public static void WriteExportSummary(
        string outputRoot,
        string gameType,
        int created,
        int skipped,
        bool singleAssetMode,
        PipelineContext context)
    {
        string gameDisplayName = gameType.Equals("anno117", StringComparison.OrdinalIgnoreCase) ? "Anno 117" : "Anno 1800";
        string readmeLang = context.ReadmeLanguage;
        Func<string, string> t = key => ConsoleMessages.GetForLanguage(key, readmeLang);
        var lines = new List<string>
        {
            t("readmeSummaryTitle"),
            "",
            string.Format(t("readmeSummaryGame"), gameDisplayName),
        };
        lines.AddRange(GetBuildFootprintLines(context, t, includeGeneratedAt: true));
        lines.AddRange(
        [
            string.Format(t("readmeSummaryCreated"), created.ToString("N0")),
            string.Format(t("readmeSummarySkipped"), skipped.ToString("N0")),
            "",
            t("readmeSummaryIntro"),
            "",
            t("readmeSummaryGuide"),
        ]);
        if (!singleAssetMode)
        {
            lines.Add("");
            lines.Add(t("readmeSummaryIndex"));
            lines.Add("");
            lines.Add(t("readmeSummaryWarning"));
        }
        else
        {
            lines.Add("");
            lines.Add(t("readmeSummarySingle"));
        }
        lines.Add("");
        WriteUtf8Lines(Path.Combine(outputRoot, "README.md"), lines);
    }
}
