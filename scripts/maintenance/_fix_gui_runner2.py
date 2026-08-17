path = r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\src\AssetSplitterUI\Services\GuiProcessRunner.cs'
with open(path, encoding='utf-8') as f:
    content = f.read()

# 1. Add braces to if (timedOut) line 52
content = content.replace(
    "            if (timedOut)\n                outputCallback?.Invoke(\"ERROR: Backend process exceeded hard timeout (30 minutes) and was terminated.\");",
    "            if (timedOut)\n            {\n                outputCallback?.Invoke(\"ERROR: Backend process exceeded hard timeout (30 minutes) and was terminated.\");\n            }"
)

# 2. Add braces to if (timedOut) line 57
content = content.replace(
    "            if (timedOut)\n                throw new InvalidOperationException(\"Backend process exceeded hard timeout (30 minutes) and was terminated.\");",
    "            if (timedOut)\n            {\n                throw new InvalidOperationException(\"Backend process exceeded hard timeout (30 minutes) and was terminated.\");\n            }"
)

# 3. Move CancellationToken to last in EnsureSuccessfulExit
content = content.replace(
    "    private static void EnsureSuccessfulExit(\n        Process process, bool wasCancelled, CancellationToken cancellationToken,\n        StringBuilder stdoutBuilder, Lock stdoutLock, StringBuilder stderrBuilder, Lock stderrLock)",
    "    private static void EnsureSuccessfulExit(\n        Process process, bool wasCancelled,\n        StringBuilder stdoutBuilder, Lock stdoutLock, StringBuilder stderrBuilder, Lock stderrLock,\n        CancellationToken cancellationToken)"
)

# 4. Update the call to EnsureSuccessfulExit
content = content.replace(
    "        EnsureSuccessfulExit(process, wasCancelled, userCancellationToken, stdoutBuilder, stdoutLock, stderrBuilder, stderrLock);",
    "        EnsureSuccessfulExit(process, wasCancelled, stdoutBuilder, stdoutLock, stderrBuilder, stderrLock, userCancellationToken);"
)

# 5. Add braces to if (wasCancelled)
content = content.replace(
    "        if (wasCancelled) cancellationToken.ThrowIfCancellationRequested();",
    "        if (wasCancelled)\n        {\n            cancellationToken.ThrowIfCancellationRequested();\n        }"
)

# 6. Add braces to if (process.ExitCode == 0) return;
content = content.replace(
    "        if (process.ExitCode == 0) return;",
    "        if (process.ExitCode == 0)\n        {\n            return;\n        }"
)

# 7. Add braces to lock statements
content = content.replace(
    "        lock (stderrLock) stderrOutput = stderrBuilder.ToString();\n        lock (stdoutLock) stdoutOutput = stdoutBuilder.ToString();",
    "        lock (stderrLock)\n        {\n            stderrOutput = stderrBuilder.ToString();\n        }\n        lock (stdoutLock)\n        {\n            stdoutOutput = stdoutBuilder.ToString();\n        }"
)

# 8. Add braces to if (string.IsNullOrEmpty(errorMessage))
content = content.replace(
    "        if (string.IsNullOrEmpty(errorMessage))\n            errorMessage = $\"AssetSplit process exited with code {process.ExitCode}\";",
    "        if (string.IsNullOrEmpty(errorMessage))\n        {\n            errorMessage = $\"AssetSplit process exited with code {process.ExitCode}\";\n        }"
)

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)

print("GuiProcessRunner fixes 2 applied")
