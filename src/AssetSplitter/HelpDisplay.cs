namespace AssetProcessor;

public static class HelpDisplay
{
    public static void Short()
    {
        Console.WriteLine("");
        Console.WriteLine("---------------------------------------------");
        Console.WriteLine(ConsoleMessages.Get("assetSplitVersion"));
        Console.WriteLine(ConsoleMessages.Get("enhancedEdition"));
        Console.WriteLine("---------------------------------------------");
        Console.WriteLine(ConsoleMessages.Get("syntaxLabel"));
        Console.WriteLine(ConsoleMessages.Get("helpShortSyntax"));
        Console.WriteLine("");
        Console.WriteLine(ConsoleMessages.Get("helpCommand"));
        Console.WriteLine("");
    }

    public static void Long()
    {
        Console.WriteLine("");
        AssetProcessorConsole.WriteColoredMessage("╔══════════════════════════════════════════════════════════════════════════════╗", "HEADER");
        AssetProcessorConsole.WriteColoredMessage(ConsoleMessages.Get("helpBannerTitle"), "HEADER");
        AssetProcessorConsole.WriteColoredMessage(ConsoleMessages.Get("helpBannerCredit"), "INFO");
        AssetProcessorConsole.WriteColoredMessage(ConsoleMessages.Get("helpBannerEdition"), "INFO");
        AssetProcessorConsole.WriteColoredMessage("╚══════════════════════════════════════════════════════════════════════════════╝", "HEADER");
        Console.WriteLine("");
        Console.WriteLine(ConsoleMessages.Get("helpUsage"));
        Console.WriteLine(ConsoleMessages.Get("helpSyntaxLine"));
        Console.WriteLine("");
        Console.WriteLine(ConsoleMessages.Get("helpRequiredParameters"));
        Console.WriteLine(ConsoleMessages.Get("helpParamSource"));
        Console.WriteLine(ConsoleMessages.Get("helpParamOutput"));
        Console.WriteLine(ConsoleMessages.Get("helpParamLanguage"));
        Console.WriteLine("");
        Console.WriteLine(ConsoleMessages.Get("helpOptions"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionComments"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionDependencies"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionTemplates"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionDebug"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionOverwrite"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionCustomTemplates"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionCustomFixlist"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionSingleGuid"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionNoModOpsWrap"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionNoDefaultProperties"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionSplitTemplates"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionCreateAssetMods"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionAutoTemplates"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionUpdateTemplates"));
        Console.WriteLine(ConsoleMessages.Get("helpOptionCompareTemplates"));
        Console.WriteLine("");
        Console.WriteLine(ConsoleMessages.Get("helpExamples"));
        Console.WriteLine("");
        Console.WriteLine("  " + ConsoleMessages.Get("basicExtraction"));
        Console.WriteLine(ConsoleMessages.Get("helpExampleBasic"));
        Console.WriteLine("");
        Console.WriteLine("  " + ConsoleMessages.Get("fullExtraction"));
        Console.WriteLine(ConsoleMessages.Get("helpExampleFull"));
        Console.WriteLine("");
        Console.WriteLine("  " + ConsoleMessages.Get("singleAsset"));
        Console.WriteLine(ConsoleMessages.Get("helpExampleSingle"));
        Console.WriteLine("");
        Console.WriteLine(ConsoleMessages.Get("helpOutputStructure"));
        Console.WriteLine(ConsoleMessages.Get("helpOutputXml"));
        Console.WriteLine(ConsoleMessages.Get("helpSourceXml"));
        Console.WriteLine("");
    }
}
