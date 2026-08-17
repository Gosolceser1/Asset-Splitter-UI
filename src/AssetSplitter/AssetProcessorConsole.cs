namespace AssetProcessor;

internal static class AssetProcessorConsole
{
    private static readonly Dictionary<ConsoleMessageKind, ConsoleColor> MessageColors = new()
    {
        [ConsoleMessageKind.Ok] = ConsoleColor.Green,
        [ConsoleMessageKind.Fix] = ConsoleColor.Cyan,
        [ConsoleMessageKind.Rda] = ConsoleColor.Yellow,
        [ConsoleMessageKind.Info] = ConsoleColor.White,
        [ConsoleMessageKind.Debug] = ConsoleColor.DarkGray,
        [ConsoleMessageKind.Error] = ConsoleColor.Red,
        [ConsoleMessageKind.Merge] = ConsoleColor.Yellow,
        [ConsoleMessageKind.Phase] = ConsoleColor.Yellow,
        [ConsoleMessageKind.Split] = ConsoleColor.White,
        [ConsoleMessageKind.Trans] = ConsoleColor.Cyan,
        [ConsoleMessageKind.Assets] = ConsoleColor.Green,
        [ConsoleMessageKind.Config] = ConsoleColor.Cyan,
        [ConsoleMessageKind.Format] = ConsoleColor.White,
        [ConsoleMessageKind.Header] = ConsoleColor.Magenta,
        [ConsoleMessageKind.Analyze] = ConsoleColor.Magenta,
        [ConsoleMessageKind.Extract] = ConsoleColor.White,
        [ConsoleMessageKind.Fixlist] = ConsoleColor.Gray,
        [ConsoleMessageKind.Warning] = ConsoleColor.Yellow,
        [ConsoleMessageKind.Complete] = ConsoleColor.Green,
        [ConsoleMessageKind.Progress] = ConsoleColor.Green,
        [ConsoleMessageKind.Templates] = ConsoleColor.Magenta,
        [ConsoleMessageKind.Cache] = ConsoleColor.Cyan,
        [ConsoleMessageKind.Summary] = ConsoleColor.White,
    };

    public static void ShowStartupBanner(string title, string subtitle)
    {
        if (Console.IsOutputRedirected)
        {
            return;
        }

        Console.WriteLine();
        WriteColoredMessage("┌────────────────────────────────────────────────┐", ConsoleMessageKind.Header);
        WriteColoredMessage(title, ConsoleMessageKind.Header);
        WriteColoredMessage("├────────────────────────────────────────────────┤", ConsoleMessageKind.Header);
        WriteColoredMessage(subtitle, ConsoleMessageKind.Info);
        WriteColoredMessage("└────────────────────────────────────────────────┘", ConsoleMessageKind.Header);
        Console.WriteLine();
    }

    public static void WriteColoredMessage(string message, ConsoleMessageKind messageKind)
    {
        ConsoleColor previous = Console.ForegroundColor;
        Console.ForegroundColor = MessageColors.TryGetValue(messageKind, out ConsoleColor color)
            ? color
            : ConsoleColor.White;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }

    public static void WriteColoredMessage(string message, string messageType = "")
    {
        WriteColoredMessage(message, ParseMessageKind(messageType) ?? ConsoleMessageKind.Info);
    }

    public static ConsoleMessageKind? ParseMessageKind(string? messageType)
    {
        if (string.IsNullOrWhiteSpace(messageType))
        {
            return null;
        }

        // Accept legacy uppercase keys ("ERROR") and enum names ("Error").
        if (Enum.TryParse(messageType, ignoreCase: true, out ConsoleMessageKind kind))
        {
            return kind;
        }

        return messageType.ToUpperInvariant() switch
        {
            "OK" => ConsoleMessageKind.Ok,
            "RDA" => ConsoleMessageKind.Rda,
            "WARN" => ConsoleMessageKind.Warning,
            "READY" => ConsoleMessageKind.Complete,
            "SINGLE" => ConsoleMessageKind.Info,
            "MODS" => ConsoleMessageKind.Assets,
            _ => null,
        };
    }
}
