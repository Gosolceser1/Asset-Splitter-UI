using System.Reflection;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.Tests;

/// <summary>
/// Wave 1 UI-side defensive security gauntlet (safe PoCs in temp / reflection only).
/// </summary>
public sealed class GauntletSecurityUiTests
{
    [Fact]
    public void BuildArguments_ShouldValidateSingleGuidAndReadmeLanguageBeforeEmit()
    {
        // Secure expectation: GUI should not emit backend flags with invalid values.
        // Currently fails — BuildArguments concatenates unchecked strings (ArgumentList mitigates shell injection).
        var config = new AssetProcessorRunConfig
        {
            GamePath = @"C:\Games\Anno",
            OutputPath = @"C:\AnnoAssets",
            Language = "english",
            ReadmeLanguage = "../evil",
            SingleGuid = "not-a-digit",
            CreateAssetMods = true,
        };

        Assert.ThrowsAny<Exception>(() => GuiProcessRunner.BuildArguments(config, "english"));
    }

    [Fact]
    public void BuildArguments_UsesSeparateArgumentListEntries_NotConcatenatedShellString()
    {
        var config = new AssetProcessorRunConfig
        {
            GamePath = @"C:\Games\Anno & calc.exe",
            OutputPath = @"C:\AnnoAssets",
            Language = "english",
        };

        List<string> args = GuiProcessRunner.BuildArguments(config, "english");
        Assert.Contains(@"C:\Games\Anno & calc.exe", args);
        Assert.DoesNotContain("&", string.Join(' ', args.Where(a => a != @"C:\Games\Anno & calc.exe")));
    }

    [Fact]
    public void ResolveAssetProcessorPath_PrefersExeBesideUiBaseDirectory()
    {
        MethodInfo? method = typeof(GuiProcessRunner).GetMethod(
            "ResolveAssetProcessorPath",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        // Document search order via source: baseDir\AssetProcessor.exe is checked first.
        // Planting a same-named binary next to the UI would win over the real build output.
        string source = File.ReadAllText(
            FindRepoFile("src", "AssetSplitterUI", "Services", "GuiProcessRunner.cs"));
        Assert.Contains("Path.Combine(baseDir, \"AssetProcessor.exe\")", source, StringComparison.Ordinal);
        int first = source.IndexOf("Path.Combine(baseDir, \"AssetProcessor.exe\")", StringComparison.Ordinal);
        int dev = source.IndexOf("src\", \"AssetSplitter\", \"bin\"", StringComparison.Ordinal);
        Assert.True(first >= 0 && (dev < 0 || first < dev),
            "UI-adjacent AssetProcessor.exe is resolved before the known-good bin path (hijack surface).");
    }

    [Fact]
    public void OpenFolderAsync_PassesCallerPathUncheckedToProcessArgumentList()
    {
        string source = File.ReadAllText(
            FindRepoFile("src", "AssetSplitterUI", "Services", "PlatformServices.cs"));
        Assert.Contains("ArgumentList = { path }", source, StringComparison.Ordinal);
        Assert.Contains("FileName = fileManager", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFullPath(path)", source, StringComparison.Ordinal);
    }

    private static string FindRepoFile(params string[] parts)
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            string candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            string? parent = Directory.GetParent(dir)?.FullName;
            if (parent is null)
            {
                break;
            }

            dir = parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
