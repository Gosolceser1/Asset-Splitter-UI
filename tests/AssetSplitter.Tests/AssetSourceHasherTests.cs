using System.Xml;
using AssetProcessor;

namespace AssetSplitter.Tests;

public sealed class AssetSourceHasherTests
{
    [Fact]
    public void HashAssetNode_IsStable_ForSameContent()
    {
        var doc = new XmlDocument();
        doc.LoadXml("""
            <Asset>
              <Template>ResidenceBuilding</Template>
              <Values><Standard><GUID>101</GUID><Name>House</Name></Standard></Values>
            </Asset>
            """);

        string a = AssetSourceHasher.HashAssetNode(doc.DocumentElement!);
        string b = AssetSourceHasher.HashAssetNode(doc.DocumentElement!);
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
    }

    [Fact]
    public void HashAssetNode_Changes_WhenContentChanges()
    {
        var aDoc = new XmlDocument();
        aDoc.LoadXml("<Asset><Values><Standard><GUID>1</GUID><Name>A</Name></Standard></Values></Asset>");
        var bDoc = new XmlDocument();
        bDoc.LoadXml("<Asset><Values><Standard><GUID>1</GUID><Name>B</Name></Standard></Values></Asset>");

        Assert.NotEqual(
            AssetSourceHasher.HashAssetNode(aDoc.DocumentElement!),
            AssetSourceHasher.HashAssetNode(bDoc.DocumentElement!));
    }

    [Fact]
    public void ExpandDescendantClosure_WalksChildren()
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = ["2", "3"],
            ["2"] = ["4"],
        };

        HashSet<string> closure = AssetSourceHasher.ExpandDescendantClosure(["1"], map);
        Assert.Contains("1", closure);
        Assert.Contains("2", closure);
        Assert.Contains("3", closure);
        Assert.Contains("4", closure);
    }
}
