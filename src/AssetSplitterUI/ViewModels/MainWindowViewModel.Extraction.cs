using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AssetProcessor;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    private StreamWriter? _rawConsoleLogWriter;
    private string? _rawConsoleLogPath;

    private async Task StartExtractionAsync()
    {
        if (IsProcessing)
        {
            return;
        }

        if (!TryValidateAndInitRun(out string? language))
        {
            return;
        }

        var runConfig = BuildEffectiveRunConfig(language);
        _lastRunWasSourceExtractionOnly = runConfig.SourceExtractionOnly
            || ProcessingRunPolicy.IsSourceExtractionOnly(BuildProcessingFlags(runConfig));
        _lastRunWasModExportOnly = ProcessingRunPolicy.IsModExportOnly(BuildProcessingFlags(runConfig));

        ExtractionResult result;
        try
        {
            result = await _extractionCoordinator.RunAsync(
                runConfig,
                percent =>
                {
                    double rounded = Math.Round(percent, 1);
                    if (rounded != _lastDispatchedProgress)
                    {
                        _lastDispatchedProgress = rounded;
                        Dispatcher.UIThread.Post(() => Progress = Math.Min(100, percent));
                    }
                },
                outputLine =>
                {
                    try { _rawConsoleLogWriter?.WriteLine(outputLine); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
                    {
                        /* best effort raw capture */
                    }

                    PostRunStatusUpdate(outputLine);

                    if (DebugMode)
                    {
                        var processed = DeveloperConsoleProcessor.Process(outputLine, debugMode: true);
                        if (!processed.SuppressDisplay)
                        {
                            string? display = ConsoleOutputLocalizer.FilterLine(processed.Text, debugMode: true);
                            if (display != null)
                            {
                                _consoleOutput.EnqueueLine(LogLine.FromBackendOutput(outputLine, display));
                            }
                        }
                    }
                    else
                    {
                        string? filtered = ConsoleOutputLocalizer.FilterLine(outputLine);
                        if (filtered != null)
                        {
                            _consoleOutput.EnqueueLine(LogLine.FromBackendOutput(outputLine, filtered));
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(outputLine))
                    {
                        _consoleOutput.EnqueueStatusLine(outputLine);
                    }
                });
        }
        catch (Exception ex)
        {
            UILogger.Warning(nameof(MainWindowViewModel), "Extraction run failed outside coordinator");
            UILogger.Debug(nameof(MainWindowViewModel), ex);
            result = new ExtractionResult { Status = ExtractionResult.StatusKind.Error, ErrorMessage = ex.Message };
        }
        finally
        {
            CloseRawConsoleLogWriter();
        }

        try
        {
            await _consoleOutput.FinishRunAsync(_rawConsoleLogPath);
        }
        catch (Exception ex)
        {
            UILogger.Warning(nameof(MainWindowViewModel), "Final console flush failed");
            UILogger.Debug(nameof(MainWindowViewModel), ex);
            result = new ExtractionResult { Status = ExtractionResult.StatusKind.Error, ErrorMessage = ex.Message };
        }

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            IsProcessing = false;
            try
            {
                await HandleExtractionResult(result, language);
            }
            catch (Exception ex)
            {
                UILogger.Warning(nameof(MainWindowViewModel), "Failed to finalize extraction result");
                UILogger.Debug(nameof(MainWindowViewModel), ex);
                SetStatusTextLocalized("consoleMessages.extractionFailed");
                StatusIsError = true;
                IsComplete = true;
                ExtractionRunResultAppender.AppendError(_logStore, ex.Message);
            }
            finally
            {
                ResetRunStatus();
                OnPropertyChanged(nameof(ExtractButtonText));
                OnPropertyChanged(nameof(ShowStructuredRunStatus));
                OnPropertyChanged(nameof(ShowLegacyStatusLine));
                OnPropertyChanged(nameof(ShowDeveloperRawStatusMirror));
                OnPropertyChanged(nameof(ShowDeveloperStepPercent));
                OnPropertyChanged(nameof(ShowRunOperationCounts));
            }
        });

        _logStore.FlushCollapsedGroup();
        _logStore.DeveloperConsoleMode = false;
        _rawConsoleLogPath = null;
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

        string normalizedSingleGuid;
        if (SourceFilesExist)
        {
            if (!TryNormalizeSingleGuid(out normalizedSingleGuid))
            {
                return false;
            }
        }
        else
        {
            normalizedSingleGuid = "";
        }

        if (SourceFilesExist
            && !GameLanguageRunPolicy.TryValidate(AddComments, HasGameLanguages, SelectedLanguage, out string? languageValidationKey))
        {
            SetStatusTextLocalized(languageValidationKey);
            StatusIsError = true;
            return false;
        }

        if (SourceFilesExist && AddComments)
        {
            string? fileBackedLanguage = FindAvailableGameLanguage(SelectedLanguage);
            if (fileBackedLanguage is null)
            {
                SetStatusTextLocalized("dialogs.selectLanguageForComments");
                StatusIsError = true;
                return false;
            }

            if (!fileBackedLanguage.Equals(SelectedLanguage, StringComparison.Ordinal))
            {
                SetGameLanguageSelection(fileBackedLanguage);
            }
        }

        IsProcessing = true;
        Progress = 0;
        IsComplete = false;
        ResetRunStatus();
        _logStore.Clear();
        _logStore.DeveloperConsoleMode = DebugMode;
        CloseRawConsoleLogWriter();
        if (DebugMode)
        {
            TryOpenRawConsoleLogWriter();
        }

        StatusIsError = false;
        Phase1Complete = false;
        SetStatusTextLocalized("statusMessages.initializingExtraction");

        bool initialSourceExtraction = !SourceFilesExist;
        language = initialSourceExtraction ? "none" : ResolveBackendLanguage();
        _lastRunBackendAssetLanguage = language;
        _lastRunBackendSingleGuid = normalizedSingleGuid.Trim();
        string annoAssetsPath = Path.GetFileName(Path.TrimEndingDirectorySeparator(OutputPath)).Equals("AnnoAssets", StringComparison.OrdinalIgnoreCase)
            ? OutputPath
            : Path.Combine(OutputPath, "AnnoAssets");
        var effectiveConfig = BuildEffectiveRunConfig(language);
        string? gameBuildLabel = GameBuildDetector
            .TryDetect(GamePath, SelectedDetectedGame?.GameType ?? DetectedGameType)
            ?.ToDisplayString();
        ExtractionRunResultAppender.AppendRunHeader(
            _logStore, GamePath, OutputPath, language, effectiveConfig.SingleGuid,
            effectiveConfig.AddComments, effectiveConfig.FixDependencies, effectiveConfig.CreateTemplateFolders,
            effectiveConfig.ModOpsWrap, effectiveConfig.IncludeDefaultProperties, effectiveConfig.SplitTemplates,
            effectiveConfig.CreateAssetMods, effectiveConfig.DebugMode,
            resolvedAnnoAssetsPath: annoAssetsPath,
            uiLanguageLabel: DebugMode ? SelectedUILanguage : null,
            gameBuildLabel: gameBuildLabel);

        if (DebugMode)
        {
            _logStore.AppendRaw("  AssetProcessor.exe " + BuildBackendArgs(GamePath, annoAssetsPath, language, effectiveConfig.SingleGuid));
        }

        return true;
    }

    private void TryOpenRawConsoleLogWriter()
    {
        try
        {
            string? logDir = OutputDirectoryManager.TryPrepareLogsDirectory(GetResolvedAnnoAssetsPath());
            if (logDir is null)
            {
                return;
            }

            string logPath = Path.Combine(logDir, $"console_raw_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            _rawConsoleLogPath = logPath;
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
        {
            return true;
        }

        if (normalizedSingleGuid.All(char.IsDigit))
        {
            if (!SingleGuid.Equals(normalizedSingleGuid, StringComparison.Ordinal))
            {
                SingleGuid = normalizedSingleGuid;
            }

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
            ConsoleLanguage = "english",
            ReadmeLanguage = GetBackendConsoleLanguage(),
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

                await RefreshAvailableLanguagesAsync();
                CheckSourceFilesExist();
                NotifyProcessingOptionsUiChanged();

                if (_lastRunWasSourceExtractionOnly)
                {
                    Phase1Complete = true;
                    OnPropertyChanged(nameof(ExtractButtonText));
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
                        CreateAssetMods,
                        _lastRunWasModExportOnly);

                    if (DebugMode)
                    {
                        ExtractionIssueSummaryAppender.AppendForDeveloperMode(_logStore, GetResolvedAnnoAssetsPath());
                    }
                    else
                    {
                        ExtractionIssueSummaryAppender.AppendIfAny(_logStore, GetResolvedAnnoAssetsPath());
                    }
                }
                break;

            case ExtractionResult.StatusKind.Cancelled:
                SetStatusTextLocalized("dialogs.extractionCancelled");
                ExtractionRunResultAppender.AppendCancelled(_logStore);
                OutputDirectoryManager.TryDeleteFixerScratchFile(GetResolvedAnnoAssetsPath());
                break;

            case ExtractionResult.StatusKind.Error:
                SetStatusTextLocalized("consoleMessages.extractionFailed");
                StatusIsError = true;
                IsComplete = true;
                ExtractionRunResultAppender.AppendError(_logStore, result.ErrorMessage ?? "");
                OutputDirectoryManager.TryDeleteFixerScratchFile(GetResolvedAnnoAssetsPath());
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

    /// <summary>Sets status from a localization key and leaves it refreshable when the UI language changes.</summary>
    public void SetLocalizedStatusText(string key) => SetStatusTextLocalized(key);

    /// <summary>Sets StatusText to a raw string (e.g. live backend output); clears the stored localization key so no re-resolution happens on language change.</summary>
    private void SetStatusTextRaw(string text) => _statusTextState.SetRaw(text);

    /// <summary>Opens the output folder in the system file manager if it exists on disk.</summary>
}
