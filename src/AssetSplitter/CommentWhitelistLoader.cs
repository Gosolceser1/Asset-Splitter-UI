namespace AssetProcessor;

internal static class CommentWhitelistLoader
{
    private const string WhitelistFolder = "04_Comment_Whitelist";
    private const string Anno117WhitelistFile = "Anno117_Comment_Whitelist.txt";
    private const string Anno1800WhitelistFile = "Anno1800_Comment_Whitelist.txt";

    private static readonly string[] Anno117FallbackProperties =
    [
        "OasisId",
        "ProductCategory",
        "BuildingCategoryName"
    ];

    public static string[] Load(string gameType, string baseDirectory, out string? warningMessage)
    {
        warningMessage = null;
        string whitelistFile = GameTypeDetector.IsAnno117(gameType)
          ? Anno117WhitelistFile
          : Anno1800WhitelistFile;

        string configWhitelistPath = Path.Combine(baseDirectory, "config", WhitelistFolder, whitelistFile);
        string devFallbackWhitelistPath = Path.GetFullPath(Path.Combine(baseDirectory, "../../../../../config", WhitelistFolder, whitelistFile));
        string whitelistPath = File.Exists(configWhitelistPath) ? configWhitelistPath : devFallbackWhitelistPath;

        if (!File.Exists(whitelistPath))
        {
            return GameTypeDetector.IsAnno117(gameType) ? [.. Anno117FallbackProperties] : [];
        }

        try
        {
            return
            [
                ..File.ReadLines(whitelistPath)
                  .Select(line => line.Trim())
                  .Where(line => line.Length > 0 && !line.StartsWith('#'))
            ];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            warningMessage = $"Warning: Could not load whitelist {whitelistPath}: {ex.Message}";
            return [];
        }
    }
}
