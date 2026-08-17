using AssetSplitterUI.Services;

namespace AssetSplitterUI.Tests;

public sealed class GameLanguageIdNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  English  ", "english")]
    [InlineData("texts_german.xml", "german")]
    [InlineData("brazilian_portuguese", "brazilian")]
    [InlineData("mexican-spanish", "mexican")]
    [InlineData("Chinese (Simplified)", "chinese")]
    [InlineData("traditional_chinese", "tchinese")]
    public void Normalize_MapsAliases(string? input, string expected)
    {
        Assert.Equal(expected, GameLanguageIdNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("english", "English")]
    [InlineData("tchinese", "Tchinese")]
    public void Capitalize_UppercasesFirstLetter(string input, string expected)
    {
        Assert.Equal(expected, GameLanguageIdNormalizer.Capitalize(input));
    }
}
