using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    public void LoadSettings()
    {
        _settingsCoordinator.Load(
            setGamePath: value => GamePath = value,
            setOutputPath: value => OutputPath = value,
            setAddComments: value => AddComments = value,
            setFixDependencies: value => FixDependencies = value,
            setCreateTemplateFolders: value => CreateTemplateFolders = value,
            setModOpsWrap: value => ModOpsWrap = value,
            setIncludeDefaultProperties: value => IncludeDefaultProperties = value,
            setSplitTemplates: value => SplitTemplates = value,
            setCreateAssetMods: value => CreateAssetMods = value,
            setUILanguage: value => SelectedUILanguage = value,
            setTheme: value => SelectedTheme = value,
            RecentGamePaths,
            RecentOutputPaths,
            AvailableUILanguages,
            AvailableThemes);

        SavedWindowWidth = _settingsCoordinator.SavedWindowWidth;
        SavedWindowHeight = _settingsCoordinator.SavedWindowHeight;
        SavedWindowLeft = _settingsCoordinator.SavedWindowLeft;
        SavedWindowTop = _settingsCoordinator.SavedWindowTop;
        SavedWindowMaximized = _settingsCoordinator.SavedWindowMaximized;
        SavedWindowDisplayName = _settingsCoordinator.SavedWindowDisplayName;

        _ = SchedulePostLoadSettingsAsync();
    }

    private async Task SchedulePostLoadSettingsAsync()
    {
        try
        {
            await Task.Delay(100);

            if (_settingsCoordinator.IsFirstRunExperience)
            {
                DetectedGames.Clear();
                SelectedDetectedGame = null;
                _detectStatusKey = null;
                _detectStatusFormatArgs = null;
                DetectStatusText = "";
                OnPropertyChanged(nameof(HasDetectedGames));
                OnPropertyChanged(nameof(HasDetectStatus));
            }

            await Dispatcher.UIThread.InvokeAsync(CheckSourceFilesExist);
            await RefreshAvailableLanguagesAsync();

            if (!string.IsNullOrWhiteSpace(GamePath))
            {
                await ValidateManualGamePathAsync();
            }

            if (!_settingsCoordinator.IsFirstRunExperience)
            {
                string? lastLang = _settingsCoordinator.LastGameLanguage;
                if (AvailableGameLanguages.Count > 0)
                {
                    SetGameLanguageSelection(
                        FindAvailableGameLanguage(lastLang)
                        ?? GetDefaultGameLanguageSelection());
                }
                else if (AvailableGameLanguages.Count == 0)
                {
                    SelectedLanguage = "";
                }
            }
            else
            {
                SelectedLanguage = "";
            }

            NotifyFirstRunStepHintsChanged();
        }
        catch (Exception ex)
        {
            UILogger.Debug(nameof(MainWindowViewModel), ex);
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        ApplyTheme(value);
        SaveSettingsIfReady(); // Don't save during LoadSettings (would write incomplete state).
        if (!_logStore.IsShutdown && LogLines.Count > 0)
            Dispatcher.UIThread.Post(_logStore.RefreshColors, DispatcherPriority.Background);
    }

    partial void OnAddCommentsChanged(bool value)
    {
        if (value && HasGameLanguages && string.IsNullOrWhiteSpace(SelectedLanguage))
            SetGameLanguageSelection(GetDefaultGameLanguageSelection());

        NotifyGameLanguageUiChanged();
        SaveSettingsIfReady();
    }
    partial void OnFixDependenciesChanged(bool value) => SaveSettingsIfReady();
    partial void OnCreateTemplateFoldersChanged(bool value) => SaveSettingsIfReady();
    partial void OnModOpsWrapChanged(bool value) => SaveSettingsIfReady();
    partial void OnIncludeDefaultPropertiesChanged(bool value) => SaveSettingsIfReady();
    partial void OnSplitTemplatesChanged(bool value) => SaveSettingsIfReady();
    partial void OnCreateAssetModsChanged(bool value) => SaveSettingsIfReady();

    /// <summary>Saves settings only when a full load is not in progress.</summary>
    private void SaveSettingsIfReady() { if (!_settingsCoordinator.IsLoading)
        {
            SaveSettings();
        }
    }

    /// <summary>Applies Light/Auto/Dark theme to the application.</summary>
    /// <param name="themeMode">"Light", "Dark", or "Auto" (follow system).</param>
    public static void ApplyTheme(string themeMode) => ApplicationThemeService.ApplyTheme(themeMode);

    partial void OnSelectedLanguageChanged(string value)
    {
        ScheduleSingleGuidLookup();
        SaveSettingsIfReady();
    }

    partial void OnSingleGuidChanged(string value)
    {
        ScheduleSingleGuidLookup();
    }

    partial void OnSelectedUILanguageChanged(string value)
    {
        StringResourceManager.Instance.CurrentLanguage = value;
        // Computed localized property (not a {loc:Localize} binding) must be refreshed explicitly.
        OnPropertyChanged(nameof(ExtractButtonText));
        // Re-evaluate live single-GUID hint text in the newly selected language.
        ScheduleSingleGuidLookup();
        _statusTextState.Refresh();
        SaveSettingsIfReady(); // Persist UI language preference.
    }

    partial void OnGamePathChanged(string value)
    {
        if (!_updatingGamePathFromSelector)
            IsGamePathRecognized = false;

        CheckSourceFilesExist();
        ScheduleSingleGuidLookup();
        _ = RefreshAvailableLanguagesAsync();
        ScheduleManualGamePathValidation();
        StartExtractionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanStartExtraction));
        NotifyFirstRunStepHintsChanged();
    }

    /// <summary>
    /// When the user picks a different game from the detected-games selector,
    /// update GamePath, recent paths, refresh source files + languages. Save the
    /// current game's console state and restore the selected game's state (or clear if none).
    /// </summary>
    partial void OnSelectedDetectedGameChanged(GameInstallation? value)
    {
        if (value is null) return;

        // Do not apply game switch while a run is in progress (dropdown is disabled, but guard for safety).
        if (IsProcessing) return;

        _consoleStateStore.Save(GamePath, CaptureConsoleState());

        var path = PathDisplayHelper.GetPathWithActualCasing(value.Path);
        _updatingGamePathFromSelector = true;
        try
        {
            GamePath = path;
        }
        finally
        {
            _updatingGamePathFromSelector = false;
        }

        PathDisplayHelper.AddToRecentPaths(RecentGamePaths, path);
        IsGamePathRecognized = true;
        SetDetectStatus("statusMessages.foundAnnoInstallation", GetLocalizedGameNameArgument(value));
        CheckSourceFilesExist();
        ScheduleSingleGuidLookup();
        _ = RefreshAvailableLanguagesAsync();

        if (_consoleStateStore.TryGet(path, out var state))
            RestoreConsoleState(state);
        else
            ResetConsoleAndRunState();
    }

    /// <summary>Captures current log, progress, phase and status into a snapshot.</summary>
    private GameConsoleState CaptureConsoleState() =>
        new()
        {
            LogLines = [.. _logStore.Snapshot()],
            Progress = Progress,
            IsComplete = IsComplete,
            Phase1Complete = Phase1Complete,
            StatusText = StatusText,
            StatusIsError = StatusIsError,
            StatusTextKey = _statusTextState.Key,
            StatusTextArgs = _statusTextState.Args
        };

    /// <summary>Restores log, progress, phase and status from a snapshot.</summary>
    private void RestoreConsoleState(GameConsoleState state)
    {
        if (IsProcessing)
        {
            return;
        }

        _logStore.Restore(state.LogLines);
        _logStore.RefreshLocalizedLines();
        Progress = state.Progress;
        IsComplete = state.IsComplete;
        Phase1Complete = state.Phase1Complete;
        StatusIsError = state.StatusIsError;
        _statusTextState.Restore(state.StatusText, state.StatusTextKey, state.StatusTextArgs);

        OnPropertyChanged(nameof(ExtractButtonText));
        OnPropertyChanged(nameof(CanUseSingleGuid));
    }

    /// <summary>Clears the console log and resets progress/phase/status so the UI reflects "no run for current selection".</summary>
    private void ResetConsoleAndRunState()
    {
        if (IsProcessing)
        {
            return;
        }

        _logStore.Clear();
        Progress = 0;
        IsComplete = false;
        Phase1Complete = false;
        StatusIsError = false;
        SetStatusTextLocalized("console.ready");
        OnPropertyChanged(nameof(ExtractButtonText));
        OnPropertyChanged(nameof(CanUseSingleGuid));
    }

    partial void OnOutputPathChanged(string value)
    {
        CheckSourceFilesExist();
        ScheduleSingleGuidLookup();
        _ = RefreshAvailableLanguagesAsync();
        OnPropertyChanged(nameof(ExtractButtonText));
        StartExtractionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanStartExtraction));
        NotifyFirstRunStepHintsChanged();
    }

    partial void OnIsProcessingChanged(bool value)
    {
        StartExtractionCommand.NotifyCanExecuteChanged();
        ((RelayCommand)CancelExtractionCommand).NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanStartExtraction));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(AreBroadOutputOptionsEnabled));
        OnPropertyChanged(nameof(AreProcessingOptionsEnabled));
        OnPropertyChanged(nameof(ExtractionOptionsToolTipText));
        OnPropertyChanged(nameof(CanUseSingleGuid));
        NotifyFirstRunStepHintsChanged();

        if (value)
        {
            _busyAnimator.Start();
            _lastDispatchedProgress = -1;
            _consoleOutput.Start();
        }
        else
        {
            _busyAnimator.Stop();
            _consoleOutput.Stop();
        }
    }

}
