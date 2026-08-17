namespace AssetProcessor;

public static partial class ConsoleMessages
{
    private static void AddPhaseDefaults(Dictionary<string, string> messages)
    {
        messages["phase1Label"] = "=== PHASE 1: RDA EXPLORER ===";
        messages["phase2Label"] = "=== PHASE 2: ASSET SPLITTER ===";
        messages["phase2bLabel"] = "=== PHASE 2B: TEMPLATE PROPERTY INHERITANCE ===";
        messages["phase3Label"] = "=== PHASE 3: ASSET EXTRACTION ===";
        messages["phase3SplitTemplatesLabel"] = "=== PHASE 3B: SPLIT TEMPLATES ===";
        messages["phase4GuidIndexLabel"] = "=== PHASE 4: GUID FILE INDEX ===";
        messages["phase5TemplateMergeLabel"] = "=== PHASE 5: TEMPLATE INHERITANCE (MERGE) ===";
        messages["phase6DependencyResolutionLabel"] = "=== PHASE 6: DEPENDENCY RESOLUTION ===";
        messages["phase7FormattingLabel"] = "=== PHASE 7: FORMATTING ===";
        messages["phase7AssetModPackages"] = "=== PHASE 7: ASSET MOD PACKAGES ===";
        messages["phase8AssetModPackages"] = "=== PHASE 8: ASSET MOD PACKAGES ===";
        messages["phaseRdaAnno117"] = "\n=== PHASE 1: Extracting from {0} Anno 117 RDA archives ===";
        messages["phaseRdaAnno1800"] = "\n=== PHASE 1: Extracting from {0} Anno 1800 RDA archives ===";
        messages["phaseGuidIndex"] = "\n=== PHASE 2.5: Building GUID index ===";
    }
}
