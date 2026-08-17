using AssetProcessor;

namespace AssetSplitter.Tests;

public sealed class AssetTextSanitizerTests
{
    [Fact]
    public void FormatAssetFileName_SanitizesPathTraversalInGuid()
    {
        string name = AssetTextSanitizer.FormatAssetFileName("..\\..\\evil", "Coins", "Product", "");
        Assert.DoesNotContain("\\", name, StringComparison.Ordinal);
        Assert.DoesNotContain("/", name, StringComparison.Ordinal);
        Assert.DoesNotContain("..", name, StringComparison.Ordinal);
        Assert.EndsWith(".xml", name, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatAssetFileName_KeepsNumericGuid()
    {
        string name = AssetTextSanitizer.FormatAssetFileName("1010017", "Coins", "Product", "");
        Assert.StartsWith("1010017 - [", name, StringComparison.Ordinal);
    }
}

public sealed class OutputPathSafetyTests
{
    [Fact]
    public void IsPathUnderDirectory_RejectsSiblingPrefixTrick()
    {
        string root = Path.Combine(Path.GetTempPath(), "assetsplitter-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string outside = root + "extra" + Path.DirectorySeparatorChar + "file.xml";
            Assert.False(OutputPathSafety.IsPathUnderDirectory(outside, root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SanitizeSinglePathSegment_RejectsTraversal()
    {
        Assert.Equal("BaseAssetGUID", OutputPathSafety.SanitizeSinglePathSegment("..\\..\\escape", "BaseAssetGUID"));
        Assert.Equal("BaseAssetGUID", OutputPathSafety.SanitizeSinglePathSegment("../x", "BaseAssetGUID"));
        Assert.Equal("SafeFolder", OutputPathSafety.SanitizeSinglePathSegment("SafeFolder", "BaseAssetGUID"));
    }

    [Fact]
    public void TryGetContainedPath_BlocksEscape()
    {
        string root = Path.Combine(Path.GetTempPath(), "assetsplitter-contain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.False(OutputPathSafety.TryGetContainedPath(root, "..\\..\\Windows\\system.ini", out _));
            Assert.True(OutputPathSafety.TryGetContainedPath(root, "child\\file.xml", out string full));
            Assert.True(OutputPathSafety.IsPathUnderDirectory(full, root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

public sealed class CommandLineLanguageTests
{
    [Fact]
    public void Parse_AcceptsNormalLanguage()
    {
        var options = AssetProcessorCommandLineOptions.Parse(
        [
            @"C:\Games\Anno",
            @"C:\AnnoAssets",
            "english"
        ]);
        Assert.Equal("texts_english.xml", options.AssetLanguage);
    }

    [Fact]
    public void Parse_RejectsLanguagePathEscape()
    {
        Assert.Throws<ArgumentException>(() => AssetProcessorCommandLineOptions.Parse(
        [
            @"C:\Games\Anno",
            @"C:\AnnoAssets",
            "../evil"
        ]));
    }
}

public sealed class PipelineIssueExitCodeTests
{
    [Fact]
    public void ResolveExitCode_ReturnsOneWhenErrorsExist()
    {
        var context = new PipelineContext();
        context.Issues.ReportFormatFileFailed(@"C:\x.xml", "boom");
        Assert.True(context.Issues.HasErrors);
        Assert.Equal(1, PipelineRunSupport.ResolveExitCode(context, 0));
    }

    [Fact]
    public void ResolveExitCode_KeepsNonZeroOrchestratorCode()
    {
        var context = new PipelineContext();
        Assert.Equal(2, PipelineRunSupport.ResolveExitCode(context, 2));
    }
}

public sealed class PipelineFeatureGatesTests
{
    [Fact]
    public void NeedsGuidIndex_TrueForCommentsOrDependencies()
    {
        var comments = new PipelineContext { AddComments = true };
        var deps = new PipelineContext { FixDependencies = true };
        var neither = new PipelineContext();
        Assert.True(PipelineFeatureGates.NeedsGuidIndex(comments));
        Assert.True(PipelineFeatureGates.NeedsGuidIndex(deps));
        Assert.False(PipelineFeatureGates.NeedsGuidIndex(neither));
    }
}
