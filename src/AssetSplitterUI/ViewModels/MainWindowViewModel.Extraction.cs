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
    private readonly Lock _rawConsoleLogLock = new();

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
        _lastRunIssueReportPath = null;

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
                        Dispatcher.UIThread.Post(() =>
                        {
                            // Ignore late progress posts after cancel/finish clears IsProcessing.
                            if (IsProcessing)
                            {
                                Progress = Math.Min(100, percent);
                            }
                        });
                    }
                },
                outputLine =>
                {
                    string? issueReportPath = ExtractionIssueSummaryLoader.TryGetReportPathFromMarker(outputLine);
                    if (issueReportPath is not null)
                    {
                        _lastRunIssueReportPath = issueReportPath;
                    }

                    try
                    {
                        lock (_rawConsoleLogLock)
                        {
                            _rawConsoleLogWriter?.WriteLine(outputLine);
                        }
                    }
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
                _logStore.FlushCollapsedGroup();
                _logStore.DeveloperConsoleMode = false;
            }
        });

        _rawConsoleLogPath = null;
    }

    /// <summary>Cancels the running extraction and kills the backend process launched by this UI instance.</summary>
    public void CancelExtraction()
    {
        _extractionCoordinator.Cancel();
        SetStatusTextLocalized("statusMessages.cancelling");
    }
}
