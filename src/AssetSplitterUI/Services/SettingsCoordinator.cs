using System.Collections.ObjectModel;

namespace AssetSplitterUI.Services;

/// <summary>Loads and saves UI settings, applying migrations and first-run defaults.</summary>
public partial class SettingsCoordinator(AppSettingsStore store)
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

    public string? LastGameLanguage => _settings?.LastGameLanguage;

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

        bool upgradingFromLegacy = _settings.SettingsVersion < CurrentSettingsVersion;
        if (upgradingFromLegacy)
        {
            // Migrate option flags before any first-run wipe so v0/v1 prefs are not lost.
            _ = ApplyModOpsWrapMigration();
            _ = ApplyIncludeDefaultPropertiesMigration();
            _settings.NoDefaultPropertiesLegacy = null;
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
            if (upgradingFromLegacy)
            {
                // Keep migrated processing flags; only clear paths for first-run UX.
                setAddComments(_settings.AddComments);
                setFixDependencies(_settings.FixDependencies);
                setCreateTemplateFolders(_settings.CreateTemplateFolders);
                setModOpsWrap(_settings.ModOpsWrap);
                setIncludeDefaultProperties(_settings.IncludeDefaultProperties ?? false);
                setSplitTemplates(_settings.SplitTemplates);
                setCreateAssetMods(_settings.CreateAssetMods);
                SanitizeIncompleteSetupOnDisk(preserveProcessingFlags: true);
            }
            else
            {
                ApplyFirstRunDefaults(
                    setAddComments,
                    setFixDependencies,
                    setCreateTemplateFolders,
                    setModOpsWrap,
                    setIncludeDefaultProperties,
                    setSplitTemplates,
                    setCreateAssetMods);

                SanitizeIncompleteSetupOnDisk(preserveProcessingFlags: false);
            }
        }

        LoadWindowSize();
        IsLoading = false;
    }

    private void SanitizeIncompleteSetupOnDisk(bool preserveProcessingFlags = false)
    {
        if (_settings is null || (_settings.SettingsVersion >= CurrentSettingsVersion && _settings.UserSetupComplete))
        {
            return;
        }

        bool addComments = _settings.AddComments;
        bool fixDependencies = _settings.FixDependencies;
        bool createTemplateFolders = _settings.CreateTemplateFolders;
        bool modOpsWrap = _settings.ModOpsWrap;
        bool includeDefaultProperties = _settings.IncludeDefaultProperties ?? false;
        bool splitTemplates = _settings.SplitTemplates;
        bool createAssetMods = _settings.CreateAssetMods;

        _settings.SettingsVersion = CurrentSettingsVersion;
        _settings.UserSetupComplete = false;
        _settings.LastGamePath = "";
        _settings.LastOutputPath = "";
        _settings.RecentGamePaths = [];
        _settings.RecentOutputPaths = [];
        _settings.LastGameLanguage = "";
        if (preserveProcessingFlags)
        {
            _settings.AddComments = addComments;
            _settings.FixDependencies = fixDependencies;
            _settings.CreateTemplateFolders = createTemplateFolders;
            _settings.ModOpsWrap = modOpsWrap;
            _settings.IncludeDefaultProperties = includeDefaultProperties;
            _settings.SplitTemplates = splitTemplates;
            _settings.CreateAssetMods = createAssetMods;
            _settings.NoModOpsWrapLegacy = null;
            _settings.NoDefaultPropertiesLegacy = null;
        }
        else
        {
            ApplyFirstRunDefaultsToSettings(_settings);
        }

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
}
