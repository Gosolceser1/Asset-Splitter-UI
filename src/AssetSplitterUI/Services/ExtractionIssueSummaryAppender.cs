using AssetSplitterUI.Localization;
using AssetSplitterUI.ViewModels;
using static AssetSplitterUI.Services.ExtractionIssueSummaryLoader;

namespace AssetSplitterUI.Services;

/// <summary>Appends a grouped developer issue summary to the console after extraction (no log scraping).</summary>
internal static class ExtractionIssueSummaryAppender
{
    public static void AppendIfAny(MainWindowLogStore logStore, string annoAssetsPath)
    {
        var report = TryLoadLatest(annoAssetsPath);
        if (report?.Counts is { Total: > 0 })
        {
            AppendReport(logStore, report);
        }
    }

    /// <summary>In debug mode, always ends the console with a structured issue block (issues listed or explicit none).</summary>
    public static void AppendForDeveloperMode(MainWindowLogStore logStore, string annoAssetsPath)
    {
        var report = TryLoadLatest(annoAssetsPath);
        if (report?.Counts is { Total: > 0 })
        {
            AppendReport(logStore, report);
            return;
        }

        logStore.AppendRaw("");
        logStore.AppendRaw(ExtractionRunResultAppender.Phase2Separator);
        logStore.AppendLocalized("issueSummary.title");
        logStore.AppendLocalized("issueSummary.noneRecorded");
        logStore.AppendRaw(ExtractionRunResultAppender.Phase2Separator);
        logStore.AppendRaw("");
    }

    public static void AppendReport(MainWindowLogStore logStore, ExtractionIssueReport report)
    {
        if (report.Counts is null || report.Counts.Total == 0)
        {
            return;
        }

        logStore.AppendRaw("");
        logStore.AppendRaw(ExtractionRunResultAppender.Phase2Separator);
        logStore.AppendLocalized("issueSummary.title");
        logStore.AppendLocalized("issueSummary.counts", [report.Counts.Warnings, report.Counts.Errors]);

        if (!string.IsNullOrWhiteSpace(report.ReportPath))
        {
            logStore.AppendLocalized("issueSummary.reportPath", [report.ReportPath]);
        }

        logStore.AppendRaw("");

        foreach (var group in report.Groups)
        {
            object countText = group.Code.Equals("ParentAssetNotInGuidIndex", StringComparison.OrdinalIgnoreCase) && group.UniqueParentGuids > 0
                ? new LocalizedConsoleArgument("issueSummary.groupWithParents", [group.Count, group.UniqueParentGuids])
                : group.Count.ToString();

            string groupTitle = IssueSummaryLocalizer.GetGroupTitle(group.Code);
            logStore.AppendLocalized("issueSummary.groupHeader", [groupTitle, countText]);

            string rootCause = IssueSummaryLocalizer.GetRootCause(group.Code, group.RootCause);
            if (!string.IsNullOrWhiteSpace(rootCause))
            {
                logStore.AppendLocalized("issueSummary.rootCause", [rootCause]);
            }

            string hint = IssueSummaryLocalizer.GetHint(group.Code, group.Hint);
            if (!string.IsNullOrWhiteSpace(hint))
            {
                logStore.AppendLocalized("issueSummary.hint", [hint]);
            }

            foreach (var sample in group.Samples.Take(5))
            {
                if (TryGetSampleLocalization(sample, out string? key, out object[]? args))
                {
                    logStore.AppendLocalized(key!, args);
                }
                else if (!string.IsNullOrWhiteSpace(sample.Message))
                {
                    logStore.AppendRaw(IssueSummaryLocalizer.SampleBulletPrefix + sample.Message);
                }
            }

            if (group.Count > 5)
            {
                logStore.AppendLocalized("issueSummary.moreInReport", [group.Count - 5]);
            }

            logStore.AppendRaw("");
        }

        logStore.AppendRaw(ExtractionRunResultAppender.Phase2Separator);
        logStore.AppendRaw("");
    }

    private static bool TryGetSampleLocalization(ExtractionIssueSample sample, out string? key, out object[]? args)
    {
        key = null;
        args = null;

        if (!string.IsNullOrWhiteSpace(sample.ChildDisplayName) && !string.IsNullOrWhiteSpace(sample.ParentGuid))
        {
            key = "issueSummary.sampleChildParent";
            args = [sample.ChildDisplayName, sample.ParentGuid];
            return true;
        }

        if (!string.IsNullOrWhiteSpace(sample.ChildGuid))
        {
            key = "issueSummary.sampleGuid";
            args = [sample.ChildGuid, sample.Detail ?? sample.Message ?? ""];
            return true;
        }

        if (!string.IsNullOrWhiteSpace(sample.FilePath))
        {
            key = "issueSummary.sampleFileDetail";
            args = [Path.GetFileName(sample.FilePath), sample.Detail ?? ""];
            return true;
        }

        return false;
    }
}
