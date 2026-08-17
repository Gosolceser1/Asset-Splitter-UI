using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

public partial class MainWindowViewModel
{
    /// <summary>Scans for Anno installations via PlatformServices, populates DetectedGames, updates DetectStatusText, and selects the preferred match.</summary>
    private async Task DetectGamesAsync()
    {
        if (IsProcessing)
        {
            return;
        }

        await RefreshDetectedGamesAsync(showStatus: true);
    }

    private async Task RefreshDetectedGamesAsync(bool showStatus)
    {
        // Detection feedback goes to DetectStatusText (inline, next to the Auto-Detect button).
        if (showStatus)
        {
            _detectStatusKey = "statusMessages.searchingForAnnoInstallations";
            _detectStatusFormatArgs = null;
            DetectStatusText = StringResourceManager.Instance.GetString(_detectStatusKey);
        }

        var games = await _platformServices.DetectGameInstallationsAsync();

        DetectedGames.Clear();
        foreach (var game in games)
        {
            DetectedGames.Add(game);
        }

        // Notify the UI that HasDetectedGames may have changed.
        OnPropertyChanged(nameof(HasDetectedGames));

        if (games.Count > 0)
        {
            var preferred = games.FirstOrDefault(g =>
                PathDisplayHelper.PathsEqual(g.Path, GamePath));

            // Only auto-fill game path when the user explicitly ran Auto-Detect and nothing is set yet.
            if (preferred is not null)
            {
                ApplyRecognizedGameInstallation(preferred);
            }
            else if (string.IsNullOrWhiteSpace(GamePath))
            {
                ApplyRecognizedGameInstallation(games[0]);
            }
            else
            {
                SelectedDetectedGame = null;
            }

            try
            {
                if (games.Count == 1)
                {
                    if (showStatus)
                    {
                        var game = preferred ?? games[0];
                        _detectStatusKey = "statusMessages.foundAnnoInstallation";
                        _detectStatusFormatArgs = [GetLocalizedGameNameArgument(game)];
                        DetectStatusText = ResolveLocalizedText(_detectStatusKey, _detectStatusFormatArgs);
                    }
                }
                else
                {
                    if (showStatus)
                    {
                        _detectStatusKey = "statusMessages.foundMultipleAnnoInstallations";
                        _detectStatusFormatArgs = [games.Count];
                        DetectStatusText = ResolveLocalizedText(_detectStatusKey, _detectStatusFormatArgs);
                    }
                }
            }
            catch (FormatException)
            {
                if (showStatus)
                {
                    _detectStatusKey = "statusMessages.foundMultipleAnnoInstallations";
                    _detectStatusFormatArgs = [games.Count];
                    DetectStatusText = StringResourceManager.Instance.GetString(_detectStatusKey);
                }
            }

            await RefreshAvailableLanguagesAsync();
        }
        else
        {
            SelectedDetectedGame = null;
            if (showStatus)
            {
                _detectStatusKey = "statusMessages.noAnnoInstallationsFound";
                _detectStatusFormatArgs = null;
                DetectStatusText = StringResourceManager.Instance.GetString(_detectStatusKey);
            }
        }
    }

    private void ScheduleManualGamePathValidation()
    {
        if (_updatingGamePathFromSelector)
        {
            return;
        }

        _gamePathValidationCts?.Cancel();
        _gamePathValidationCts = new CancellationTokenSource();
        CancellationToken token = _gamePathValidationCts.Token;
        _ = ValidateManualGamePathDebouncedAsync(token);
    }

    private async Task ValidateManualGamePathDebouncedAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(400, token);
            await ValidateManualGamePathAsync(token);
        }
        catch (OperationCanceledException)
        {
            /* superseded by a newer edit */
        }
    }

    private Task ValidateManualGamePathAsync(CancellationToken token = default) =>
        ValidateManualGamePathCoreAsync(token);

    private async Task ValidateManualGamePathCoreAsync(CancellationToken token)
    {
        string path = GamePath.Trim();
        if (string.IsNullOrEmpty(path))
        {
            await Dispatcher.UIThread.InvokeAsync(ClearManualGamePathRecognition);
            return;
        }

        IReadOnlyList<GameInstallation> matches = await Task.Run(
            () => _platformServices.RecognizeGameInstallations(path),
            token);

        if (token.IsCancellationRequested)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            ApplyManualGamePathRecognition(path, matches);
        });
    }

    private void ApplyManualGamePathRecognition(string requestedPath, IReadOnlyList<GameInstallation> matches)
    {
        if (!PathDisplayHelper.PathsEqual(requestedPath, GamePath))
        {
            return;
        }

        OnPropertyChanged(nameof(HasDetectedGames));

        if (matches.Count == 1)
        {
            ApplyRecognizedGameInstallation(matches[0]);
            return;
        }

        if (matches.Count > 1)
        {
            DetectedGames.Clear();
            foreach (GameInstallation game in matches)
            {
                DetectedGames.Add(game);
            }

            SelectedDetectedGame = null;
            IsGamePathRecognized = false;
            SetDetectStatus("statusMessages.selectGameSubfolder");
            return;
        }

        SelectedDetectedGame = null;
        IsGamePathRecognized = false;
        SetDetectStatus("statusMessages.invalidGameDirectory");
    }

    private void ApplyRecognizedGameInstallation(GameInstallation game)
    {
        string path = PathDisplayHelper.GetPathWithActualCasing(game.Path);

        if (!DetectedGames.Any(existing => PathDisplayHelper.PathsEqual(existing.Path, path)))
        {
            DetectedGames.Add(game);
        }

        _updatingGamePathFromSelector = true;
        try
        {
            if (!PathDisplayHelper.PathsEqual(path, GamePath))
            {
                GamePath = path;
            }

            SelectedDetectedGame = game;
        }
        finally
        {
            _updatingGamePathFromSelector = false;
        }

        PathDisplayHelper.AddToRecentPaths(RecentGamePaths, path);
        IsGamePathRecognized = true;
        SetDetectStatus("statusMessages.foundAnnoInstallation", GetLocalizedGameNameArgument(game));
    }

    private void ClearManualGamePathRecognition()
    {
        IsGamePathRecognized = false;
        _detectStatusKey = null;
        _detectStatusFormatArgs = null;
        DetectStatusText = "";
        OnPropertyChanged(nameof(HasDetectStatus));
    }

    private void SetDetectStatus(string key, params object[] args)
    {
        _detectStatusKey = key;
        _detectStatusFormatArgs = args.Length > 0 ? args : null;
        try
        {
            DetectStatusText = ResolveLocalizedText(key, _detectStatusFormatArgs);
        }
        catch (FormatException)
        {
            DetectStatusText = StringResourceManager.Instance.GetString(key);
        }

        OnPropertyChanged(nameof(HasDetectStatus));
    }

    private static LocalizedConsoleArgument GetLocalizedGameNameArgument(GameInstallation game) =>
        new(game.DisplayName);

    /// <summary>
    /// Checks if source XML files already exist and detects which game type
    /// </summary>
    private void CheckSourceFilesExist()
    {
        DetectedGameType = ExtractedAssetSourceLocator.DetectGameType(OutputPath, GamePath);
        // Only clear GUID when sources are actually gone — not when CanUseSingleGuid is
        // false solely because a run is in progress (!IsProcessing is part of that gate).
        if (!SourceFilesExist)
        {
            _singleGuidLookupCts?.Cancel();
            SingleGuid = "";
            SingleGuidStatusText = "";
        }
    }
}
