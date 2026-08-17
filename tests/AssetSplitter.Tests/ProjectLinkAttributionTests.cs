using AssetProcessor;

namespace AssetSplitter.Tests;

public sealed class ProjectLinkAttributionTests
{
    [Fact]
    public void WriteAnnoAssetsReadme_IncludesGitHubProjectAndReleasesLinks()
    {
        string root = Path.Combine(Path.GetTempPath(), "as-link-" + Guid.NewGuid().ToString("N"));
        try
        {
            ModReadmeWriter.WriteAnnoAssetsReadme(root);
            string readme = File.ReadAllText(Path.Combine(root, "README.md"));
            Assert.Contains(ModReadmeWriter.ProjectHomeUrl, readme, StringComparison.Ordinal);
            Assert.Contains(ModReadmeWriter.ProjectReleasesUrl, readme, StringComparison.Ordinal);
            Assert.Contains("Asset Splitter UI", readme, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ConsoleDefaults_IncludeToolAttributionAndAssetSplitterReference()
    {
        Assert.Contains(
            ModReadmeWriter.ProjectHomeUrl,
            string.Format(ConsoleMessages.Get("readmeToolAttribution"), ModReadmeWriter.ProjectHomeUrl),
            StringComparison.Ordinal);
        Assert.Contains(
            ModReadmeWriter.ProjectHomeUrl,
            ConsoleMessages.Get("readmeRefAssetSplitter"),
            StringComparison.Ordinal);
        Assert.Contains(
            ModReadmeWriter.ProjectHomeUrl,
            string.Format(ConsoleMessages.Get("modDescriptionToolSuffix"), ModReadmeWriter.ProjectHomeUrl),
            StringComparison.Ordinal);
    }
}
