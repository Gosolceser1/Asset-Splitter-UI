using System.Collections.ObjectModel;

namespace AssetSplitterUI.Services;

public class SettingsCoordinator(AppSettingsStore store)
{
    private PersistedAppSettings? _settings;
    private AppSettingsStore Store => store;

    public bool IsLoading { get; private set; }

    public double SavedWindowWidth { get; private set; }
    public double SavedWindowHeight { get; private set; }

    /// <summary>Call before Save to persist the current window dimensions.</summary>
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

        if (!string.IsNullOrEmpty(_settings.LastGamePath) && Directory.Exists(_settings.LastGamePath))
            setGamePath(PathDisplayHelper.GetPathWithActualCasing(_settings.LastGamePath));

        if (!string.IsNullOrEmpty(_settings.LastOutputPath))
        {
            string outPath = _settings.LastOutputPath.Trim();
            if (!File.Exists(outPath))
                setOutputPath(PathDisplayHelper.GetPathWithActualCasing(outPath));
        }

        setAddComments(_settings.AddComments);
        setFixDependencies(_settings.FixDependencies);
        setCreateTemplateFolders(_settings.CreateTemplateFolders);
        setModOpsWrap(ApplyModOpsWrapMigration());
        setIncludeDefaultProperties(ApplyIncludeDefaultPropertiesMigration());
        setSplitTemplates(_settings.SplitTemplates);
        setCreateAssetMods(_settings.CreateAssetMods);

        setUILanguage(ResolveUILanguage(availableUILanguages));
        string resolvedTheme = ResolveTheme(availableThemes);
        setTheme(resolvedTheme);
        ApplicationThemeService.ApplyTheme(resolvedTheme);

        foreach (string path in _settings.RecentGamePaths ?? [])
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                recentGamePaths.Add(PathDisplayHelper.GetPathWithActualCasing(path));
        }

        foreach (string path in _settings.RecentOutputPaths ?? [])
        {
            if (!string.IsNullOrWhiteSpace(path))
                recentOutputPaths.Add(PathDisplayHelper.GetPathWithActualCasing(path));
        }

        LoadWindowSize();
        IsLoading = false;
    }

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
        _settings.LastGamePath = gamePath;
        _settings.LastOutputPath = outputPath;
        _settings.AddComments = addComments;
        _settings.FixDependencies = fixDependencies;
        _settings.CreateTemplateFolders = createTemplateFolders;
        _settings.ModOpsWrap = modOpsWrap;
        _settings.IncludeDefaultProperties = includeDefaultProperties;
        _settings.SplitTemplates = splitTemplates;
        _settings.CreateAssetMods = createAssetMods;
        _settings.UILanguage = uiLanguage;
        _settings.ThemeMode = themeMode;
        _settings.LastGameLanguage = lastGameLanguage;
        _settings.RecentGamePaths = [.. recentGamePaths];
        _settings.RecentOutputPaths = [.. recentOutputPaths];
        Store.Save(_settings);
    }

    public string? LastGameLanguage => _settings?.LastGameLanguage;

    private bool ApplyModOpsWrapMigration()
    {
        if (_settings is null)
            return false;

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
            return true;

        if (!_settings.IncludeDefaultProperties.HasValue && _settings.NoDefaultPropertiesLegacy.HasValue)
            _settings.IncludeDefaultProperties = !_settings.NoDefaultPropertiesLegacy.Value;
        return _settings.IncludeDefaultProperties ?? true;
    }

    private string ResolveUILanguage(ObservableCollection<string> availableUILanguages)
    {
        if (_settings is null)
            return "English";

        if (!string.IsNullOrEmpty(_settings.UILanguage) && availableUILanguages.Contains(_settings.UILanguage))
            return _settings.UILanguage;
        if (!string.IsNullOrEmpty(_settings.UILanguage))
            return availableUILanguages.Contains("English") ? "English" : availableUILanguages[0];
        return "English";
    }

    private string ResolveTheme(ObservableCollection<string> availableThemes)
    {
        if (_settings is null)
            return availableThemes.Contains("Auto") ? "Auto" : (availableThemes.Count > 0 ? availableThemes[0] : "Auto");

        if (!string.IsNullOrEmpty(_settings.ThemeMode) && availableThemes.Contains(_settings.ThemeMode))
        {
            return _settings.ThemeMode;
        }

        return availableThemes.Contains("Auto") ? "Auto" : availableThemes.Count > 0 ? availableThemes[0] : "Auto";
    }

    private void LoadWindowSize()
    {
        const double minW = 840, minH = 850;
        if (_settings is not null && _settings.WindowWidth >= minW && _settings.WindowHeight >= minH)
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
}
