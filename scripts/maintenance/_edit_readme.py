import re

path = r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\src\AssetSplitter\ModReadmeWriter.cs'
with open(path, encoding='utf-8') as f:
    content = f.read()

# 1. In WriteExportSummary: add readmeLang + t func after gameDisplayName
old = '        string gameDisplayName = gameType.Equals("anno117", StringComparison.OrdinalIgnoreCase) ? "Anno 117" : "Anno 1800";\n\n        var lines = new List<string>'
new = '        string gameDisplayName = gameType.Equals("anno117", StringComparison.OrdinalIgnoreCase) ? "Anno 117" : "Anno 1800";\n\n        string readmeLang = context.ReadmeLanguage;\n\n        Func<string, string> t = key => ConsoleMessages.GetForLanguage(key, readmeLang);\n\n        var lines = new List<string>'
assert old in content, "chunk 1 not found"
content = content.replace(old, new, 1)

# 2. Replace ConsoleMessages.Get("readmeSummaryTitle") with t("readmeSummaryTitle")
content = content.replace('ConsoleMessages.Get("readmeSummaryTitle")', 't("readmeSummaryTitle")', 1)

# 3. Replace ConsoleMessages.Get("readmeSummaryGame") with t("readmeSummaryGame")
content = content.replace('ConsoleMessages.Get("readmeSummaryGame")', 't("readmeSummaryGame")', 1)

# 4. Replace GetBuildFootprintLines(context, ConsoleMessages.Get, with GetBuildFootprintLines(context, t,
content = content.replace('GetBuildFootprintLines(context, ConsoleMessages.Get,', 'GetBuildFootprintLines(context, t,', 1)

# 5. Replace ConsoleMessages.Get("readmeSummaryCreated") with t("readmeSummaryCreated")
content = content.replace('ConsoleMessages.Get("readmeSummaryCreated")', 't("readmeSummaryCreated")', 1)

# 6. Replace ConsoleMessages.Get("readmeSummarySkipped") with t("readmeSummarySkipped")
content = content.replace('ConsoleMessages.Get("readmeSummarySkipped")', 't("readmeSummarySkipped")', 1)

# 7. Replace ConsoleMessages.Get("readmeSummaryIntro") with t("readmeSummaryIntro")
content = content.replace('ConsoleMessages.Get("readmeSummaryIntro")', 't("readmeSummaryIntro")', 1)

# 8. Replace ConsoleMessages.Get("readmeSummaryGuide") with t("readmeSummaryGuide")
content = content.replace('ConsoleMessages.Get("readmeSummaryGuide")', 't("readmeSummaryGuide")', 1)

# 9. Replace ConsoleMessages.Get("readmeSummaryIndex") with t("readmeSummaryIndex")
content = content.replace('ConsoleMessages.Get("readmeSummaryIndex")', 't("readmeSummaryIndex")', 1)

# 10. Replace ConsoleMessages.Get("readmeSummaryWarning") with t("readmeSummaryWarning")
content = content.replace('ConsoleMessages.Get("readmeSummaryWarning")', 't("readmeSummaryWarning")', 1)

# 11. Replace ConsoleMessages.Get("readmeSummarySingle") with t("readmeSummarySingle")
content = content.replace('ConsoleMessages.Get("readmeSummarySingle")', 't("readmeSummarySingle")', 1)

# 12. GetIndexTitle - add readmeLanguage param
old = 'public static string GetIndexTitle(string templateFolder) =>\n\n        string.Format(ConsoleMessages.Get("readmeIndexTitle"), templateFolder);'
new = 'public static string GetIndexTitle(string templateFolder, string readmeLanguage) =>\n\n        string.Format(ConsoleMessages.GetForLanguage("readmeIndexTitle", readmeLanguage), templateFolder);'
assert old in content, "chunk 12 not found"
content = content.replace(old, new, 1)

# 13. GetIndexBrowseLine - add readmeLanguage param
old = 'public static string GetIndexBrowseLine() =>\n\n        string.Format(ConsoleMessages.Get("readmeIndexBrowseLine"), ModdingGuideFileName);'
new = 'public static string GetIndexBrowseLine(string readmeLanguage) =>\n\n        string.Format(ConsoleMessages.GetForLanguage("readmeIndexBrowseLine", readmeLanguage), ModdingGuideFileName);'
assert old in content, "chunk 13 not found"
content = content.replace(old, new, 1)

# 14. GetIndexTableHeader - add readmeLanguage param
old = 'public static IReadOnlyList<string> GetIndexTableHeader() =>\n\n    [\n\n        ConsoleMessages.Get("readmeIndexColGuid"),\n\n        ConsoleMessages.Get("readmeIndexColDisplayName"),\n\n        ConsoleMessages.Get("readmeIndexColInternalName"),\n\n        ConsoleMessages.Get("readmeIndexColPathHint"),\n\n        ConsoleMessages.Get("readmeIndexColFolder"),\n\n    ];'
new = 'public static IReadOnlyList<string> GetIndexTableHeader(string readmeLanguage) =>\n\n    [\n\n        ConsoleMessages.GetForLanguage("readmeIndexColGuid", readmeLanguage),\n\n        ConsoleMessages.GetForLanguage("readmeIndexColDisplayName", readmeLanguage),\n\n        ConsoleMessages.GetForLanguage("readmeIndexColInternalName", readmeLanguage),\n\n        ConsoleMessages.GetForLanguage("readmeIndexColPathHint", readmeLanguage),\n\n        ConsoleMessages.GetForLanguage("readmeIndexColFolder", readmeLanguage),\n\n    ];'
assert old in content, "chunk 14 not found"
content = content.replace(old, new, 1)

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)

print("All edits applied successfully")
