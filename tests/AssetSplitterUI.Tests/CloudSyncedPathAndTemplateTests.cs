using AssetProcessor;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.Tests;

public sealed class CloudSyncedPathDetectorTests
{
    [Theory]
    [InlineData(@"C:\Users\vadim\OneDrive\Documents\out")]
    [InlineData(@"C:\Users\vadim\OneDrive - Contoso\Projects\assets")]
    [InlineData(@"D:\Dropbox\AnnoAssets")]
    [InlineData(@"C:\Users\vadim\Google Drive\mods")]
    public void IsLikelyCloudSynced_DetectsKnownProviders(string path)
    {
        Assert.True(CloudSyncedPathDetector.IsLikelyCloudSynced(path));
    }

    [Theory]
    [InlineData(@"C:\AnnoAssets")]
    [InlineData(@"D:\Games\Output")]
    [InlineData("")]
    [InlineData(null)]
    public void IsLikelyCloudSynced_ReturnsFalseForLocalOrEmpty(string? path)
    {
        Assert.False(CloudSyncedPathDetector.IsLikelyCloudSynced(path));
    }
}

public sealed class AssetProcessorFileSystemTemplateTests : IDisposable
{
    private readonly string _root;

    public AssetProcessorFileSystemTemplateTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "assetsplitter-template-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void TryReadTemplateFromAssetFile_ReadsTemplateFromHeaderWithoutFullFile()
    {
        string path = Path.Combine(_root, "1010017 - [ Gold ].xml");
        string body = new string('x', 64 * 1024);
        File.WriteAllText(
            path,
            $"<?xml version=\"1.0\"?>\n<ModOps><ModOp><Asset><Template>ResidenceBuilding</Template><Values/></Asset></ModOp></ModOps>\n<!-- {body} -->\n");

        string? template = AssetProcessorFileSystem.TryReadTemplateFromAssetFile(path);

        Assert.Equal("ResidenceBuilding", template);
    }

    [Fact]
    public void TryReadTemplateFromAssetFile_ReturnsNullWhenTemplateMissingInHeaderWindow()
    {
        string path = Path.Combine(_root, "no-template.xml");
        File.WriteAllText(path, "<ModOps><Asset><Name>x</Name></Asset></ModOps>");

        Assert.Null(AssetProcessorFileSystem.TryReadTemplateFromAssetFile(path));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
