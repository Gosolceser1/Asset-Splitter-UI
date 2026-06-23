using Newtonsoft.Json;
using AssetProcessor;

namespace AssetProcessor;

internal static class AssetProcessorConfiguration
{
    private const string RegionalIngredientsFile = "regional_ingredients.json";
    private const string RegionalIngredientsFolder = "03_Regional_Ingredients";
    private const string AppSettingsFile = "app_settings.json";
    private const string Anno1800GameType = "anno1800";
    private const string IngredientsRegionKey = "ingredients";

    public static string GetConfigPath(string filename)
    {
        string subfolder = filename switch
        {
            RegionalIngredientsFile => RegionalIngredientsFolder,
            AppSettingsFile => "",
            _ => ""
        };

        return ConfigPathResolver.Resolve(subfolder, filename);
    }

    public static bool TryLoadJsonConfig<T>(string configPath, out T? config, out string? errorMessage) where T : class
    {
        config = null;
        errorMessage = null;

        try
        {
            string configJson = File.ReadAllText(configPath);
            config = JsonConvert.DeserializeObject<T>(configJson);
            return config is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static string[] GetAfricanIngredients(RegionalIngredientsConfig? config, PipelineContext? context = null)
        => GetIngredientGuids(GetAnno1800RegionalConfig(config)?.Africa, ["114356", "114402"], context);

    public static string[] GetDefaultIngredients(RegionalIngredientsConfig? config, PipelineContext? context = null)
        => GetIngredientGuids(GetAnno1800RegionalConfig(config)?.Default, ["1010196", "1010205", "1010202"], context);

    private static GameRegionalConfig? GetAnno1800RegionalConfig(RegionalIngredientsConfig? config)
        => config?.Games.TryGetValue(Anno1800GameType, out GameRegionalConfig? game) == true ? game : null;

    public static bool LoadRegionalIngredientsConfig(PipelineContext context)
    {
        string configPath = GetConfigPath("regional_ingredients.json");

        if (!TryLoadConfig(
          configPath,
          $"[WARN] Regional ingredients config not found: {configPath}, using hardcoded values",
          "[ERROR] Failed to load regional ingredients config: ",
          "[INFO] Using hardcoded regional ingredients",
          out RegionalIngredientsConfig? config,
          context))
        {
            return false;
        }

        context.RegionalIngredientsConfig = config;
        return true;
    }

    public static bool LoadAppSettingsConfig(PipelineContext context)
    {
        string configPath = GetConfigPath("app_settings.json");

        if (!TryLoadConfig(
          configPath,
          $"[WARN] App settings config not found: {configPath}, using hardcoded values",
          "[ERROR] Failed to load app settings config: ",
          "[INFO] Using hardcoded application settings",
          out AppSettingsConfig? config,
          context))
        {
            return false;
        }

        context.AppSettingsConfig = config;
        return true;
    }

    private static bool TryLoadConfig<T>(
      string configPath,
      string missingConfigMessage,
      string loadErrorPrefix,
      string fallbackMessage,
      out T? config,
      PipelineContext context) where T : class
    {
        config = null;
        if (!File.Exists(configPath))
        {
            context.Log.Write("WARNING", missingConfigMessage);
            return false;
        }

        if (TryLoadJsonConfig(configPath, out config, out string? errorMessage))
        {
            return true;
        }

        if (errorMessage is not null)
        {
            context.Log.Write("ERROR", loadErrorPrefix + errorMessage, always: true);
            context.Log.Write("INFO", fallbackMessage, always: true);
        }

        return false;
    }

    private static string[] GetIngredientGuids(Dictionary<string, RegionConfig>? regionConfigs, string[] fallbackGuids, PipelineContext? context = null)
    {
        if (regionConfigs is null
            || !regionConfigs.TryGetValue(IngredientsRegionKey, out RegionConfig? region)
            || region.Ingredients.Count == 0)
        {
            context?.Log?.Write("WARN", "Using hardcoded fallback ingredient GUIDs — config file missing or invalid.");
            return [.. fallbackGuids];
        }

        string[] guids =
        [
            ..region.Ingredients
              .Select(ingredient => ingredient.Guid)
              .Where(guid => !string.IsNullOrWhiteSpace(guid))
        ];

        return guids.Length > 0 ? guids : [.. fallbackGuids];
    }
}
