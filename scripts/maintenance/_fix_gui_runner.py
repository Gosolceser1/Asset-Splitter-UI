import re

path = r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\src\AssetSplitterUI\Services\GuiProcessRunner.cs'
with open(path, encoding='utf-8') as f:
    content = f.read()

# 1. Add braces to if (timedOut) at line 50
content = content.replace(
    "        if (timedOut)\n            outputCallback?.Invoke(\"ERROR: Backend process exceeded hard timeout (30 minutes) and was terminated.\");",
    "        if (timedOut)\n        {\n            outputCallback?.Invoke(\"ERROR: Backend process exceeded hard timeout (30 minutes) and was terminated.\");\n        }"
)

# 2. Add braces to if (timedOut) at line 55
content = content.replace(
    "        if (timedOut)\n            throw new InvalidOperationException(\"Backend process exceeded hard timeout (30 minutes) and was terminated.\");",
    "        if (timedOut)\n        {\n            throw new InvalidOperationException(\"Backend process exceeded hard timeout (30 minutes) and was terminated.\");\n        }"
)

# 3. Add braces to BuildArguments if statements
if_replacements = [
    ("        if (config.AddComments) source.Add(\"-c\");", "        if (config.AddComments)\n        {\n            source.Add(\"-c\");\n        }"),
    ("        if (config.FixDependencies) source.Add(\"-f\");", "        if (config.FixDependencies)\n        {\n            source.Add(\"-f\");\n        }"),
    ("        if (config.CreateTemplateFolders) source.Add(\"-t\");", "        if (config.CreateTemplateFolders)\n        {\n            source.Add(\"-t\");\n        }"),
    ("        if (!config.ModOpsWrap) source.Add(\"--no-modops-wrap\");", "        if (!config.ModOpsWrap)\n        {\n            source.Add(\"--no-modops-wrap\");\n        }"),
    ("        if (!config.IncludeDefaultProperties) source.Add(\"--no-default-properties\");", "        if (!config.IncludeDefaultProperties)\n        {\n            source.Add(\"--no-default-properties\");\n        }"),
    ("        if (config.SplitTemplates) source.Add(\"--split-templates\");", "        if (config.SplitTemplates)\n        {\n            source.Add(\"--split-templates\");\n        }"),
    ("        if (config.CreateAssetMods) source.Add(\"--create-asset-mods\");", "        if (config.CreateAssetMods)\n        {\n            source.Add(\"--create-asset-mods\");\n        }"),
    ("        if (config.SourceExtractionOnly) source.Add(\"--source-extraction-only\");", "        if (config.SourceExtractionOnly)\n        {\n            source.Add(\"--source-extraction-only\");\n        }"),
    ("        if (!string.IsNullOrEmpty(config.SingleGuid)) source.Add(\"-g:\" + config.SingleGuid);", "        if (!string.IsNullOrEmpty(config.SingleGuid))\n        {\n            source.Add(\"-g:\" + config.SingleGuid);\n        }"),
    ("        if (config.DebugMode) source.Add(\"-d\");", "        if (config.DebugMode)\n        {\n            source.Add(\"-d\");\n        }"),
    ("        if (!string.IsNullOrWhiteSpace(config.ReadmeLanguage))\n            source.Add(\"--readme-lang:\" + config.ReadmeLanguage.ToLowerInvariant());", "        if (!string.IsNullOrWhiteSpace(config.ReadmeLanguage))\n        {\n            source.Add(\"--readme-lang:\" + config.ReadmeLanguage.ToLowerInvariant());\n        }"),
]

for old, new in if_replacements:
    content = content.replace(old, new)

# 4. Make config variable const in conditional compilation
content = content.replace(
    "#if DEBUG\n        string config = \"Debug\";\n#else\n        string config = \"Release\";\n#endif",
    "#if DEBUG\n        const string Config = \"Debug\";\n#else\n        const string Config = \"Release\";\n#endif"
)

# 5. Replace 'config' usage with 'Config'
content = content.replace(
    'Path.Combine(devRoot, "src", "AssetSplitter", "bin", config, tfm, "AssetProcessor.exe")',
    'Path.Combine(devRoot, "src", "AssetSplitter", "bin", Config, tfm, "AssetProcessor.exe")'
)
content = content.replace(
    'Path.Combine(altDevRoot, "src", "AssetSplitter", "bin", config, tfm, "AssetProcessor.exe")',
    'Path.Combine(altDevRoot, "src", "AssetSplitter", "bin", Config, tfm, "AssetProcessor.exe")'
)

# 6. Change var candidates to List<string> candidates with collection expression
content = content.replace(
    "        var candidates = new List<string>\n        {\n            Path.Combine(baseDir, \"AssetProcessor.exe\"),\n            Path.Combine(AppContext.BaseDirectory, \"AssetProcessor.exe\"),\n            Path.Combine(baseDir, \"..\", \"AssetProcessor.exe\"),\n        };",
    "        List<string> candidates =\n        [\n            Path.Combine(baseDir, \"AssetProcessor.exe\"),\n            Path.Combine(AppContext.BaseDirectory, \"AssetProcessor.exe\"),\n            Path.Combine(baseDir, \"..\", \"AssetProcessor.exe\"),\n        ];"
)

# 7. Add braces to foreach in CreateStartInfo
content = content.replace(
    "        foreach (string argument in arguments)\n            startInfo.ArgumentList.Add(argument);",
    "        foreach (string argument in arguments)\n        {\n            startInfo.ArgumentList.Add(argument);\n        }"
)

# 8. Add braces to lock in StartOutputReader
content = content.replace(
    "                    lock (syncRoot) output.AppendLine(line);",
    "                    lock (syncRoot)\n                    {\n                        output.AppendLine(line);\n                    }"
)

# 9. Add braces to if (File.Exists(candidate))
content = content.replace(
    "            if (File.Exists(candidate))\n                return Path.GetFullPath(candidate);",
    "            if (File.Exists(candidate))\n            {\n                return Path.GetFullPath(candidate);\n            }"
)

# 10. Move CancellationToken to last parameter in WaitForExitOrCancellationAsync
content = content.replace(
    "    private static async ValueTask<bool> WaitForExitOrCancellationAsync(\n        Process process, CancellationToken cancellationToken, Action<string>? outputCallback)",
    "    private static async ValueTask<bool> WaitForExitOrCancellationAsync(\n        Process process, Action<string>? outputCallback, CancellationToken cancellationToken)"
)

# 11. Update the call to WaitForExitOrCancellationAsync
content = content.replace(
    "bool wasCancelled = await WaitForExitOrCancellationAsync(process, runCancellationToken, outputCallback);",
    "bool wasCancelled = await WaitForExitOrCancellationAsync(process, outputCallback, runCancellationToken);"
)

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)

print("GuiProcessRunner fixes applied")
