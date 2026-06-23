using System.Windows.Input;
using AssetProcessor;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DebugModeToolTipText))]
    public partial bool DebugMode { get; set; }

    [ObservableProperty]
    public partial bool DeveloperOptionsUnlocked { get; set; }

    public ICommand OpenAnnoAssetsFolderCommand { get; }
    public ICommand ClearLogCommand { get; }

    public void UnlockDeveloperOptions()
    {
        DeveloperOptionsUnlocked = true;
        if (!IsProcessing)
        {
            DebugMode = true;
        }
    }

    partial void OnDeveloperOptionsUnlockedChanged(bool value)
    {
        if (!value)
        {
            DebugMode = false;
            SetStatusTextLocalized("console.ready");
        }
    }

    partial void OnDebugModeChanged(bool value)
    {
        if (!value)
            DeveloperOptionsUnlocked = false;

        OnPropertyChanged(nameof(ShowStructuredRunStatus));
        OnPropertyChanged(nameof(ShowLegacyStatusLine));
        OnPropertyChanged(nameof(ShowDeveloperRawStatusMirror));
        OnPropertyChanged(nameof(ShowDeveloperStepPercent));
        OnPropertyChanged(nameof(ShowRunOperationCounts));
        SaveSettingsIfReady();
    }

    private async Task OpenAnnoAssetsFolderAsync()
    {
        string path = GetResolvedAnnoAssetsPath();
        if (Directory.Exists(path))
        {
            await _platformServices.OpenFolderAsync(path);
        }
    }

    public void ClearLog()
    {
        _logStore.Clear();
        SetStatusTextLocalized("console.ready");
        StatusIsError = false;
    }

    public string BuildDeveloperReport()
    {
        string languageForReport = _lastRunBackendAssetLanguage ?? ResolveBackendLanguage();

        // Preserve exact backend run behavior: when a prior run had no GUID, pass empty-string override
        // instead of null so BuildBackendArgs does not fall back to current UI SingleGuid.
        string? guidOverrideForBackendArgs = _lastRunBackendAssetLanguage == null
            ? null
            : _lastRunBackendSingleGuid.Trim();

        string? singleGuidForReport = _lastRunBackendAssetLanguage == null
            ? (string.IsNullOrWhiteSpace(SingleGuid) ? null : SingleGuid.Trim())
            : string.IsNullOrWhiteSpace(_lastRunBackendSingleGuid) ? null : _lastRunBackendSingleGuid.Trim();

        string annoAssetsPath = GetResolvedAnnoAssetsPath();
        string sourceXmlPath = GetResolvedSourceXmlPath(annoAssetsPath);
        int logLinesToCapture = LogLines.Count;

        // In debug mode, also write the full console log to a file for unlimited capture
        if (DebugMode)
        {
            try
            {
                string? logDir = OutputDirectoryManager.TryPrepareLogsDirectory(GetResolvedAnnoAssetsPath());
                if (logDir is not null)
                {
                    string logPath = Path.Combine(logDir, $"console_full_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                    File.WriteAllText(logPath, string.Join(Environment.NewLine, LogLines.Select(l => l.Text)));
                }
            }
            catch { /* best effort */ }
        }
        var logTail = LogLines.TakeLast(logLinesToCapture).Select(line => line.Text);
        static string T(string key) => Localization.StringResourceManager.Instance.GetString(key);
        static string TF(string key, params object[] args) => string.Format(Localization.StringResourceManager.Instance.GetString(key), args);

        var reportLines = new List<string>
        {
            T("developerReport.title"),
            TF("developerReport.timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
            TF("developerReport.theme", SelectedTheme),
            TF("developerReport.uiLanguage", SelectedUILanguage),
            TF("developerReport.gamePath", GamePath),
            TF("developerReport.outputPath", OutputPath),
            TF("developerReport.resolvedAnnoAssetsPath", annoAssetsPath),
            TF("developerReport.sourceXmlPath", sourceXmlPath),
            TF("developerReport.selectedAssetLanguage", languageForReport),
            TF("developerReport.singleGuid", singleGuidForReport ?? T("developerReport.none")),
            TF("developerReport.phase1Complete", Phase1Complete),
            TF("developerReport.processing", IsProcessing),
            TF("developerReport.progress", Progress.ToString("F1")),
            TF("developerReport.status", StatusText),
            TF("developerReport.statusIsError", StatusIsError),
            TF("developerReport.detectedGameType", string.IsNullOrWhiteSpace(DetectedGameType) ? T("developerReport.unknown") : DetectedGameType),
            TF("developerReport.availableGameLanguages", string.Join(", ", AvailableGameLanguages)),
            TF("developerReport.options", AddComments, FixDependencies, CreateTemplateFolders, ModOpsWrap, IncludeDefaultProperties, SplitTemplates, CreateAssetMods, DebugMode),
            TF("developerReport.pathChecks", Directory.Exists(GamePath), Directory.Exists(OutputPath), Directory.Exists(annoAssetsPath), Directory.Exists(sourceXmlPath), Directory.Exists(Path.Combine(GamePath, "maindata"))),
            T("developerReport.backendCommand"),
            "  AssetProcessor.exe " + BuildBackendArgs(GamePath, annoAssetsPath, languageForReport, guidOverrideForBackendArgs),
            "",
        };

        AppendIssueReportSection(reportLines, annoAssetsPath, T, TF);
        AppendConsoleAlertsSection(reportLines, T, TF);
        reportLines.Add(T("developerReport.lastConsoleLines"));
        reportLines.AddRange(logTail);
        reportLines.Add("");
        if (DebugMode)
        {
            reportLines.Add(TF("developerReport.debugCaptureNote", logLinesToCapture));
        }

        return string.Join(Environment.NewLine, reportLines);
    }

    private void AppendConsoleAlertsSection(
        List<string> lines,
        Func<string, string> t,
        Func<string, object[], string> tf)
    {
        const int maxLines = 250;
        var alerts = LogLines
            .Select(l => l.Text)
            .Where(ConsoleLineClassifier.IsReportableAlert)
            .Distinct()
            .ToList();

        lines.Add(t("developerReport.consoleAlertsSection"));
        if (alerts.Count == 0)
        {
            lines.Add(t("developerReport.consoleAlertsNone"));
        }
        else
        {
            lines.AddRange(alerts.Take(maxLines));
            if (alerts.Count > maxLines)
            {
                lines.Add(tf("developerReport.consoleAlertsMore", [alerts.Count - maxLines]));
            }
        }

        lines.Add(t("developerReport.consoleAlertsNote"));
        lines.Add("");
    }

    private static void AppendIssueReportSection(
        List<string> lines,
        string annoAssetsPath,
        Func<string, string> t,
        Func<string, object[], string> tf)
    {
        var issueReport = ExtractionIssueSummaryLoader.TryLoadLatest(annoAssetsPath);
        if (issueReport?.Counts is null || issueReport.Counts.Total == 0)
        {
            lines.Add(t("developerReport.issuesNone"));
            lines.Add("");
            return;
        }

        lines.Add(t("developerReport.issuesSection"));
        lines.Add(tf("developerReport.issuesCounts", [issueReport.Counts.Warnings, issueReport.Counts.Errors]));
        if (!string.IsNullOrWhiteSpace(issueReport.ReportPath))
        {
            lines.Add(tf("developerReport.issuesReportPath", [issueReport.ReportPath]));
        }

        lines.Add("");

        foreach (var group in issueReport.Groups)
        {
            string groupTitle = IssueSummaryLocalizer.GetGroupTitle(group.Code);
            lines.Add(tf("developerReport.issuesGroup", [groupTitle, group.Count]));

            string rootCause = IssueSummaryLocalizer.GetRootCause(group.Code, group.RootCause);
            if (!string.IsNullOrWhiteSpace(rootCause))
            {
                lines.Add(t("developerReport.issuesRootCause") + rootCause);
            }

            string hint = IssueSummaryLocalizer.GetHint(group.Code, group.Hint);
            if (!string.IsNullOrWhiteSpace(hint))
            {
                lines.Add(t("developerReport.issuesHint") + hint);
            }

            foreach (var sample in group.Samples.Take(8))
            {
                string? sampleLine = FormatIssueSampleForReport(sample);
                if (!string.IsNullOrWhiteSpace(sampleLine))
                {
                    lines.Add("  " + IssueSummaryLocalizer.SampleBulletPrefix.Trim() + sampleLine);
                }
            }

            if (group.Count > 8)
            {
                lines.Add(tf("developerReport.issuesMore", [group.Count - 8]));
            }

            lines.Add("");
        }
    }

    private static string? FormatIssueSampleForReport(ExtractionIssueSummaryLoader.ExtractionIssueSample sample)
    {
        if (!string.IsNullOrWhiteSpace(sample.ChildDisplayName) && !string.IsNullOrWhiteSpace(sample.ParentGuid))
        {
            return string.Format(
                StringResourceManager.Instance.GetString("issueSummary.sampleChildParent"),
                sample.ChildDisplayName,
                sample.ParentGuid);
        }

        if (!string.IsNullOrWhiteSpace(sample.FilePath))
        {
            return IssueSummaryLocalizer.SampleFileDetail(Path.GetFileName(sample.FilePath), sample.Detail);
        }

        return sample.Message;
    }

    private string GetResolvedAnnoAssetsPath() =>
        Path.GetFileName(Path.TrimEndingDirectorySeparator(OutputPath)).Equals("AnnoAssets", StringComparison.OrdinalIgnoreCase)
            ? OutputPath
            : Path.Combine(OutputPath, "AnnoAssets");

    private string GetResolvedSourceXmlPath(string annoAssetsPath)
    {
        string gameFolder = DetectedGameType.Equals("Anno117", StringComparison.OrdinalIgnoreCase)
            || (SelectedDetectedGame?.GameType.Equals("anno117", StringComparison.OrdinalIgnoreCase) ?? false)
                ? "Anno117"
                : "Anno1800";
        string sourceFolder = gameFolder.Equals("Anno117", StringComparison.OrdinalIgnoreCase)
            ? "source_xml_anno117"
            : "source_xml_anno1800";

        return Path.Combine(annoAssetsPath, gameFolder, sourceFolder);
    }
}
