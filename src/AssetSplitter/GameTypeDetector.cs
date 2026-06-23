namespace AssetProcessor;

internal static class GameTypeDetector
{
    public const string Anno117 = "anno117";
    public const string Anno1800 = "anno1800";
    public const string UnknownAnno = "unknown_anno";

    public static bool IsAnno117(string gameType)
    {
        return gameType.Equals(Anno117, StringComparison.OrdinalIgnoreCase);
    }

    public static string DetectFromPath(string gamePath)
    {
        if (gamePath.Contains("Anno 117", StringComparison.OrdinalIgnoreCase)
            || gamePath.Contains("Pax Romana", StringComparison.OrdinalIgnoreCase)
            || gamePath.Contains("Anno117", StringComparison.OrdinalIgnoreCase))
        {
            return Anno117;
        }

        if (gamePath.Contains("Anno 1800", StringComparison.OrdinalIgnoreCase)
            || gamePath.Contains("Anno1800", StringComparison.OrdinalIgnoreCase))
        {
            return Anno1800;
        }

        string maindataPath = Path.Combine(gamePath, "maindata");
        try
        {
            if (File.Exists(Path.Combine(maindataPath, "config.rda"))
                || File.Exists(Path.Combine(maindataPath, "shared_configs.rda")))
            {
                return Anno117;
            }

            if (Directory.Exists(maindataPath)
                && Directory.EnumerateFiles(maindataPath, "data*.rda").Any())
            {
                return Anno1800;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return UnknownAnno;
        }

        return DetectFromPropertiesFile(Path.Combine(gamePath, "properties.xml"));
    }

    private static string DetectFromPropertiesFile(string propertiesPath)
    {
        if (!File.Exists(propertiesPath))
        {
            return UnknownAnno;
        }

        try
        {
            string content = File.ReadAllText(propertiesPath);
            if (content.Contains("ComponentLocation", StringComparison.Ordinal)
                || content.Contains("EmptyAutoCreateValue", StringComparison.Ordinal)
                || content.Contains("BoolVariableOrValue", StringComparison.Ordinal))
            {
                return Anno117;
            }

            if (content.Contains("InfluenceDistance", StringComparison.Ordinal)
                || content.Contains("TradePrice", StringComparison.Ordinal))
            {
                return Anno1800;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return UnknownAnno;
        }

        return UnknownAnno;
    }
}
