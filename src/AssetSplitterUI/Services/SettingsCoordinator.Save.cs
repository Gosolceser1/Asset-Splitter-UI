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
            _settings.RecentGamePaths = [.. recentGamePaths];
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
            _settings.LastGamePath = gamePath.Trim();
            _settings.LastOutputPath = outputPath.Trim();
            _settings.RecentGamePaths = [.. recentGamePaths];
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
            _settings.UserSetupComplete = false;
            _settings.LastGamePath = "";
            _settings.LastOutputPath = "";
            _settings.RecentGamePaths = [];
            _settings.RecentOutputPaths = [];
            _settings.LastGameLanguage = "";
            ApplyFirstRunDefaultsToSettings(_settings);
        }

        Store.Save(_settings);
        IsFirstRunExperience = !(_settings.SettingsVersion >= CurrentSettingsVersion && _settings.UserSetupComplete);
    }

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
