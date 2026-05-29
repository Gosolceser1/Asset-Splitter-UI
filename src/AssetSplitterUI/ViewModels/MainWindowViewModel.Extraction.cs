using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    private StreamWriter? _rawConsoleLogWriter;

    private async Task StartExtractionAsync()
    {
        if (IsProcessing) return;

        if (!TryValidateAndInitRun(out var language))
            return;

        var runConfig = BuildRunConfig(language);
        var result = await _extractionCoordinator.RunAsync(
            runConfig,
            percent =>
            {
                var rounded = Math.Round(percent, 1);
                if (rounded != _lastDispatchedProgress)
                {
                    _lastDispatchedProgress = rounded;
                    Dispatcher.UIThread.Post(() => Progress = Math.Min(100, percent));
                }
            },
            outputLine =>
            {
                try { _rawConsoleLogWriter?.WriteLine(outputLine); }
                catch (IOException) { /* best effort raw capture */ }

                PostRunStatusUpdate(outputLine);

                if (DebugMode)
                {
                    var processed = DeveloperConsoleProcessor.Process(outputLine, debugMode: true);
                    if (!processed.SuppressDisplay)
                    {
                        var display = ConsoleOutputLocalizer.FilterLine(processed.Text, debugMode: true);
                        if (display != null)
                            _consoleOutput.EnqueueLine(LogLine.FromBackendOutput(outputLine, display));
                    }
                }
                else
                {
                    var filtered = ConsoleOutputLocalizer.FilterLine(outputLine);
                    if (filtered != null)
                        _consoleOutput.EnqueueLine(LogLine.FromBackendOutput(outputLine, filtered));
                }

                if (!string.IsNullOrWhiteSpace(outputLine))
                    _consoleOutput.EnqueueStatusLine(outputLine);
            });

        await _consoleOutput.StopAndFlushAsync();
        CloseRawConsoleLogWriter();
        _logStore.FlushCollapsedGroup();
        _logStore.DeveloperConsoleMode = false;
        await HandleExtractionResult(result, language);

        OnPropertyChanged(nameof(ShowPhase2Banner));
        OnPropertyChanged(nameof(ExtractButtonText));
        IsProcessing = false;
        ResetRunStatus();
        OnPropertyChanged(nameof(ShowStructuredRunStatus));
        OnPropertyChanged(nameof(ShowLegacyStatusLine));
        OnPropertyChanged(nameof(ShowDeveloperRawStatusMirror));
        OnPropertyChanged(nameof(ShowDeveloperStepPercent));
        OnPropertyChanged(nameof(ShowRunOperationCounts));
    }

    private bool TryValidateAndInitRun(out string language)
    {
        language = string.Empty;
        string? validationError = ExtractionRunResultAppender.ValidatePaths(GamePath, OutputPath);
        if (validationError is not null)
        {
            SetStatusTextLocalized(validationError);
            return false;
        }

        if (!TryNormalizeSingleGuid(out string normalizedSingleGuid))
            return false;

        IsProcessing = true;
        Progress = 0;
        IsComplete = false;
        ResetRunStatus();
        _logStore.Clear();
        _logStore.DeveloperConsoleMode = DebugMode;
        CloseRawConsoleLogWriter();
        if (DebugMode)
            TryOpenRawConsoleLogWriter();
        StatusIsError = false;
        Phase1Complete = false;
        OnPropertyChanged(nameof(ShowPhase2Banner));
        SetStatusTextLocalized("statusMessages.initializingExtraction");

        language = string.IsNullOrEmpty(SelectedLanguage) ? "none" : SelectedLanguage.ToLowerInvariant();
        _lastRunBackendAssetLanguage = language;
        _lastRunBackendSingleGuid = normalizedSingleGuid.Trim();
        bool singleGuidMode = !string.IsNullOrEmpty(normalizedSingleGuid);
        var annoAssetsPath = Path.GetFileName(Path.TrimEndingDirectorySeparator(OutputPath)).Equals("AnnoAssets", StringComparison.OrdinalIgnoreCase)
            ? OutputPath
            : Path.Combine(OutputPath, "AnnoAssets");
        ExtractionRunResultAppender.AppendRunHeader(
            _logStore, GamePath, OutputPath, language, normalizedSingleGuid,
            AddComments, FixDependencies, CreateTemplateFolders && !singleGuidMode,
            ModOpsWrap, IncludeDefaultProperties, SplitTemplates && !singleGuidMode, CreateAssetMods, DebugMode,
            resolvedAnnoAssetsPath: annoAssetsPath,
            uiLanguageLabel: DebugMode ? SelectedUILanguage : null);

        if (DebugMode)
            _logStore.AppendRaw("  AssetProcessor.exe " + BuildBackendArgs(GamePath, annoAssetsPath, language, normalizedSingleGuid));

        return true;
    }

    private void TryOpenRawConsoleLogWriter()
    {
        try
        {
            string logDir = Path.Combine(GetResolvedAnnoAssetsPath(), "logs");
            Directory.CreateDirectory(logDir);
            string logPath = Path.Combine(logDir, $"console_raw_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            _rawConsoleLogWriter = new StreamWriter(logPath, append: false, Encoding.UTF8) { AutoFlush = true };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UILogger.Debug(nameof(MainWindowViewModel), "Could not open raw console log: " + ex.Message);
        }
    }

    private void CloseRawConsoleLogWriter()
    {
        try { _rawConsoleLogWriter?.Dispose(); }
        catch (IOException) { /* ignore */ }
        _rawConsoleLogWriter = null;
    }

    private bool TryNormalizeSingleGuid(out string normalizedSingleGuid)
    {
        normalizedSingleGuid = SingleGuid.Trim();
        if (normalizedSingleGuid.Length == 0)
            return true;

        if (normalizedSingleGuid.All(char.IsDigit))
        {
            if (!SingleGuid.Equals(normalizedSingleGuid, StringComparison.Ordinal))
                SingleGuid = normalizedSingleGuid;

            return true;
        }

        SetStatusTextLocalized("statusMessages.singleGuidNumericOnly");
        StatusIsError = true;
        return false;
    }

    private AssetProcessorRunConfig BuildRunConfig(string language)
    {
        bool singleGuidMode = !string.IsNullOrWhiteSpace(SingleGuid);
        return new AssetProcessorRunConfig
        {
            GamePath = GamePath,
            OutputPath = OutputPath,
            Language = language,
            ConsoleLanguage = GetBackendConsoleLanguage(),
            SingleGuid = SingleGuid.Trim(),
            AddComments = AddComments,
            FixDependencies = FixDependencies,
            CreateTemplateFolders = CreateTemplateFolders && !singleGuidMode,
            ModOpsWrap = ModOpsWrap,
            IncludeDefaultProperties = IncludeDefaultProperties,
            SplitTemplates = SplitTemplates && !singleGuidMode,
            CreateAssetMods = CreateAssetMods,
            DebugMode = DebugMode
        };
    }

    private async Task HandleExtractionResult(ExtractionResult result, string language)
    {
        switch (result.Status)
        {
            case ExtractionResult.StatusKind.Success:
                Progress = 100;
                IsComplete = true;

                var wasPhase1Only = language.Equals("none", StringComparison.OrdinalIgnoreCase);
                await RefreshAvailableLanguagesAsync();
                CheckSourceFilesExist();

                if (wasPhase1Only && AvailableGameLanguages.Count > 0)
                {
                    Phase1Complete = true;
                    SetStatusTextLocalized("statusMessages.phase1Complete");
                    StatusIsError = false;
                    ExtractionRunResultAppender.AppendPhase1Complete(_logStore);
                }
                else
                {
                    Phase1Complete = false;
                    SetStatusTextLocalized("dialogs.extractionCompletedMsg");
                    StatusIsError = false;
                    ExtractionRunResultAppender.AppendPhase2Complete(
                        _logStore,
                        OutputPath,
                        GamePath,
                        SelectedDetectedGame?.GameType ?? DetectedGameType,
                        SingleGuid,
                        CreateAssetMods);

                    if (DebugMode)
                        ExtractionIssueSummaryAppender.AppendForDeveloperMode(_logStore, GetResolvedAnnoAssetsPath());
                    else
                        ExtractionIssueSummaryAppender.AppendIfAny(_logStore, GetResolvedAnnoAssetsPath());
                }
                break;

            case ExtractionResult.StatusKind.Cancelled:
                SetStatusTextLocalized("dialogs.extractionCancelled");
                ExtractionRunResultAppender.AppendCancelled(_logStore);
                break;

            case ExtractionResult.StatusKind.Error:
                SetStatusTextLocalized("consoleMessages.extractionFailed");
                StatusIsError = true;
                IsComplete = true;
                ExtractionRunResultAppender.AppendError(_logStore, result.ErrorMessage ?? "");
                break;
        }
    }

    /// <summary>Cancels the running extraction and kills the backend process launched by this UI instance.</summary>
    public void CancelExtraction()
    {
        _extractionCoordinator.Cancel();
        SetStatusTextLocalized("statusMessages.cancelling");
    }

    /// <summary>Sets StatusText from a localization key (with optional format args) and stores both so the status bar can be re-resolved on UI language change.</summary>
    private void SetStatusTextLocalized(string key, params ReadOnlySpan<object> args) =>
        _statusTextState.SetLocalized(key, args);

    /// <summary>Public alias for view code-behind to set status from a localization key.</summary>
    public void SetStatusText(string key) => SetStatusTextLocalized(key);

    /// <summary>Sets StatusText to a raw string (e.g. live backend output); clears the stored localization key so no re-resolution happens on language change.</summary>
    private void SetStatusTextRaw(string text) => _statusTextState.SetRaw(text);

    /// <summary>Opens the output folder in the system file manager if it exists on disk.</summary>
}
