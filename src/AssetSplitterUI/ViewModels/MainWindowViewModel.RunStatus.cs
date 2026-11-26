using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    private readonly ConsoleRunStatusPresenter _runStatus = new();

    /// <summary>Backend lines waiting to be fed to <see cref="_runStatus"/> on the UI thread.</summary>
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _pendingRunStatusLines = new();

    /// <summary>1 while a drain of <see cref="_pendingRunStatusLines"/> is queued on the dispatcher.</summary>
    private int _runStatusDrainScheduled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStructuredRunStatus))]
    [NotifyPropertyChangedFor(nameof(ShowLegacyStatusLine))]
    [NotifyPropertyChangedFor(nameof(ShowDeveloperRawStatusMirror))]
    [NotifyPropertyChangedFor(nameof(ShowDeveloperStepPercent))]
    [NotifyPropertyChangedFor(nameof(ShowRunOperationCounts))]
    public partial bool RunHasLiveDetail { get; set; }

    [ObservableProperty]
    public partial string RunPhaseTitle { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRunOperationCounts))]
    public partial string RunOperationCounts { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDeveloperStepPercent))]
    public partial string RunStepPercentText { get; set; } = "";

    [ObservableProperty]
    public partial string RunStatusOperation { get; set; } = "";

    [ObservableProperty]
    public partial string RunStatusGuid { get; set; } = "";

    [ObservableProperty]
    public partial string RunStatusAssetName { get; set; } = "";

    [ObservableProperty]
    public partial string RunStatusTemplate { get; set; } = "";

    [ObservableProperty]
    public partial string RunStatusGuidSeparator { get; set; } = "";

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
        _pendingRunStatusLines.Clear();
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

    private void ApplyRunStatusProperties()
    {
        RunPhaseTitle = _runStatus.PhaseTitle;
        RunOperationCounts = _runStatus.OperationCounts;
        RunStepPercentText = string.IsNullOrEmpty(_runStatus.StepPercent)
            ? ""
            : $"{_runStatus.StepPercent}{StringResourceManager.Instance.GetString("statusMessages.stepSuffix")}";
        RunStatusOperation = _runStatus.Operation;
        RunStatusGuid = _runStatus.Guid;
        RunStatusAssetName = _runStatus.AssetName;
        RunStatusTemplate = _runStatus.TemplateDetail;
        RunStatusGuidSeparator = _runStatus.GuidSeparator;
        RunHasLiveDetail = _runStatus.HasLiveDetail;
    }

    /// <summary>
    /// Queues a backend line for the run-status header. Lines are fed in order, but at most one
    /// dispatcher post is pending at a time and the bound properties are updated once per drain;
    /// posting one UI callback per stdout line flooded the dispatcher on verbose runs.
    /// </summary>
    private void PostRunStatusUpdate(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        _pendingRunStatusLines.Enqueue(line);
        if (Interlocked.Exchange(ref _runStatusDrainScheduled, 1) == 0)
        {
            Dispatcher.UIThread.Post(DrainRunStatusLines, DispatcherPriority.Background);
        }
    }

    private void DrainRunStatusLines()
    {
        Interlocked.Exchange(ref _runStatusDrainScheduled, 0);
        bool fedAny = false;
        while (_pendingRunStatusLines.TryDequeue(out string? line))
        {
            _runStatus.Feed(line);
            fedAny = true;
        }

        if (fedAny)
        {
            ApplyRunStatusProperties();
        }
    }

    private void RefreshLocalizedLogLinesInternal() => _logStore.RefreshLocalizedLines();
}
