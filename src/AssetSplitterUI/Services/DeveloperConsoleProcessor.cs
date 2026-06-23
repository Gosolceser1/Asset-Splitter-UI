namespace AssetSplitterUI.Services;

/// <summary>Shapes backend stdout for developer mode: keep milestones, collapse repetitive trace noise.</summary>
internal static class DeveloperConsoleProcessor
{
    internal readonly record struct ProcessedLine(string Text, bool SuppressDisplay);

    public static ProcessedLine Process(string rawLine, bool debugMode)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return new ProcessedLine(rawLine, SuppressDisplay: true);
        }

        if (!debugMode)
        {
            return new ProcessedLine(rawLine, SuppressDisplay: false);
        }

        string line = rawLine;

        if (line.StartsWith("[PLAN]", StringComparison.Ordinal))
        {
            string num = line.AsSpan(6).Trim().ToString();
            return new ProcessedLine(
                $"[PLAN] {num} — work units for the progress bar (GUID index + merge + format + mods)",
                SuppressDisplay: false);
        }

        if (line.StartsWith("=== PHASE", StringComparison.Ordinal))
        {
            return new ProcessedLine(line, SuppressDisplay: false);
        }

        if (ShouldHideInDeveloperUi(line))
        {
            return new ProcessedLine(line, SuppressDisplay: true);
        }

        return new ProcessedLine(line, SuppressDisplay: false);
    }

    /// <summary>Groups consecutive repetitive lines in the on-screen console (×N).</summary>
    public static string? GetCollapseGroup(string displayText)
    {
        if (displayText.Contains("[DEBUG] Creating mod package for:", StringComparison.Ordinal))
        {
            return "mod-create";
        }

        if (displayText.Contains("[DEBUG] Mod package created:", StringComparison.Ordinal))
        {
            return "mod-created";
        }

        if (displayText.Contains("[DEBUG] MERGING: Template", StringComparison.Ordinal))
        {
            return "merge-template";
        }

        if (displayText.Contains("[DEBUG] SKIPPING: Template", StringComparison.Ordinal))
        {
            return "merge-skip";
        }

        if (displayText.Contains("[DEBUG] STEP 4: Moving file to template folder", StringComparison.Ordinal))
        {
            return "format-move";
        }

        if (displayText.Contains("[DEBUG] Added ", StringComparison.Ordinal) && displayText.Contains("translated comments", StringComparison.Ordinal))
        {
            return "format-comments";
        }

        if (displayText.Contains("[DEBUG] No Template node found", StringComparison.Ordinal))
        {
            return "format-no-template";
        }

        if (displayText.Contains("[CACHE] Parent asset", StringComparison.Ordinal))
        {
            return "dep-cache";
        }

        if (displayText.Contains("[DEBUG][MERGE] Built ", StringComparison.Ordinal))
        {
            return "merge-xpath";
        }

        if (displayText.Contains("[DEBUG] Element: <", StringComparison.Ordinal))
        {
            return "format-element";
        }

        return null;
    }

    public static string BuildCollapsedSummary(string groupKey, int count, string sampleLine) =>
        groupKey switch
        {
            "mod-create" => $"[DEBUG] … ×{count:N0} mod packages started (e.g. {SampleTail(sampleLine, "for: ")})",
            "mod-created" => $"[DEBUG] … ×{count:N0} mod packages finished (e.g. {SampleTail(sampleLine, "created: ")})",
            "merge-template" => $"[DEBUG] … ×{count:N0} templates merged from fixlist (e.g. {SampleTail(sampleLine, "MERGING: ")})",
            "merge-skip" => $"[DEBUG] … ×{count:N0} assets kept basic (not in fixlist)",
            "format-move" => $"[DEBUG] … ×{count:N0} files moved into template folders (-t)",
            "format-comments" => $"[DEBUG] … ×{count:N0} files received GUID comment annotations",
            "format-no-template" => $"[DEBUG] … ×{count:N0} files had no Template node (continued)",
            "dep-cache" => $"[DEBUG] … ×{count:N0} parent GUID cache lookups",
            "merge-xpath" => $"[DEBUG] … ×{count:N0} per-file merge xpath traces",
            "format-element" => $"[DEBUG] … ×{count:N0} element translation traces",
            _ => $"[DEBUG] … ×{count:N0} {sampleLine}"
        };

    private static string SampleTail(string line, string after)
    {
        int index = line.IndexOf(after, StringComparison.Ordinal);
        if (index < 0)
        {
            return TrimSample(line);
        }

        return TrimSample(line[(index + after.Length)..]);
    }

    private static string TrimSample(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Length <= 72 ? trimmed : trimmed[..69] + "...";
    }

    private static bool ShouldHideInDeveloperUi(string line) =>
        line.Contains("[DEBUG] Flags applied:", StringComparison.Ordinal) ||
        line.StartsWith("[DEBUG] args[", StringComparison.Ordinal) ||
        line.StartsWith("[DEBUG] Total arguments:", StringComparison.Ordinal) ||
        line.Contains("singleGuidFilter='' length=0", StringComparison.Ordinal);
}
