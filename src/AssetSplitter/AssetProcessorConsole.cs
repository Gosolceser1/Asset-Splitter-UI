namespace AssetProcessor;

internal static class AssetProcessorConsole
{
    private static readonly Dictionary<string, ConsoleColor> MessageColors = new(StringComparer.OrdinalIgnoreCase)
    {
        { "OK", ConsoleColor.Green },
        { "FIX", ConsoleColor.Cyan },
        { "RDA", ConsoleColor.Yellow },
        { "INFO", ConsoleColor.White },
        { "DEBUG", ConsoleColor.DarkGray },
        { "ERROR", ConsoleColor.Red },
        { "MERGE", ConsoleColor.Yellow },
        { "PHASE", ConsoleColor.Yellow },
        { "SPLIT", ConsoleColor.White },
        { "TRANS", ConsoleColor.Cyan },
        { "ASSETS", ConsoleColor.Green },
        { "CONFIG", ConsoleColor.Cyan },
        { "FORMAT", ConsoleColor.White },
        { "HEADER", ConsoleColor.Magenta },
        { "ANALYZE", ConsoleColor.Magenta },
        { "EXTRACT", ConsoleColor.White },
        { "FIXLIST", ConsoleColor.Gray },
        { "WARNING", ConsoleColor.Yellow },
        { "COMPLETE", ConsoleColor.Green },
        { "PROGRESS", ConsoleColor.Green },
        { "TEMPLATES", ConsoleColor.Magenta },
        { "CACHE", ConsoleColor.Cyan }
    };

    public static void ShowStartupBanner(string title, string subtitle)
    {
        if (Console.IsOutputRedirected)
            return;

        Console.WriteLine();
        WriteColoredMessage("┌────────────────────────────────────────────────┐", "HEADER");
        WriteColoredMessage(title, "HEADER");
        WriteColoredMessage("├────────────────────────────────────────────────┤", "HEADER");
        WriteColoredMessage(subtitle, "INFO");
        WriteColoredMessage("└────────────────────────────────────────────────┘", "HEADER");
        Console.WriteLine();
    }

    public static void ShowGameDetectionBanner(string gameType, string detectedMessage, string readyMessage)
    {
        Console.WriteLine();
        string gameName = gameType.Contains("1800", StringComparison.OrdinalIgnoreCase)
            ? "Anno 1800"
            : "Anno 117 - Pax Romana";

        WriteColoredMessage("┌────────────────────────────────────────────────┐", "HEADER");
        WriteColoredMessage(detectedMessage.Replace("{0}", gameName), "COMPLETE");
        WriteColoredMessage(readyMessage, "OK");
        WriteColoredMessage("└────────────────────────────────────────────────┘", "HEADER");
        Console.WriteLine();
    }

    public static void WriteColoredMessage(string message, string messageType = "")
    {
        ConsoleColor previous = Console.ForegroundColor;

        Console.ForegroundColor =
            !string.IsNullOrEmpty(messageType) && MessageColors.TryGetValue(messageType, out ConsoleColor color)
                ? color
                : ConsoleColor.White;

        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }
}
