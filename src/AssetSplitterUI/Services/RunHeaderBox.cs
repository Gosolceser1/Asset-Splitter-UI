namespace AssetSplitterUI.Services;

/// <summary>Builds a fixed-width run header box so borders align in every locale.</summary>
internal static class RunHeaderBox
{
    private const int InnerWidth = 55;
    private const char Horizontal = '\u2500';

    public static IReadOnlyList<string> BuildLines(string title = "ASSET SPLITTER")
    {
        string inner = title.Length > InnerWidth ? title[..InnerWidth] : title;
        int leftPad = Math.Max(0, (InnerWidth - inner.Length) / 2);
        string row = inner.PadLeft(leftPad + inner.Length).PadRight(InnerWidth);
        string horizontal = new string(Horizontal, InnerWidth);
        return
        [
            $"┌{horizontal}┐",
            $"│{row}│",
            $"└{horizontal}┘",
        ];
    }
}
