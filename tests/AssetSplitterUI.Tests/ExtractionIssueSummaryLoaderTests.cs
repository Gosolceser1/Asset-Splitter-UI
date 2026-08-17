using System.Text.Json;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.Tests;

public sealed class ExtractionIssueSummaryLoaderTests : IDisposable
{
    private readonly string _root;
    private readonly string _annoAssets;
    private readonly string _logsDir;

    public ExtractionIssueSummaryLoaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "assetsplitter-issue-summary-" + Guid.NewGuid().ToString("N"));
        _annoAssets = Path.Combine(_root, "AnnoAssets");
        _logsDir = Path.Combine(_annoAssets, "logs");
        Directory.CreateDirectory(_logsDir);
    }

    [Fact]
    public void TryLoadForRun_PrefersThisRunPath_OverStaleLatest()
    {
        string stale = Path.Combine(_logsDir, "issues_20260101_000000.json");
        string fresh = Path.Combine(_logsDir, "issues_20260101_000001.json");
        WriteReport(stale, total: 99, errors: 99);
        Thread.Sleep(20);
        WriteReport(fresh, total: 0, errors: 0);

        var report = ExtractionIssueSummaryLoader.TryLoadForRun(_annoAssets, stale);
        Assert.NotNull(report);
        Assert.Equal(99, report!.Counts!.Total);
        Assert.Equal(stale, report.ReportPath);
    }

    [Fact]
    public void TryLoadForRun_FallsBackToLatest_WhenThisRunPathMissing()
    {
        string older = Path.Combine(_logsDir, "issues_old.json");
        string newer = Path.Combine(_logsDir, "issues_new.json");
        WriteReport(older, total: 5, errors: 5);
        Thread.Sleep(20);
        WriteReport(newer, total: 0, errors: 0);

        var report = ExtractionIssueSummaryLoader.TryLoadForRun(_annoAssets, reportPathFromThisRun: null);
        Assert.NotNull(report);
        Assert.Equal(0, report!.Counts!.Total);
        Assert.Equal(newer, report.ReportPath);
    }

    [Fact]
    public void TryGetReportPathFromMarker_ParsesIssueSummaryLine()
    {
        string path = Path.Combine(_logsDir, "issues_x.json");
        string line = $"[ISSUE_SUMMARY] {{\"reportPath\":{JsonSerializer.Serialize(path)},\"errors\":0,\"warnings\":0}}";
        Assert.Equal(path, ExtractionIssueSummaryLoader.TryGetReportPathFromMarker(line));
        Assert.Null(ExtractionIssueSummaryLoader.TryGetReportPathFromMarker("unrelated"));
    }

    private static void WriteReport(string path, int total, int errors)
    {
        string json = $$"""
            {
              "GeneratedAt": "2026-01-01T00:00:00",
              "ReportPath": {{JsonSerializer.Serialize(path)}},
              "RunSucceeded": {{(total == 0).ToString().ToLowerInvariant()}},
              "Counts": { "Total": {{total}}, "Errors": {{errors}}, "Warnings": 0 },
              "Groups": [],
              "Issues": []
            }
            """;
        File.WriteAllText(path, json);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // best effort
        }
    }
}
