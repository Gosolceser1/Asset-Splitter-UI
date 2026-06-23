using System.Collections.ObjectModel;

namespace AssetSplitterUI.Services;

public class SettingsCoordinator(AppSettingsStore store)
{
    private const int CurrentSettingsVersion = 2;

    private PersistedAppSettings? _settings;
    private AppSettingsStore Store => store;

    public bool IsLoading { get; private set; }

    /// <summary>True when the user has not finished initial path setup; show blank first-run UI.</summary>
    public bool IsFirstRunExperience { get; private set; }

    public double SavedWindowWidth { get; private set; }
    public double SavedWindowHeight { get; private set; }
    public int SavedWindowLeft { get; private set; } = WindowPlacementSettings.UnsetCoordinate;
    public int SavedWindowTop { get; private set; } = WindowPlacementSettings.UnsetCoordinate;
    public bool SavedWindowMaximized { get; private set; }
    public string? SavedWindowDisplayName { get; private set; }

    /// <summary>Call before Save to persist the current window bounds.</summary>
    public void SetWindowPlacementForSave(WindowPlacementSettings placement)
    {
        _settings ??= new PersistedAppSettings();
        _settings.WindowDisplayName = placement.SavedDisplayName;

        if (placement.StartMaximized)
        {
            _settings.WindowMaximized = true;
            return;
        }

        _settings.WindowMaximized = false;
        _settings.WindowLeft = placement.SavedX;
        _settings.WindowTop = placement.SavedY;
        _settings.WindowWidth = placement.SavedWidth;
        _settings.WindowHeight = placement.SavedHeight;
    }

    /// <summary>Legacy size-only save.</summary>
    public void SetWindowSizeForSave(double width, double height)
    {
        _settings ??= new PersistedAppSettings();
        _settings.WindowWidth = width;
        _settings.WindowHeight = height;
    }

    /// <summary>Deserialises settings.json and populates the ViewModel properties via the provided setters.</summary>
    public void Load(
        Action<string> setGamePath,
        Action<string> setOutputPath,
        Action<bool> setAddComments,
        Action<bool> setFixDependencies,
        Action<bool> setCreateTemplateFolders,
        Action<bool> setModOpsWrap,
        Action<bool> setIncludeDefaultProperties,
        Action<bool> setSplitTemplates,
        Action<bool> setCreateAssetMods,
        Action<string> setUILanguage,
        Action<string> setTheme,
        ObservableCollection<string> recentGamePaths,
        ObservableCollection<string> recentOutputPaths,
        ObservableCollection<string> availableUILanguages,
        ObservableCollection<string> availableThemes)
    {
        IsLoading = true;
        _settings = Store.Load() ?? new PersistedAppSettings();

        if (_settings.SettingsVersion < CurrentSettingsVersion)
        {
            _settings.UserSetupComplete = false;
        }

        bool restoreSavedPreferences =
            _settings.SettingsVersion >= CurrentSettingsVersion && _settings.UserSetupComplete;

        IsFirstRunExperience = !restoreSavedPreferences;

        setUILanguage(ResolveUILanguage(availableUILanguages));
        string resolvedTheme = ResolveTheme(availableThemes);
        setTheme(resolvedTheme);
        ApplicationThemeService.ApplyTheme(resolvedTheme);

        recentGamePaths.Clear();
        recentOutputPaths.Clear();

        if (restoreSavedPreferences)
        {
            if (!string.IsNullOrEmpty(_settings.LastGamePath) && Directory.Exists(_settings.LastGamePath))
            {
                setGamePath(PathDisplayHelper.GetPathWithActualCasing(_settings.LastGamePath));
            }
            else
            {
                setGamePath("");
            }

            if (!string.IsNullOrEmpty(_settings.LastOutputPath))
            {
                string outPath = _settings.LastOutputPath.Trim();
                if (!File.Exists(outPath))
                {
                    setOutputPath(PathDisplayHelper.GetPathWithActualCasing(outPath));
                }
                else
                {
                    setOutputPath("");
                }
            }
            else
            {
                setOutputPath("");
            }

            setAddComments(_settings.AddComments);
            setFixDependencies(_settings.FixDependencies);
            setCreateTemplateFolders(_settings.CreateTemplateFolders);
            setModOpsWrap(ApplyModOpsWrapMigration());
            setIncludeDefaultProperties(ApplyIncludeDefaultPropertiesMigration());
            setSplitTemplates(_settings.SplitTemplates);
            setCreateAssetMods(_settings.CreateAssetMods);

            foreach (string path in _settings.RecentGamePaths ?? [])
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    recentGamePaths.Add(PathDisplayHelper.GetPathWithActualCasing(path));
                }
            }

