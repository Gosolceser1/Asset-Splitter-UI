using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetSplitterUI.Services;

/// <summary>Loads structured issue reports written by the backend under AnnoAssets/logs/issues_*.json.</summary>
internal static class ExtractionIssueSummaryLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static ExtractionIssueReport? TryLoadLatest(string annoAssetsPath)
    {
        string logsDir = Path.Combine(annoAssetsPath, "logs");
        if (!Directory.Exists(logsDir))
            return null;

        string? latest = Directory
            .EnumerateFiles(logsDir, "issues_*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (latest is null)
            return null;

        try
        {
            string json = File.ReadAllText(latest);
            var report = JsonSerializer.Deserialize<ExtractionIssueReport>(json, JsonOptions);
            if (report is null)
                return null;

            report.ReportPath ??= latest;
            return report;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            UILogger.Debug(nameof(ExtractionIssueSummaryLoader), "Could not read issue report: " + ex.Message);
            return null;
        }
    }

    public static ExtractionIssueReport? TryParseSummaryMarker(string line)
    {
        const string prefix = "[ISSUE_SUMMARY] ";
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(line[prefix.Length..]);
            if (!TryGetProperty(doc.RootElement, "reportPath", "ReportPath", out JsonElement pathEl))
                return null;

            string? path = pathEl.GetString();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            return TryLoadFromFile(path);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetProperty(JsonElement parent, string camelName, string pascalName, out JsonElement value)
    {
        if (parent.TryGetProperty(camelName, out value) || parent.TryGetProperty(pascalName, out value))
            return true;

        value = default;
        return false;
    }

    private static ExtractionIssueReport? TryLoadFromFile(string path)
    {
        try
        {
            var report = JsonSerializer.Deserialize<ExtractionIssueReport>(File.ReadAllText(path), JsonOptions);
            if (report is not null)
                report.ReportPath = path;
            return report;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            UILogger.Debug(nameof(ExtractionIssueSummaryLoader), "Could not parse issue report: " + ex.Message);
            return null;
        }
    }

    internal sealed class ExtractionIssueReport
    {
        public string? GeneratedAt { get; set; }
        public string? ReportPath { get; set; }
        public bool RunSucceeded { get; set; }
        public ExtractionIssueCounts? Counts { get; set; }
        public List<ExtractionIssueGroup> Groups { get; set; } = [];
    }

    internal sealed class ExtractionIssueCounts
    {
        public int Total { get; set; }
        public int Errors { get; set; }
        public int Warnings { get; set; }
    }

    internal sealed class ExtractionIssueGroup
    {
        public string Code { get; set; } = "";
        public string Severity { get; set; } = "";
        public int Count { get; set; }
        public string Message { get; set; } = "";
        public string RootCause { get; set; } = "";
        public string? Hint { get; set; }
        public string? Phase { get; set; }
        public int UniqueParentGuids { get; set; }
        public List<ExtractionIssueSample> Samples { get; set; } = [];
    }

    internal sealed class ExtractionIssueSample
    {
        public string? ChildDisplayName { get; set; }
        public string? ParentGuid { get; set; }
        public string? ChildGuid { get; set; }
        public string? FilePath { get; set; }
        public string? Detail { get; set; }
        public string? Message { get; set; }
    }
}
