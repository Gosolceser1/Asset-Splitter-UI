using System.Text.Json;
using AssetProcessor;

namespace AssetProcessor;

/// <summary>
/// Provides localized console messages loaded from user-editable JSON files.
/// Files are located in <c>config/05_Console_Messages/console_{language}.json</c>.
/// Falls back to embedded English defaults when the file is not found.
/// </summary>
public static partial class ConsoleMessages
{
    private const string EnglishLanguage = "en";
    private const string ConsoleMessagesFolder = "05_Console_Messages";

    private static string _currentLanguage = EnglishLanguage;
    private static Dictionary<string, string> _messages = [];
    private static Dictionary<string, string> _fallbackMessages = [];
    private static bool _initialized;

    private static readonly Dictionary<string, string> DefaultMessages = CreateDefaultMessages();

    private static Dictionary<string, string> CreateDefaultMessages()
    {
        Dictionary<string, string> messages = new(StringComparer.Ordinal);
        AddBannerDefaults(messages);
        AddDebugDefaults(messages);
        AddErrorDefaults(messages);
        AddGeneralDefaults(messages);
        AddHelpDefaults(messages);
        AddPhaseDefaults(messages);
        AddProgressDefaults(messages);
        AddRdaDefaults(messages);
        return messages;
    }

    public static void SetLanguage(string language)
    {
        _currentLanguage = NormalizeLanguage(language);

        LoadMessages();
    }

    /// <summary>
    /// Returns a localized message by key.
    /// Priority: current language → English fallback → embedded defaults → key itself.
    /// </summary>
    /// <param name="key">Message key (e.g. "extractingFromRda").</param>
    public static string Get(string key)
    {
        if (!_initialized)
        {
            LoadMessages();
        }

        if (_messages.TryGetValue(key, out string? message) && !string.IsNullOrEmpty(message))
        {
            return message;
        }

        if (_fallbackMessages.TryGetValue(key, out string? fallback) && !string.IsNullOrEmpty(fallback))
        {
            return fallback;
        }

        if (DefaultMessages.TryGetValue(key, out string? defaultMessage))
        {
            return defaultMessage;
        }

        return key;
    }

    /// <summary>
    /// Returns a localized message for a specific language without changing the current global language.
    /// Priority: requested language → English fallback → embedded defaults → key itself.
    /// </summary>
    /// <param name="key">Message key.</param>
    /// <param name="language">Language code (e.g. "russian", "german").</param>
    public static string GetForLanguage(string key, string language)
    {
        string normalized = NormalizeLanguage(language);
        Dictionary<string, string> messages = LoadMessagesFromFile(normalized);
        if (messages.TryGetValue(key, out string? msg) && !string.IsNullOrEmpty(msg))
        {
            return msg;
        }

        if (normalized != EnglishLanguage)
        {
            messages = LoadMessagesFromFile(EnglishLanguage);
            if (messages.TryGetValue(key, out msg) && !string.IsNullOrEmpty(msg))
            {
                return msg;
            }
        }

        if (DefaultMessages.TryGetValue(key, out string? dm) && !string.IsNullOrEmpty(dm))
        {
            return dm;
        }

        return key;
    }

    /// <summary>Initialises _messages for the current language and _fallbackMessages for English (or DefaultMessages when already English).</summary>
    private static void LoadMessages()
    {
        _initialized = true;
        _messages = LoadMessagesFromFile(_currentLanguage);
        _fallbackMessages = _currentLanguage != EnglishLanguage
          ? LoadMessagesFromFile(EnglishLanguage)
          : new(DefaultMessages);
    }

    /// <summary>Parses console_&lt;language&gt;.json from disk; returns DefaultMessages for english, or an empty dict when a non-English file is missing or unreadable.</summary>
    private static Dictionary<string, string> LoadMessagesFromFile(string language)
    {
        string? filePath = GetConfigPath(language);

        if (filePath is not null && File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                Dictionary<string, string>? messages = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (messages is not null)
                {
                    return messages;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Silently fall back to defaults on any parse/IO error.
            }
        }

        return language == EnglishLanguage ? new(DefaultMessages) : [];
    }

    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return EnglishLanguage;
        }

        return language.Trim().ToLowerInvariant() switch
        {
            "english" => EnglishLanguage,
            "german" => "de",
            "french" => "fr",
            "spanish" => "es",
            "italian" => "it",
            "polish" => "pl",
            "russian" => "ru",
            "japanese" => "ja",
            "korean" => "ko",
            "chinese" or "simplified_chinese" => "zh",
            "traditional_chinese" or "tchinese" => "tw",
            "mexican" => "es",
            var normalized => normalized
        };
    }

    /// <summary>Returns the first existing file path for console_&lt;language&gt;.json across production, dev, and relative path candidates; null if none exist.</summary>
    private static string? GetConfigPath(string language)
    {
        string filename = $"console_{language}.json";
        string resolved = ConfigPathResolver.Resolve(ConsoleMessagesFolder, filename);
        return string.IsNullOrEmpty(resolved) ? null : resolved;
    }
}
