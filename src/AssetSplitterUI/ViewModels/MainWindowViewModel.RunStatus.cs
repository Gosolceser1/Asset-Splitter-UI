using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    private readonly ConsoleRunStatusPresenter _runStatus = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStructuredRunStatus))]
    [NotifyPropertyChangedFor(nameof(ShowLegacyStatusLine))]
    [NotifyPropertyChangedFor(nameof(ShowDeveloperRawStatusMirror))]
    [NotifyPropertyChangedFor(nameof(ShowDeveloperStepPercent))]
    [NotifyPropertyChangedFor(nameof(ShowRunOperationCounts))]
    private bool _runHasLiveDetail;

    [ObservableProperty]
    private string _runPhaseTitle = "";

    [ObservableProperty]
    private string _runOperationCounts = "";

    [ObservableProperty]
    private string _runStepPercentText = "";

    [ObservableProperty]
    private string _runStatusOperation = "";

    [ObservableProperty]
    private string _runStatusGuid = "";

    [ObservableProperty]
    private string _runStatusAssetName = "";

    [ObservableProperty]
    private string _runStatusTemplate = "";

    [ObservableProperty]
    private string _runStatusGuidSeparator = "";

    /// <summary>Structured header while a per-item progress line is active (regular + developer).</summary>
    public bool ShowStructuredRunStatus => IsProcessing && RunHasLiveDetail;

    /// <summary>Step-local % from the backend progress line (developer only).</summary>
    public bool ShowDeveloperStepPercent =>
        DebugMode && ShowStructuredRunStatus && RunStepPercentText.Length > 0;

    /// <summary>Full progress line mirror under the structured header (developer only).</summary>
    public bool ShowDeveloperRawStatusMirror => DebugMode && ShowStructuredRunStatus;

    /// <summary>Counts for the current operation (regular + developer).</summary>
    public bool ShowRunOperationCounts => ShowStructuredRunStatus && RunOperationCounts.Length > 0;

    /// <summary>Idle, initializing, or waiting for first progress line.</summary>
    public bool ShowLegacyStatusLine => !ShowStructuredRunStatus;

    private void ResetRunStatus()
    {
        _runStatus.Reset();
        RunHasLiveDetail = false;
        RunPhaseTitle = "";
        RunOperationCounts = "";
        RunStepPercentText = "";
        RunStatusOperation = "";
        RunStatusGuid = "";
        RunStatusAssetName = "";
        RunStatusTemplate = "";
        RunStatusGuidSeparator = "";
    }

    private void UpdateRunStatusFromLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        _runStatus.Feed(line);
        RunPhaseTitle = _runStatus.PhaseTitle;
        RunOperationCounts = _runStatus.OperationCounts;
        RunStepPercentText = string.IsNullOrEmpty(_runStatus.StepPercent) ? "" : $"{_runStatus.StepPercent} step";
        RunStatusOperation = _runStatus.Operation;
        RunStatusGuid = _runStatus.Guid;
        RunStatusAssetName = _runStatus.AssetName;
        RunStatusTemplate = _runStatus.TemplateDetail;
        RunStatusGuidSeparator = _runStatus.GuidSeparator;
        RunHasLiveDetail = _runStatus.HasLiveDetail;
    }

    private void PostRunStatusUpdate(string? line) =>
        Dispatcher.UIThread.Post(() => UpdateRunStatusFromLine(line));
}
