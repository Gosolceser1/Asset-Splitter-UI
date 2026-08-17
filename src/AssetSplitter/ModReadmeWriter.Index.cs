namespace AssetProcessor;

/// <summary>Writes per-export <c>MODDING-GUIDE.md</c>, short per-mod README files, and the export summary README.</summary>
internal static partial class ModReadmeWriter
{
    public static string GetIndexTitle(string templateFolder, string readmeLanguage) =>
        string.Format(ConsoleMessages.GetForLanguage("readmeIndexTitle", readmeLanguage), templateFolder);

    public static string GetIndexBrowseLine(string readmeLanguage) =>
        string.Format(ConsoleMessages.GetForLanguage("readmeIndexBrowseLine", readmeLanguage), ModdingGuideFileName);

    public static IReadOnlyList<string> GetIndexTableHeader(string readmeLanguage) =>
    [
        ConsoleMessages.GetForLanguage("readmeIndexColGuid", readmeLanguage),
        ConsoleMessages.GetForLanguage("readmeIndexColDisplayName", readmeLanguage),
        ConsoleMessages.GetForLanguage("readmeIndexColInternalName", readmeLanguage),
        ConsoleMessages.GetForLanguage("readmeIndexColPathHint", readmeLanguage),
        ConsoleMessages.GetForLanguage("readmeIndexColFolder", readmeLanguage),
    ];
}
