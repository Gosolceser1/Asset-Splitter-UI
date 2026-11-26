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
    private async Task HandleExtractionResult(ExtractionResult result, string language)
    {
        switch (result.Status)
        {
            case ExtractionResult.StatusKind.Success:
                Progress = 100;
                IsComplete = true;

                await RefreshAvailableLanguagesAsync();
                CheckSourceFilesExist();
                SingleGuidAssetLookup.InvalidateCache();
                NotifyProcessingOptionsUiChanged();

                if (_lastRunWasSourceExtractionOnly)
                {
                    Phase1Complete = true;
                    OnPropertyChanged(nameof(ExtractButtonText));
                    SetStatusTextLocalized(
                        "statusMessages.phase1Complete",
                        new LocalizedConsoleArgument("buttons.processAssets"));
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
                        ExtractionIssueSummaryAppender.AppendForDeveloperMode(
                            _logStore,
                            GetResolvedAnnoAssetsPath(),
                            _lastRunIssueReportPath,
                            _lastRunStartedUtc);
                    }
                    else
                    {
                        ExtractionIssueSummaryAppender.AppendIfAny(
                            _logStore,
                            GetResolvedAnnoAssetsPath(),
                            _lastRunIssueReportPath,
                            _lastRunStartedUtc);
                    }
                }

                await PromptOpenOutputFolderAfterSuccessAsync();
                break;

            case ExtractionResult.StatusKind.Cancelled:
                Progress = 0;
                IsComplete = false;
                StatusIsError = false;
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
        lock (_rawConsoleLogLock)
        {
            try { _rawConsoleLogWriter?.Dispose(); }
            catch (IOException) { /* ignore */ }
            _rawConsoleLogWriter = null;
        }
    }
}
