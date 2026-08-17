using AssetProcessor;

namespace AssetSplitter.Tests;

public sealed class ProcessingRunPolicyTests
{
    [Fact]
    public void IsSourceExtractionOnly_True_WhenAllProcessingTogglesOff()
    {
        ProcessingFlags flags = ProcessingRunPolicy.NormalizeFromGui(
            singleGuid: "",
            addComments: false,
            fixDependencies: false,
            createTemplateFolders: false,
            splitTemplates: false,
            createAssetMods: false,
            includeDefaultProperties: false);

        Assert.True(ProcessingRunPolicy.IsSourceExtractionOnly(flags));
    }

    [Fact]
    public void IsSourceExtractionOnly_False_WhenAnyProcessingToggleOn()
    {
        ProcessingFlags flags = ProcessingRunPolicy.NormalizeFromGui(
            singleGuid: "",
            addComments: true,
            fixDependencies: false,
            createTemplateFolders: false,
            splitTemplates: false,
            createAssetMods: false,
            includeDefaultProperties: false);

        Assert.False(ProcessingRunPolicy.IsSourceExtractionOnly(flags));
    }
}
