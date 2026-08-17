using AssetSplitterUI.Services;

namespace AssetSplitterUI.Tests;

public sealed class SingleGuidAssetLookupTests : IDisposable
{
    private readonly string _root;

    public SingleGuidAssetLookupTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "assetsplitter-guid-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        SingleGuidAssetLookup.InvalidateCache();
    }

    [Fact]
    public void Find_ReturnsFoundForMatchingGuid()
    {
        string assetsPath = WriteAssetsXml(
            """
            <Assets>
              <Asset>
                <Template>TestTemplate</Template>
                <Standard>
                  <GUID>1010017</GUID>
                  <Name>Test Asset</Name>
                </Standard>
              </Asset>
            </Assets>
            """);

        var result = SingleGuidAssetLookup.Find(_root, _root, "anno117", "english", "1010017");

        Assert.Equal(SingleGuidLookupStatus.Found, result.Status);
        Assert.Equal("Test Asset", result.AssetName);
        Assert.Equal("TestTemplate", result.TemplateName);
        Assert.Equal(assetsPath, Directory.GetFiles(_root, "assets.xml", SearchOption.AllDirectories).Single());
    }

    [Fact]
    public void Find_UsesCacheOnSecondLookup()
    {
        WriteAssetsXml(
            """
            <Assets>
              <Asset>
                <Template>T</Template>
                <Standard>
                  <GUID>42</GUID>
                  <Name>Cached Asset</Name>
                </Standard>
              </Asset>
            </Assets>
            """);

        var first = SingleGuidAssetLookup.Find(_root, _root, "anno117", "english", "42");
        var second = SingleGuidAssetLookup.Find(_root, _root, "anno117", "english", "42");

        Assert.Equal(SingleGuidLookupStatus.Found, first.Status);
        Assert.Equal(SingleGuidLookupStatus.Found, second.Status);
        Assert.Equal("Cached Asset", second.AssetName);
    }

    [Fact]
    public void Find_ReturnsNotFoundForMissingGuid()
    {
        WriteAssetsXml(
            """
            <Assets>
              <Asset>
                <Template>T</Template>
                <Standard>
                  <GUID>1</GUID>
                  <Name>Only Asset</Name>
                </Standard>
              </Asset>
            </Assets>
            """);

        var result = SingleGuidAssetLookup.Find(_root, _root, "anno117", "english", "999");

        Assert.Equal(SingleGuidLookupStatus.NotFound, result.Status);
    }

    private string WriteAssetsXml(string xml)
    {
        string sourceFolder = Path.Combine(_root, "AnnoAssets", "Anno117", "source_xml_anno117");
        Directory.CreateDirectory(sourceFolder);
        string assetsPath = Path.Combine(sourceFolder, "assets.xml");
        File.WriteAllText(assetsPath, xml);
        foreach (string required in new[]
                 {
                     "properties.xml", "properties-meta.xml", "templates.xml", "datasets.xml", "audio_generated.xml"
                 })
        {
            File.WriteAllText(Path.Combine(sourceFolder, required), "<Root/>");
        }

        File.WriteAllText(Path.Combine(sourceFolder, "texts_english.xml"), "<Texts><Text><LineId>1</LineId><Text>Name</Text></Text></Texts>");

        return assetsPath;
    }

    public void Dispose()
    {
        SingleGuidAssetLookup.InvalidateCache();
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup for temp test directories.
        }
    }
}
