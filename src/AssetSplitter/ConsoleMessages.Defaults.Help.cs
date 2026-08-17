namespace AssetProcessor;

public static partial class ConsoleMessages
{
    private static void AddHelpDefaults(Dictionary<string, string> messages)
    {
        messages["assetSplitVersion"] = "AssetSplit v1.0 - Based on Pogobuckel's work";
        messages["enhancedEdition"] = "Enhanced Edition 2022-2026";
        messages["syntaxLabel"] = "Syntax:";
        messages["helpCommand"] = "AssetSplit -h for help";
        messages["helpUsage"] = "USAGE:";
        messages["helpRequiredParameters"] = "REQUIRED PARAMETERS:";
        messages["helpOptions"] = "OPTIONS:";
        messages["helpExamples"] = "EXAMPLES:";
        messages["helpOutputStructure"] = "OUTPUT STRUCTURE:";
        messages["helpShortSyntax"] = "AssetSplit <source> <out> <lang> [-c] [-f] [-t] [-d] [-u:file] [-x:file] [-g:GUID] [--create-asset-mods]";
        messages["helpBannerTitle"] = "║                    AssetSplit v1.0 - Anno 1800 Asset Processor               ║";
        messages["helpBannerCredit"] = "║                     Based on original work by Pogobuckel                     ║";
        messages["helpBannerEdition"] = "║                        Enhanced version - © 2022-2026                        ║";
        messages["helpSyntaxLine"] = "  AssetSplit <source> <output> <language> [options]";
        messages["helpParamSource"] = "  <source>      Anno 1800 installation directory";
        messages["helpParamOutput"] = "  <output>      Target directory for extracted assets";
        messages["helpParamLanguage"] = "  <language>    Localization: english, french, german, italian, polish, spanish, russian, chinese, japanese, korean";
        messages["helpOptionComments"] = "  -c            Add translated GUID comments to XML files";
        messages["helpOptionDependencies"] = "  -f            Resolve BaseAssetGUID dependencies (complete assets)";
        messages["helpOptionTemplates"] = "  -t            Organize files into template-based folders";
        messages["helpOptionDebug"] = "  -d            Enable debug output for troubleshooting";
        messages["helpOptionOverwrite"] = "  -y            (legacy, ignored — output folders are cleared automatically each run)";
        messages["helpOptionCustomTemplates"] = "  -u [file]     Use custom template list (default: used_templates.txt)";
        messages["helpOptionCustomFixlist"] = "  -x [file]     Use custom fixlist file (default: game-specific built-in)";
        messages["helpOptionSingleGuid"] = "  -g [GUID]     Extract single asset by GUID";
        messages["helpOptionNoModOpsWrap"] = "  --no-modops-wrap    Output raw Asset XML without ModOps/ModOp wrapper";
        messages["helpOptionNoDefaultProperties"] = "  --no-default-properties  Split-only: skip template merge (Phase 5) and enrichment cleanup (Phase 7 vector/regional)";
        messages["helpOptionSplitTemplates"] = "  --split-templates   Split templates.xml into per-template files in template-named folders";
        messages["helpOptionCreateAssetMods"] = "  --create-asset-mods Create one ready-to-copy Mod Loader folder per generated asset XML";
        messages["helpOptionForceFullRebuild"] = "  --force-full-rebuild  Ignore output cache; wipe and rebuild all asset XML/mods";
        messages["helpOptionAutoTemplates"] = "  --auto-templates    Auto-extract all templates from game (ignores used_templates.txt)";
        messages["helpOptionUpdateTemplates"] = "  --update-templates  Update template list from game's templates.xml";
        messages["helpOptionCompareTemplates"] = "  --compare-templates Compare config templates with game templates";
        messages["helpExampleBasic"] = "    AssetSplit \"C:\\Program Files\\Ubisoft\\Ubisoft Game Launcher\\games\\Anno 1800\" extracted_assets english";
        messages["helpExampleFull"] = "    AssetSplit \"C:\\Program Files\\Ubisoft\\Ubisoft Game Launcher\\games\\Anno 1800\" my_mods german -c -f -t";
        messages["helpExampleSingle"] = "    AssetSplit \"C:\\Program Files\\Ubisoft\\Ubisoft Game Launcher\\games\\Anno 1800\" test_output english -g 1010017";
        messages["helpOutputXml"] = "  output_xml/           XML files for modding";
        messages["helpSourceXml"] = "  source_xml/           Raw extracted data from Anno 1800";
    }
}
