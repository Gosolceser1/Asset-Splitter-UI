using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetProcessor;

/// <summary>Writes grouped issue summaries to disk and the console (developer mode).</summary>
public static class PipelineIssueReporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string? WriteSummary(PipelineContext context, bool runSucceeded)
    {
        IReadOnlyList<PipelineIssue> issues = context.Issues.GetAll();
        if (issues.Count == 0)
            return WriteEmptySummaryIfDebug(context, runSucceeded);

        string logsDir = Path.Combine(
            string.IsNullOrWhiteSpace(context.AnnoAssetsRoot) ? context.BaseOutputDir : context.AnnoAssetsRoot,
            "logs");
        Directory.CreateDirectory(logsDir);

        string reportPath = Path.Combine(logsDir, $"issues_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        var report = BuildReport(issues, runSucceeded, reportPath);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));

        if (context.DebugMode && !Console.IsOutputRedirected)
            WriteConsoleSummary(context, report);

        Console.WriteLine($"[ISSUE_SUMMARY] {{\"reportPath\":{JsonSerializer.Serialize(reportPath)},\"errors\":{report.Counts.Errors},\"warnings\":{report.Counts.Warnings}}}");
        return reportPath;
    }

    private static string? WriteEmptySummaryIfDebug(PipelineContext context, bool runSucceeded)
    {
        if (!context.DebugMode)
            return null;

        string logsDir = Path.Combine(
            string.IsNullOrWhiteSpace(context.AnnoAssetsRoot) ? context.BaseOutputDir : context.AnnoAssetsRoot,
            "logs");
        Directory.CreateDirectory(logsDir);

        string reportPath = Path.Combine(logsDir, $"issues_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        var report = new PipelineIssueReport
        {
            GeneratedAt = DateTime.Now.ToString("O"),
            ReportPath = reportPath,
            RunSucceeded = runSucceeded,
            Counts = new PipelineIssueCounts(),
            Groups = [],
            Issues = [],
        };
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions));

        if (!Console.IsOutputRedirected)
            context.Log.Write("SUMMARY", ConsoleMessages.Get("issueSummaryNoneRecorded"), always: true);

        Console.WriteLine($"[ISSUE_SUMMARY] {{\"reportPath\":{JsonSerializer.Serialize(reportPath)},\"errors\":0,\"warnings\":0}}");
        return reportPath;
    }

    private static PipelineIssueReport BuildReport(IReadOnlyList<PipelineIssue> issues, bool runSucceeded, string reportPath)
    {
        var groups = issues
            .GroupBy(i => i.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                PipelineIssue first = g.First();
                return new PipelineIssueGroup
                {
                    Code = g.Key,
                    Severity = g.Any(i => i.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)) ? "Error" : "Warning",
                    Count = g.Count(),
                    Message = first.Message,
                    RootCause = first.RootCause,
                    Hint = first.Hint,
                    Phase = first.Phase,
                    UniqueParentGuids = g.Select(i => i.ParentGuid).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    Samples = g.Take(12).Select(ToSample).ToList(),
                };
            })
            .OrderByDescending(g => g.Severity == "Error")
            .ThenByDescending(g => g.Count)
            .ThenBy(g => g.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int errors = issues.Count(i => i.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
        int warnings = issues.Count - errors;

        return new PipelineIssueReport
        {
            GeneratedAt = DateTime.Now.ToString("O"),
            ReportPath = reportPath,
            RunSucceeded = runSucceeded,
            Counts = new PipelineIssueCounts { Total = issues.Count, Errors = errors, Warnings = warnings },
            Groups = groups,
            Issues = issues.Select(ToSample).ToList(),
        };
    }

    private static PipelineIssueSample ToSample(PipelineIssue issue) =>
        new()
        {
            Code = issue.Code,
            Severity = issue.Severity,
            Message = issue.Message,
            RootCause = issue.RootCause,
            Hint = issue.Hint,
            Phase = issue.Phase,
            ChildGuid = issue.ChildGuid,
            ChildDisplayName = issue.ChildDisplayName,
            ParentGuid = issue.ParentGuid,
            RelatedGuid = issue.RelatedGuid,
            FilePath = issue.FilePath,
            Detail = issue.Detail,
        };

    private static void WriteConsoleSummary(PipelineContext context, PipelineIssueReport report)
    {
        context.Log.Write("SUMMARY", ConsoleMessages.Get("issueSummaryHeader"), always: true);
        context.Log.Write("SUMMARY", string.Format(ConsoleMessages.Get("issueSummaryCounts"), report.Counts.Warnings, report.Counts.Errors), always: true);
        context.Log.Write("SUMMARY", string.Format(ConsoleMessages.Get("issueSummaryReportPath"), report.ReportPath), always: true);
        context.Log.Write("SUMMARY", "", always: true);

        foreach (PipelineIssueGroup group in report.Groups)
        {
            string countLabel = group.Code == PipelineIssueCodes.ParentAssetNotInGuidIndex && group.UniqueParentGuids > 0
                ? string.Format(ConsoleMessages.Get("issueSummaryGroupWithParents"), group.Count, group.UniqueParentGuids)
                : group.Count.ToString();

            context.Log.Write(group.Severity.ToUpperInvariant(),
                string.Format(ConsoleMessages.Get("issueSummaryGroupLine"), group.Code, countLabel),
                always: true);
            context.Log.Write("INFO", "  " + ConsoleMessages.Get("issueSummaryRootCausePrefix") + group.RootCause, always: true);
            if (!string.IsNullOrWhiteSpace(group.Hint))
                context.Log.Write("INFO", "  " + ConsoleMessages.Get("issueSummaryHintPrefix") + group.Hint, always: true);

            foreach (PipelineIssueSample sample in group.Samples.Take(5))
            {
                string line = FormatSampleLine(sample);
                if (!string.IsNullOrEmpty(line))
                    context.Log.Write("INFO", ConsoleMessages.Get("issueSummarySampleBulletPrefix") + line, always: true);
            }

            if (group.Count > 5)
                context.Log.Write("INFO", string.Format(ConsoleMessages.Get("issueSummaryMoreInReport"), group.Count - 5), always: true);

            context.Log.Write("SUMMARY", "", always: true);
        }
    }

    private static string FormatSampleLine(PipelineIssueSample sample)
    {
        if (!string.IsNullOrWhiteSpace(sample.ChildDisplayName) && !string.IsNullOrWhiteSpace(sample.ParentGuid))
            return string.Format(ConsoleMessages.Get("issueSummarySampleChildParent"), sample.ChildDisplayName, sample.ParentGuid);

        if (!string.IsNullOrWhiteSpace(sample.ChildGuid))
            return string.Format(ConsoleMessages.Get("issueSummarySampleGuid"), sample.ChildGuid, sample.Detail ?? sample.Message);

        if (!string.IsNullOrWhiteSpace(sample.FilePath))
        {
            string name = Path.GetFileName(sample.FilePath);
            return string.IsNullOrWhiteSpace(sample.Detail)
                ? name
                : string.Format(ConsoleMessages.Get("issueSummarySampleFileDetail"), name, sample.Detail);
        }

        return sample.Message;
    }

    private sealed class PipelineIssueReport
    {
        public string GeneratedAt { get; set; } = "";
        public string ReportPath { get; set; } = "";
        public bool RunSucceeded { get; set; }
        public PipelineIssueCounts Counts { get; set; } = new();
        public List<PipelineIssueGroup> Groups { get; set; } = [];
        public List<PipelineIssueSample> Issues { get; set; } = [];
    }

    private sealed class PipelineIssueCounts
    {
        public int Total { get; set; }
        public int Errors { get; set; }
        public int Warnings { get; set; }
    }

    private sealed class PipelineIssueGroup
    {
        public string Code { get; set; } = "";
        public string Severity { get; set; } = "";
        public int Count { get; set; }
        public string Message { get; set; } = "";
        public string RootCause { get; set; } = "";
        public string? Hint { get; set; }
        public string? Phase { get; set; }
        public int UniqueParentGuids { get; set; }
        public List<PipelineIssueSample> Samples { get; set; } = [];
    }

    private sealed class PipelineIssueSample
    {
        public string Code { get; set; } = "";
        public string Severity { get; set; } = "";
        public string Message { get; set; } = "";
        public string RootCause { get; set; } = "";
        public string? Hint { get; set; }
        public string? Phase { get; set; }
        public string? ChildGuid { get; set; }
        public string? ChildDisplayName { get; set; }
        public string? ParentGuid { get; set; }
        public string? RelatedGuid { get; set; }
        public string? FilePath { get; set; }
        public string? Detail { get; set; }
    }
}
