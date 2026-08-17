using AssetProcessor;

namespace AssetSplitter.Tests;

public sealed class GameBuildDetectorTests
{
    [Fact]
    public void NormalizeBinaryScanText_CollapsesNullPadding()
    {
        string raw = "Win64 \0Goldmaster\0\0Static\0\0Release-\0Steam \0Anno7 \0\018.4.1412158.165273 main";
        string normalized = GameBuildDetector.NormalizeBinaryScanText(raw);
        Assert.Contains("Goldmaster", normalized, StringComparison.Ordinal);
        Assert.Contains("18.4.1412158.165273", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParseGoldmasterVersion_ReadsAnno1800Layout()
    {
        string normalized =
            "Win64 Goldmaster Static Release- Steam Anno7 18.4.1412158.165273 main";
        Assert.True(GameBuildDetector.TryParseGoldmasterVersion(normalized, @"C:\fake\Anno1800.exe", out GameBuildInfo? info));
        Assert.NotNull(info);
        Assert.Equal("18.4.1412158.165273", info!.PatchVersion);
        Assert.Equal("Goldmaster", info.Branch);
        Assert.Equal("v18.4.1412158.165273 Goldmaster", info.ToDisplayString());
    }

    [Fact]
    public void TryParseDocumentsBuildString_ReadsLogLine()
    {
        string line =
            "[21:39:54] BuildString: Developer build - for internal use only! " +
            "[Build: 2024-11-11 14:14:36] Anno7 Steam Goldmaster Win64 Release-Static v18.4.1412158.165273 main";
        GameBuildInfo? info = GameBuildDetector.TryParseDocumentsBuildString(line, @"C:\docs\log.txt");
        Assert.NotNull(info);
        Assert.Equal("18.4.1412158.165273", info!.PatchVersion);
        Assert.Equal("2024-11-11 14:14:36", info.BuildTime);
        Assert.Equal("v18.4.1412158.165273 Goldmaster", info.ToDisplayString());
    }

    [Fact]
    public void ToDisplayString_DoesNotPrefixNonNumericLabels()
    {
        var info = new GameBuildInfo("", "Steam buildid 16367353", "", "", @"C:\steam\appmanifest.acf");
        Assert.Equal("Steam buildid 16367353", info.ToDisplayString());
    }

    [Fact]
    public void TryReadSteamBuildId_ParsesManifestNextToCommon()
    {
        string root = Path.Combine(Path.GetTempPath(), "assetsplitter-steam-" + Guid.NewGuid().ToString("N"));
        string steamApps = Path.Combine(root, "steamapps");
        string install = Path.Combine(steamApps, "common", "Anno 1800");
        Directory.CreateDirectory(install);
        try
        {
            File.WriteAllText(
                Path.Combine(steamApps, "appmanifest_916440.acf"),
                """
                "AppState"
                {
                	"appid"		"916440"
                	"buildid"		"16367353"
                	"name"		"Anno 1800"
                }
                """);

            GameBuildInfo? info = GameBuildDetector.TryReadSteamBuildId(install, "anno1800");
            Assert.NotNull(info);
            Assert.Equal("Steam buildid 16367353", info!.ToDisplayString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
