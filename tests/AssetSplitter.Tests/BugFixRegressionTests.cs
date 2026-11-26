using AssetProcessor;

namespace AssetSplitter.Tests;

/// <summary>Regression tests for fixes from the static bug review.</summary>
public sealed class BugFixRegressionTests
{
    [Theory]
    [InlineData("-g")]
    [InlineData("-g", "-c")]
    public void Parse_GuidOptionWithoutValue_Throws(params string[] options)
    {
        string[] args = ["game", "out", "english", .. options];
        Assert.Throws<ArgumentException>(() => AssetProcessorCommandLineOptions.Parse(args));
    }

    [Fact]
    public void Parse_GuidOptionWithValue_IsAccepted()
    {
        AssetProcessorCommandLineOptions options = AssetProcessorCommandLineOptions.Parse(["game", "out", "english", "-g", "1010017"]);
        Assert.Equal("1010017", options.SingleAssetGuid);
    }

    [Theory]
    [InlineData(0, 5, 7, "05:07")]
    [InlineData(1, 5, 0, "1:05:00")]
    [InlineData(12, 0, 9, "12:00:09")]
    public void FormatElapsed_DoesNotWrapAfterOneHour(int hours, int minutes, int seconds, string expected)
    {
        Assert.Equal(expected, PipelineRunSupport.FormatElapsed(new TimeSpan(hours, minutes, seconds)));
    }

    [Theory]
    [InlineData("Inherits: A 1 Name---x", "Inherits: A 1 Name-x")]
    [InlineData("Ends with dash-", "Ends with dash- ")]
    [InlineData("plain", "plain")]
    public void ToSafeXmlCommentBody_ProducesValidCommentText(string input, string expected)
    {
        string result = AssetTextSanitizer.ToSafeXmlCommentBody(input);
        Assert.Equal(expected, result);
        Assert.DoesNotContain("--", result, StringComparison.Ordinal);
        Assert.False(result.EndsWith('-'));

        var doc = new System.Xml.XmlDocument();
        doc.AppendChild(doc.CreateElement("root"));
        doc.DocumentElement!.AppendChild(doc.CreateComment(result));
        Assert.Null(Record.Exception(() => doc.Save(new StringWriter())));
    }
}
