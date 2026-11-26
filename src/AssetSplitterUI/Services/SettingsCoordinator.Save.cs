using System.Collections.ObjectModel;

namespace AssetSplitterUI.Services;

public partial class SettingsCoordinator
{
    public void Save(
        string gamePath,
        string outputPath,
        bool addComments,
        bool fixDependencies,
        bool createTemplateFolders,
        bool modOpsWrap,
        bool includeDefaultProperties,
        bool splitTemplates,
        bool createAssetMods,
        string uiLanguage,
        string themeMode,
        string lastGameLanguage,
        ObservableCollection<string> recentGamePaths,
        ObservableCollection<string> recentOutputPaths)
    {
        _settings ??= new PersistedAppSettings();

        bool pathsPresent = !string.IsNullOrWhiteSpace(gamePath) && !string.IsNullOrWhiteSpace(outputPath);
        bool hadCompletedSetup =
            _settings.UserSetupComplete && _settings.SettingsVersion >= CurrentSettingsVersion;

        // Theme / UI language always persist.
        _settings.UILanguage = uiLanguage;
        _settings.ThemeMode = themeMode;

        if (pathsPresent)
        {
            _settings.UserSetupComplete = true;
            _settings.SettingsVersion = CurrentSettingsVersion;
            _settings.LastGamePath = gamePath;
            _settings.LastOutputPath = outputPath;
            _settings.RecentGamePaths = MergeUnavailableRecentGamePaths(recentGamePaths);
            _settings.RecentOutputPaths = [.. recentOutputPaths];
            _settings.AddComments = addComments;
            _settings.FixDependencies = fixDependencies;
            _settings.CreateTemplateFolders = createTemplateFolders;
            _settings.ModOpsWrap = modOpsWrap;
            _settings.IncludeDefaultProperties = includeDefaultProperties;
            _settings.SplitTemplates = splitTemplates;
            _settings.CreateAssetMods = createAssetMods;
            _settings.LastGameLanguage = lastGameLanguage;
        }
        else if (hadCompletedSetup)
        {
            // Paths were cleared after a completed setup — keep preferences and recents.
            // Do not flip into first-run wipe (that used to reset every toggle).
            // A blank game path because the saved folder was offline at launch is kept as is.
            _settings.LastGamePath = string.IsNullOrWhiteSpace(gamePath) && _unavailableLastGamePath.Length > 0
                ? _unavailableLastGamePath
                : gamePath.Trim();
            _settings.LastOutputPath = outputPath.Trim();
            _settings.RecentGamePaths = MergeUnavailableRecentGamePaths(recentGamePaths);
            _settings.RecentOutputPaths = [.. recentOutputPaths];
            _settings.AddComments = addComments;
            _settings.FixDependencies = fixDependencies;
            _settings.CreateTemplateFolders = createTemplateFolders;
            _settings.ModOpsWrap = modOpsWrap;
            _settings.IncludeDefaultProperties = includeDefaultProperties;
            _settings.SplitTemplates = splitTemplates;
            _settings.CreateAssetMods = createAssetMods;
            _settings.LastGameLanguage = lastGameLanguage;
        }
        else
        {
            // Setup not finished yet: keep paths blank, but persist the toggles the user sees
            // (fresh first run = defaults; upgrade from v0/v1 = migrated flags). Writing first-run
            // defaults here wiped the migrated flags on the first close.
            _settings.UserSetupComplete = false;
            _settings.LastGamePath = "";
            _settings.LastOutputPath = "";
            _settings.RecentGamePaths = [];
            _settings.RecentOutputPaths = [];
            _settings.LastGameLanguage = "";
            _settings.AddComments = addComments;
            _settings.FixDependencies = fixDependencies;
            _settings.CreateTemplateFolders = createTemplateFolders;
            _settings.ModOpsWrap = modOpsWrap;
            _settings.IncludeDefaultProperties = includeDefaultProperties;
            _settings.SplitTemplates = splitTemplates;
            _settings.CreateAssetMods = createAssetMods;
        }

        Store.Save(_settings);
        IsFirstRunExperience = !(_settings.SettingsVersion >= CurrentSettingsVersion && _settings.UserSetupComplete);
    }

    private List<string> MergeUnavailableRecentGamePaths(IEnumerable<string> visibleRecentGamePaths)
    {
        List<string> merged = [.. visibleRecentGamePaths];
        foreach (string path in _unavailableRecentGamePaths)
        {
            if (merged.Count >= MaxPersistedRecentPaths)
            {
                break;
            }

            if (!merged.Any(existing => PathDisplayHelper.PathsEqual(existing, path)))
            {
                merged.Add(path);
            }
        }

        return merged;
    }

    private const int MaxPersistedRecentPaths = 10;

    private static void ApplyFirstRunDefaultsToSettings(PersistedAppSettings settings)
    {
        settings.AddComments = false;
        settings.FixDependencies = false;
        settings.CreateTemplateFolders = false;
        settings.ModOpsWrap = false;
        settings.IncludeDefaultProperties = false;
        settings.SplitTemplates = false;
        settings.CreateAssetMods = false;
    }
}
