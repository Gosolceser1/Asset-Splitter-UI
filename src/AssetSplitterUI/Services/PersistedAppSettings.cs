using System.Text.Json.Serialization;

namespace AssetSplitterUI.Services;

/// <summary>Persisted UI state stored in the per-user app data settings file.</summary>
public sealed class PersistedAppSettings
{
    /// <summary>Last selected game installation path.</summary>
    public string LastGamePath { get; set; } = "";

    /// <summary>Last selected output folder.</summary>
    public string LastOutputPath { get; set; } = "";

    /// <summary>False until the user has set game + output paths; gates loading saved paths and processing flags.</summary>
    public bool UserSetupComplete { get; set; }

    /// <summary>Bumped when setup/persistence rules change; older files get a blank first-run load once.</summary>
    public int SettingsVersion { get; set; }

    /// <summary>Whether to add translated GUID comments to assets.</summary>
    public bool AddComments { get; set; }

    /// <summary>Whether to resolve BaseAssetGUID dependencies.</summary>
    public bool FixDependencies { get; set; }

    /// <summary>Whether to organize output into template-based folders.</summary>
    public bool CreateTemplateFolders { get; set; }

    /// <summary>Whether to wrap output in ModOps/ModOp; <see langword="false"/> means raw Asset XML only.</summary>
    public bool ModOpsWrap { get; set; }

    /// <summary>Legacy setting: old inverted ModOps-wrap flag, used only when loading old settings files.</summary>
    [JsonPropertyName("NoModOpsWrap")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? NoModOpsWrapLegacy { get; set; }

    /// <summary>Whether to apply default properties from properties.xml when merging.</summary>
    public bool? IncludeDefaultProperties { get; set; }

    /// <summary>Legacy setting: old inverted default-properties flag, used only when loading old settings files.</summary>
    [JsonPropertyName("NoDefaultProperties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? NoDefaultPropertiesLegacy { get; set; }

    /// <summary>Whether to split templates.xml into per-template files.</summary>
    public bool SplitTemplates { get; set; }

    /// <summary>Whether to create one standalone Mod Loader folder for each generated asset XML.</summary>
    public bool CreateAssetMods { get; set; }

    /// <summary>Whether to enable verbose debug logging in the backend. Never persisted — always starts false.</summary>
    [JsonIgnore]
    public bool DebugMode { get; set; }

    /// <summary>UI language, e.g. English or Deutsch.</summary>
    public string UILanguage { get; set; } = "English";

    /// <summary>Theme mode: Light, Dark, or Auto.</summary>
    public string ThemeMode { get; set; } = "Auto";

    /// <summary>Last selected game asset language for GUID comments.</summary>
    public string LastGameLanguage { get; set; } = "";

    /// <summary>Recent game paths for the dropdown.</summary>
    public List<string> RecentGamePaths { get; set; } = [];

    /// <summary>Recent output paths for the dropdown.</summary>
    public List<string> RecentOutputPaths { get; set; } = [];

    /// <summary>Main window width; 0 means use the default.</summary>
    public double WindowWidth { get; set; }

    /// <summary>Main window height; 0 means use the default.</summary>
    public double WindowHeight { get; set; }

    /// <summary>Main window left (screen pixels). Unset when equal to <see cref="WindowPlacementSettings.UnsetCoordinate"/>.</summary>
    public int WindowLeft { get; set; } = WindowPlacementSettings.UnsetCoordinate;

    /// <summary>Main window top (screen pixels). Unset when equal to <see cref="WindowPlacementSettings.UnsetCoordinate"/>.</summary>
    public int WindowTop { get; set; } = WindowPlacementSettings.UnsetCoordinate;

    /// <summary>Whether the main window was maximized on last exit.</summary>
    public bool WindowMaximized { get; set; }

    /// <summary>Display name of the monitor that held the window on last exit (<see cref="Screen.DisplayName"/>).</summary>
    public string? WindowDisplayName { get; set; }
}