            foreach (string path in _settings.RecentOutputPaths ?? [])
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    recentOutputPaths.Add(PathDisplayHelper.GetPathWithActualCasing(path));
                }
            }
        }
        else
        {
            setGamePath("");
            setOutputPath("");
            ApplyFirstRunDefaults(
                setAddComments,
                setFixDependencies,
                setCreateTemplateFolders,
                setModOpsWrap,
                setIncludeDefaultProperties,
                setSplitTemplates,
                setCreateAssetMods);

            SanitizeIncompleteSetupOnDisk();
        }

        LoadWindowSize();
        IsLoading = false;
    }

    private void SanitizeIncompleteSetupOnDisk()
    {
        if (_settings is null || (_settings.SettingsVersion >= CurrentSettingsVersion && _settings.UserSetupComplete))
        {
            return;
        }

        _settings.SettingsVersion = 0;
        _settings.UserSetupComplete = false;
        _settings.LastGamePath = "";
        _settings.LastOutputPath = "";
        _settings.RecentGamePaths = [];
        _settings.RecentOutputPaths = [];
        _settings.LastGameLanguage = "";
        ApplyFirstRunDefaultsToSettings(_settings);
        Store.Save(_settings);
    }

    private static void ApplyFirstRunDefaults(
        Action<bool> setAddComments,
        Action<bool> setFixDependencies,
        Action<bool> setCreateTemplateFolders,
        Action<bool> setModOpsWrap,
        Action<bool> setIncludeDefaultProperties,
        Action<bool> setSplitTemplates,
        Action<bool> setCreateAssetMods)
    {
        setAddComments(false);
        setFixDependencies(false);
        setCreateTemplateFolders(false);
        setModOpsWrap(false);
        setIncludeDefaultProperties(false);
        setSplitTemplates(false);
        setCreateAssetMods(false);
    }

    public string? LastGameLanguage => _settings?.LastGameLanguage;

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

        bool setupComplete = !string.IsNullOrWhiteSpace(gamePath) && !string.IsNullOrWhiteSpace(outputPath);
        _settings.UserSetupComplete = setupComplete;
        if (setupComplete)
        {
            _settings.SettingsVersion = CurrentSettingsVersion;
        }

        if (setupComplete)
        {
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
        else
        {
            _settings.LastGamePath = "";
            _settings.LastOutputPath = "";
            _settings.RecentGamePaths = [];
            _settings.RecentOutputPaths = [];
            _settings.LastGameLanguage = "";
            ApplyFirstRunDefaultsToSettings(_settings);
        }

        _settings.UILanguage = uiLanguage;
        _settings.ThemeMode = themeMode;
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

    public bool UserSetupComplete =>
        _settings is { SettingsVersion: >= CurrentSettingsVersion, UserSetupComplete: true };

    private bool ApplyModOpsWrapMigration()
    {
        if (_settings is null)
        {
            return false;
        }

        if (_settings.NoModOpsWrapLegacy.HasValue)
        {
            _settings.ModOpsWrap = !_settings.NoModOpsWrapLegacy.Value;
            _settings.NoModOpsWrapLegacy = null;
        }
        return _settings.ModOpsWrap;
    }

    private bool ApplyIncludeDefaultPropertiesMigration()
    {
        if (_settings is null)
        {
            return false;
        }

        if (!_settings.IncludeDefaultProperties.HasValue && _settings.NoDefaultPropertiesLegacy.HasValue)
        {
            _settings.IncludeDefaultProperties = !_settings.NoDefaultPropertiesLegacy.Value;
        }

        return _settings.IncludeDefaultProperties ?? false;
    }

    private string ResolveUILanguage(ObservableCollection<string> availableUILanguages)
    {
        if (_settings is null)
        {
            return "English";
        }

        if (!string.IsNullOrEmpty(_settings.UILanguage) && availableUILanguages.Contains(_settings.UILanguage))
        {
            return _settings.UILanguage;
        }

        if (!string.IsNullOrEmpty(_settings.UILanguage))
        {
            return availableUILanguages.Contains("English") ? "English" : availableUILanguages[0];
        }

        return "English";
    }

    private string ResolveTheme(ObservableCollection<string> availableThemes)
    {
        if (_settings is null)
        {
            return availableThemes.Contains("Auto") ? "Auto" : (availableThemes.Count > 0 ? availableThemes[0] : "Auto");
        }

        if (!string.IsNullOrEmpty(_settings.ThemeMode) && availableThemes.Contains(_settings.ThemeMode))
        {
            return _settings.ThemeMode;
        }

        return availableThemes.Contains("Auto") ? "Auto" : availableThemes.Count > 0 ? availableThemes[0] : "Auto";
    }

    private void LoadWindowSize()
    {
        if (_settings is null)
        {
            ClearSavedWindowPlacement();
            return;
        }

        SavedWindowMaximized = _settings.WindowMaximized;
        SavedWindowLeft = _settings.WindowLeft;
        SavedWindowTop = _settings.WindowTop;
        SavedWindowDisplayName = _settings.WindowDisplayName;

        if (_settings.WindowMaximized)
        {
            SavedWindowWidth = 0;
            SavedWindowHeight = 0;
            return;
        }

        if (_settings.WindowWidth >= WindowPlacementHelper.MinWidth
            && _settings.WindowHeight >= WindowPlacementHelper.MinHeight)
        {
            SavedWindowWidth = _settings.WindowWidth;
            SavedWindowHeight = _settings.WindowHeight;
        }
        else
        {
            SavedWindowWidth = 0;
            SavedWindowHeight = 0;
        }
    }

    private void ClearSavedWindowPlacement()
    {
        SavedWindowWidth = 0;
        SavedWindowHeight = 0;
        SavedWindowLeft = WindowPlacementSettings.UnsetCoordinate;
        SavedWindowTop = WindowPlacementSettings.UnsetCoordinate;
        SavedWindowMaximized = false;
        SavedWindowDisplayName = null;
    }
}
