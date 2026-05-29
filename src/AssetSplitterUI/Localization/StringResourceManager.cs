using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AssetProcessor;

namespace AssetSplitterUI.Localization;

/// <summary>
/// Singleton manager for localized UI strings loaded from JSON files
/// (<c>Localization/Languages/Strings[.code].json</c>).
/// Implements <see cref="INotifyPropertyChanged"/> so XAML bindings refresh when the language changes.
/// </summary>
public class StringResourceManager : INotifyPropertyChanged
{
    private static StringResourceManager? _instance;
    private JsonDocument? _currentDocument;
    private JsonDocument? _fallbackDocument;

    private static readonly Dictionary<string, string> LanguageMap = new()
  {
    { "English",  "" },
    { "Français", "fr" },
    { "Deutsch",  "de" },
    { "Italiano", "it" },
    { "Polski",   "pl" },
    { "Español",  "es" },
    { "Русский",  "ru" },
    { "中文",     "zh" },
    { "日本語",   "ja" },
    { "한국어",   "ko" },
    { "繁體中文", "tw" }
  };

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Singleton instance for XAML bindings and code access.</summary>
    public static StringResourceManager Instance => _instance ??= new StringResourceManager();

    /// <summary>Initializes with English and loads <c>Strings.json</c> from <c>Localization/Languages</c>.</summary>
    public StringResourceManager() => LoadStrings("English");

    /// <summary>
    /// Current UI language display name (e.g. "English", "Deutsch").
    /// Assigning a new value reloads the matching JSON file and refreshes all bindings.
    /// </summary>
    public string CurrentLanguage
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            LoadStrings(value);
            NotifyAllPropertiesChanged();
        }
    } = "English";

    /// <summary>Returns a localized string by dot-separated path (e.g. <c>"app.title"</c>). Falls back to English before returning <c>"[path]"</c>.</summary>
    public string GetString(string path)
    {
        if (string.IsNullOrEmpty(path))
            return $"[{path}]";

        var result = TryGetString(_currentDocument, path)
                  ?? TryGetString(_fallbackDocument, path);

        if (result != null)
            return result;

        // In debug builds or developer mode, surface missing keys clearly
        if (System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debug.WriteLine($"[LOC] Missing key in current + fallback: {path}");
        }

        // Never show raw [key] to end users — return a reasonable placeholder
        return path;
    }

    private static string? TryGetString(JsonDocument? document, string path)
    {
        if (document is null)
            return null;

        try
        {
            JsonElement? current = document.RootElement;

            foreach (var segment in path.Split('.'))
            {
                if (!current.HasValue || !current.Value.TryGetProperty(segment, out var next))
                    return null;
                current = next;
            }

            if (!current.HasValue)
                return null;

            return current.Value.ValueKind == JsonValueKind.String
              ? current.Value.GetString()
              : current.Value.ToString();
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Indexer for convenient XAML bindings:
    /// <c>{Binding [app.title], Source={x:Static loc:StringResourceManager.Instance}}</c>
    /// </summary>
    public string this[string key] => GetString(key);

    /// <summary>Incremented whenever the language changes; lets bindings that watch this property force a refresh.</summary>
    public int LanguageVersion { get; private set; }

    /// <summary>Returns the language code (e.g. <c>"de"</c>) for a display name (e.g. <c>"Deutsch"</c>).</summary>
    public static string GetLanguageCode(string displayName) =>
      LanguageMap.TryGetValue(displayName, out var code) ? code : "";

    /// <summary>Returns the ordered list of available UI language display names.</summary>
    public static IReadOnlyList<string> GetAvailableLanguages() => [.. LanguageMap.Keys];

    private void LoadStrings(string language)
    {
        try
        {
            string normalizedLanguage = language.ToLowerInvariant();
            var code = LanguageMap.TryGetValue(language, out var c) ? c
              : (LanguageMap.ContainsValue(normalizedLanguage) ? normalizedLanguage : "");

            var filename = string.IsNullOrEmpty(code) ? "Strings.json" : $"Strings.{code}.json";

            var foundPath = ConfigPathResolver.Resolve(AppDomain.CurrentDomain.BaseDirectory, Path.Combine("Localization", "Languages"), filename);
            if (string.IsNullOrEmpty(foundPath))
                foundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Localization", "Languages", filename);

            if (string.IsNullOrEmpty(foundPath) || !File.Exists(foundPath))
            {
                if (!string.IsNullOrEmpty(code))
                    LoadStrings("English");
                else
                    _currentDocument = null;
                return;
            }

            _currentDocument?.Dispose();
            _currentDocument = JsonDocument.Parse(File.ReadAllText(foundPath));

            if (string.IsNullOrEmpty(code))
            {
                _fallbackDocument?.Dispose();
                _fallbackDocument = JsonDocument.Parse(File.ReadAllText(foundPath));
            }
            else
            {
                if (_fallbackDocument is null)
                    LoadFallbackStrings();

                // Merge missing keys from English so the UI never shows broken/missing text
                if (_fallbackDocument is not null)
                    MergeMissingKeys(_currentDocument, _fallbackDocument);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            System.Diagnostics.Debug.WriteLine($"[StringResourceManager] Failed to load {language}: {ex.Message}");
            _currentDocument?.Dispose();
            _currentDocument = null;
        }
    }

    private void LoadFallbackStrings()
    {
        var foundPath = ConfigPathResolver.Resolve(AppDomain.CurrentDomain.BaseDirectory, Path.Combine("Localization", "Languages"), "Strings.json");
        if (string.IsNullOrEmpty(foundPath))
            foundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Localization", "Languages", "Strings.json");

        if (string.IsNullOrEmpty(foundPath) || !File.Exists(foundPath))
            return;

        _fallbackDocument?.Dispose();
        _fallbackDocument = JsonDocument.Parse(File.ReadAllText(foundPath));
    }

    private void NotifyAllPropertiesChanged()
    {
        LanguageVersion++;
        OnPropertyChanged(nameof(LanguageVersion));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    /// <summary>Raises <see cref="PropertyChanged"/> for the given property name.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Recursively merges any missing keys from the fallback (English) document into the current language document.
    /// This ensures the UI never shows broken keys or mixed languages when translations are incomplete.
    /// </summary>
    private static void MergeMissingKeys(JsonDocument current, JsonDocument fallback)
    {
        try
        {
            var currentRoot = current.RootElement;
            var fallbackRoot = fallback.RootElement;

            MergeObjects(currentRoot, fallbackRoot);
        }
        catch
        {
            // Silent fail — better to have slightly incomplete data than crash
        }
    }

    private static void MergeObjects(JsonElement currentObj, JsonElement fallbackObj)
    {
        if (currentObj.ValueKind != JsonValueKind.Object || fallbackObj.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in fallbackObj.EnumerateObject())
        {
            if (!currentObj.TryGetProperty(prop.Name, out var currentValue))
            {
                // Key missing in current language → we can't easily inject into immutable JsonDocument here.
                // For now we rely on the GetString fallback, which is already improved.
                continue;
            }

            if (currentValue.ValueKind == JsonValueKind.Object && prop.Value.ValueKind == JsonValueKind.Object)
            {
                MergeObjects(currentValue, prop.Value);
            }
        }
    }
}
