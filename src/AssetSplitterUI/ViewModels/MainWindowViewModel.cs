using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

/// <summary>
/// Main window VM: game/output paths, options (comments, fix dependencies, template folders), and run logic.
/// Starts AssetProcessor.exe as a subprocess, tracks progress via stdout, and updates UI (progress bar, status, log).
/// Uses IPlatformServices for folder picker and game detection. Child processes are tracked via ChildProcessTracker.
/// </summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IPlatformServices _platformServices;
    private readonly ExtractionCoordinator _extractionCoordinator;
    private readonly MainWindowLogStore _logStore;
    private readonly GameConsoleStateStore _consoleStateStore = new();
    private readonly SettingsCoordinator _settingsCoordinator;
    private readonly BusyIndicatorAnimator _busyAnimator;
    private readonly LocalizedTextState _statusTextState;
    private readonly ConsoleOutputCoordinator _consoleOutput;
    private double _lastDispatchedProgress = -1;
    private int _availableLanguageRefreshVersion;
    private CancellationTokenSource? _singleGuidLookupCts;
    private CancellationTokenSource? _gamePathValidationCts;
    private string? _lastSingleGuidLookupGuid;
    private SingleGuidLookupResult? _lastSingleGuidLookupResult;
    private bool _updatingGamePathFromSelector;
    private bool _isGamePathRecognized;

    /// <summary>
    /// After Phase 1, the UI language dropdown can change automatically; developer report stays aligned with the backend run.
    /// </summary>
    private string? _lastRunBackendAssetLanguage;

    private string _lastRunBackendSingleGuid = "";

    [ObservableProperty]
    public partial string GamePath { get; set; } = "";

    [ObservableProperty]
    public partial string OutputPath { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameLanguageToolTipText))]
    public partial string SelectedLanguage { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSingleGuidMode))]
    [NotifyPropertyChangedFor(nameof(AreBroadOutputOptionsEnabled))]
    public partial string SingleGuid { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSingleGuidStatus))]
    public partial string SingleGuidStatusText { get; set; } = "";

    public bool HasSingleGuidStatus => !string.IsNullOrWhiteSpace(SingleGuidStatusText);

    public bool IsSingleGuidMode => !string.IsNullOrWhiteSpace(SingleGuid);

    public bool AreBroadOutputOptionsEnabled => SourceFilesExist && !IsSingleGuidMode && !IsProcessing;

    /// <summary>Processing toggles, language, and GUID filter unlock after complete source_xml is on disk.</summary>
    public bool AreProcessingOptionsEnabled => SourceFilesExist && !IsProcessing;

    /// <summary>Section tooltip for the extraction-options panel (locked vs ready).</summary>
    public string ExtractionOptionsToolTipText =>
        StringResourceManager.Instance.GetString(
            SourceFilesExist ? "tooltips.processingOptionsWhenReady" : "tooltips.processingOptionsWhenLocked");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameLanguageSelectionEnabled))]
    [NotifyPropertyChangedFor(nameof(GameLanguageToolTipText))]
    [NotifyPropertyChangedFor(nameof(CommentsToolTipText))]
    public partial bool AddComments { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DependenciesToolTipText))]
    public partial bool FixDependencies { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemplateFoldersToolTipText))]
    public partial bool CreateTemplateFolders { get; set; }

    /// <summary>When true, output is wrapped in ModOps/ModOp; when false, raw &lt;Asset&gt; XML only.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModOpsWrapToolTipText))]
    public partial bool ModOpsWrap { get; set; }

    /// <summary>When true, fill in missing properties from properties.xml when merging; when false, skip (user opts out).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IncludeDefaultPropertiesToolTipText))]
    public partial bool IncludeDefaultProperties { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SplitTemplatesToolTipText))]
    public partial bool SplitTemplates { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CreateAssetModsToolTipText))]
    [NotifyPropertyChangedFor(nameof(TemplateFoldersToolTipText))]
    [NotifyPropertyChangedFor(nameof(ModOpsWrapToolTipText))]
    public partial bool CreateAssetMods { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseSingleGuid))]
    [NotifyPropertyChangedFor(nameof(ShowStructuredRunStatus))]
    [NotifyPropertyChangedFor(nameof(ShowLegacyStatusLine))]
    [NotifyPropertyChangedFor(nameof(ShowDeveloperRawStatusMirror))]
    [NotifyPropertyChangedFor(nameof(ShowDeveloperStepPercent))]
    [NotifyPropertyChangedFor(nameof(ShowRunOperationCounts))]
    public partial bool IsProcessing { get; set; }

    [ObservableProperty]
    public partial string Ellipsis { get; set; } = "";

    [ObservableProperty]
    public partial string SpinnerIcon { get; set; } = "";

    /// <summary>Color-coded log entries bound to the console <c>ListBox</c>.</summary>
    public ObservableCollection<LogLine> LogLines => _logStore.Lines;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(ProgressBarColor))]
    [NotifyPropertyChangedFor(nameof(ShowStructuredRunStatus))]
    [NotifyPropertyChangedFor(nameof(ShowLegacyStatusLine))]
    public partial double Progress { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressBarColor))]
    public partial bool IsComplete { get; set; }

    /// <summary>Formatted progress percentage for display (e.g. "45.2%") or empty when not started.</summary>
    public string ProgressText => Progress > 0 ? $"{Progress:F1}%" : "";

    /// <summary>Hex color for progress bar: burnished amber while running, green on success, red on error.</summary>
    public string ProgressBarColor => (IsComplete, StatusIsError) switch
    {
        (true, true) => "#D94545",
        (true, false) => "#5DBE6E",
        _ => "#D4A843"
    };

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressBarColor))]
    public partial bool StatusIsError { get; set; }

    /// <summary>
    /// Incremented once per log-flush batch so the view can scroll to the newest line
    /// without subscribing to every individual <see cref="LogLines"/> CollectionChanged event.
    /// </summary>
    [ObservableProperty]
    public partial int LogVersion { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceFilesExist))]
    public partial bool Phase1Complete { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExtractButtonText))]
    [NotifyPropertyChangedFor(nameof(SourceFilesExist))]
    [NotifyPropertyChangedFor(nameof(CanUseSingleGuid))]
    [NotifyPropertyChangedFor(nameof(AreProcessingOptionsEnabled))]
    [NotifyPropertyChangedFor(nameof(AreBroadOutputOptionsEnabled))]
    [NotifyPropertyChangedFor(nameof(ExtractionOptionsToolTipText))]
    [NotifyPropertyChangedFor(nameof(ShowFirstRunHint))]
    public partial string DetectedGameType { get; set; } = "";

    /// <summary>Currently selected game from the Auto-Detect results; switching it updates <see cref="GamePath"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetectedGames))]
    public partial GameInstallation? SelectedDetectedGame { get; set; }

    /// <summary>True when at least one installation has been detected (drives the selector ComboBox visibility).</summary>
    public bool HasDetectedGames => DetectedGames.Count > 0;

    /// <summary>Feedback shown in the Game Configuration section (never touches the console status bar).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetectStatus))]
    public partial string DetectStatusText { get; set; } = "";

    /// <summary>Stored so we can re-resolve DetectStatusText when UI language changes.</summary>
    private string? _detectStatusKey;
    private object[]? _detectStatusFormatArgs;

    /// <summary>True when there is a detection status message to display.</summary>
    public bool HasDetectStatus => !string.IsNullOrEmpty(DetectStatusText);

    /// <summary>Check if source XML files already exist (can skip Phase 1)</summary>
    public bool SourceFilesExist => !string.IsNullOrEmpty(DetectedGameType);

    /// <summary>Shown in Game Configuration before the first successful source extraction.</summary>
    public bool ShowFirstRunHint => _settingsCoordinator.IsFirstRunExperience || !SourceFilesExist;

    public bool CanUseSingleGuid => SourceFilesExist && !IsProcessing;

    /// <summary>Dynamic button text based on current state</summary>
    public string ExtractButtonText => SourceFilesExist
        ? StringResourceManager.Instance.GetString("buttons.processAssets")
        : StringResourceManager.Instance.GetString("buttons.extract");

    [ObservableProperty]
    public partial string SelectedUILanguage { get; set; } = "English";

    [ObservableProperty]
    public partial string SelectedTheme { get; set; } = "Auto";

    /// <summary>Theme options: Light, Dark, Auto (follow system).</summary>
    public ObservableCollection<string> AvailableThemes { get; }

    /// <summary>Opens folder picker for game installation path.</summary>
    public ICommand BrowseGamePathCommand { get; }
    /// <summary>Opens folder picker for output path.</summary>
    public ICommand BrowseOutputPathCommand { get; }
    /// <summary>Scans system for installed Anno games and fills DetectedGames.</summary>
    public ICommand DetectGamesCommand { get; }
    /// <summary>Starts extraction (Phase 1) or asset processing (Phase 2) depending on state.</summary>
    public IAsyncRelayCommand StartExtractionCommand { get; }
    /// <summary>Cancels the running extraction/processing.</summary>
    public ICommand CancelExtractionCommand { get; }
    /// <summary>Opens the output folder in the system file manager.</summary>
    public ICommand OpenOutputFolderCommand { get; }

    /// <summary>True when paths are set, the game folder is a valid Anno install, and no extraction is in progress.</summary>
    public bool IsGamePathRecognized
    {
        get => _isGamePathRecognized;
        private set
        {
            if (SetProperty(ref _isGamePathRecognized, value))
            {
                StartExtractionCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CanStartExtraction));
                NotifyFirstRunStepHintsChanged();
            }
        }
    }

    public bool CanStartExtraction =>
        !IsProcessing && IsGamePathRecognized && !string.IsNullOrWhiteSpace(OutputPath);
    /// <summary>True when an extraction is running and can be cancelled.</summary>
    public bool CanCancel => IsProcessing;

    /// <summary>UI language options (English, Deutsch, etc.).</summary>
    public ObservableCollection<string> AvailableUILanguages { get; }
    /// <summary>Game asset language options for GUID comments (from config or detected).</summary>
    public ObservableCollection<string> AvailableGameLanguages { get; }
    /// <summary>Detected Anno installations (path, game type, display name).</summary>
    public ObservableCollection<GameInstallation> DetectedGames { get; }
    /// <summary>Recently used game paths for dropdown.</summary>
    public ObservableCollection<string> RecentGamePaths { get; }
    /// <summary>Recently used output paths for dropdown.</summary>
    public ObservableCollection<string> RecentOutputPaths { get; }

    /// <summary>True when at least one game language file (texts_*.xml) is available.</summary>
    public bool HasGameLanguages => AvailableGameLanguages.Count > 0;

    /// <summary>Creates the VM with platform services (folder picker, game detection, open folder).</summary>
    public MainWindowViewModel(IPlatformServices platformServices)
    {
        _platformServices = platformServices;
        _extractionCoordinator = new ExtractionCoordinator();
        _settingsCoordinator = new SettingsCoordinator(new AppSettingsStore(platformServices));
        _logStore = new MainWindowLogStore(() => LogVersion++);
        _busyAnimator = new BusyIndicatorAnimator((spinner, ellipsis) =>
        {
            SpinnerIcon = spinner;
            Ellipsis = ellipsis;
        });
        _statusTextState = new LocalizedTextState(
            text => StatusText = text,
            () => OnPropertyChanged(nameof(StatusText)));
        _consoleOutput = new ConsoleOutputCoordinator(
            _logStore,
            SetStatusTextRaw,
            SetStatusTextLocalized);
        AvailableUILanguages = new ObservableCollection<string>(StringResourceManager.GetAvailableLanguages());
        AvailableThemes = ["Light", "Auto", "Dark"];
        AvailableGameLanguages = [];
        AvailableGameLanguages.CollectionChanged += OnAvailableGameLanguagesChanged;
        DetectedGames = [];
        RecentGamePaths = [];
        RecentOutputPaths = [];

        BrowseGamePathCommand = new AsyncRelayCommand(BrowseGamePathAsync);
        BrowseOutputPathCommand = new AsyncRelayCommand(BrowseOutputPathAsync);
        DetectGamesCommand = new AsyncRelayCommand(DetectGamesAsync);
        StartExtractionCommand = new AsyncRelayCommand(StartExtractionAsync, () => CanStartExtraction);
        CancelExtractionCommand = new RelayCommand(CancelExtraction, () => CanCancel);
        OpenOutputFolderCommand = new AsyncRelayCommand(OpenOutputFolderAsync);
        OpenAnnoAssetsFolderCommand = new AsyncRelayCommand(OpenAnnoAssetsFolderAsync);
        ClearLogCommand = new RelayCommand(ClearLog);

        // When UI language changes, re-resolve DetectStatusText and console UI-generated lines.
        StringResourceManager.Instance.PropertyChanged += OnStringResourceManagerPropertyChanged;
        ApplicationThemeService.ThemeChanged += OnApplicationThemeChanged;

        // LoadSettings() is deferred to MainWindow.Opened so the window appears immediately.
        SetStatusTextLocalized("console.ready");
    }

    private void OnApplicationThemeChanged()
    {
        if (_logStore.IsShutdown)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!_logStore.IsShutdown && LogLines.Count > 0)
            {
                _logStore.RefreshColors();
            }
        }, DispatcherPriority.Background);
    }

    private void OnAvailableGameLanguagesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        NotifyGameLanguageUiChanged();

    private void OnStringResourceManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (null or nameof(StringResourceManager.LanguageVersion)))
        {
            return;
        }

        // Covers language changes triggered from anywhere (not only SelectedUILanguage setter).
        OnPropertyChanged(nameof(ExtractButtonText));
        OnPropertyChanged(nameof(GameLanguageToolTipText));
        NotifyProcessingTooltipsChanged();
        RefreshDetectStatusText();
        RefreshSingleGuidStatusText();
        RefreshLocalizedLogLinesInternal();
    }

    /// <summary>Re-resolves DetectStatusText from its stored key and format args; called when UI language changes.</summary>
    private void RefreshDetectStatusText()
    {
        if (_detectStatusKey is null)
        {
            return;
        }

        try
        {
            DetectStatusText = ResolveLocalizedText(_detectStatusKey, _detectStatusFormatArgs);
        }
        catch
        {
            DetectStatusText = StringResourceManager.Instance.GetString(_detectStatusKey);
        }
    }

    private static string ResolveLocalizedText(string key, object[]? args)
    {
        string template = StringResourceManager.Instance.GetString(key);
        if (args is not { Length: > 0 })
        {
            return template;
        }

        object[] resolvedArgs = args
            .Select(arg => arg is LocalizedConsoleArgument localized
                ? ConsoleOutputLocalizer.ResolveLocalized(localized.Key, localized.Args)
                : arg)
            .ToArray();

        return string.Format(template, resolvedArgs);
    }

    /// <summary>Called when the window is first shown so startup stays fast.</summary>
}
