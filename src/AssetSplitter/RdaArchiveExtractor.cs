using RDAExplorer;

namespace AssetProcessor;

public static partial class RdaArchiveExtractor
{
    private const int MaxBlockDetailsLogged = 12;

    /// </summary>
    public static void Extract(PipelineContext context, string gameRootPath, string extractFilter, string outputFolder, string gameType)
    {
        if (context.DebugMode)
        {
            WriteDebugSearchPlan(context, gameRootPath, extractFilter, outputFolder, gameType);
        }

        if (gameType.Contains("117", StringComparison.OrdinalIgnoreCase))
        {
            ExtractAnno117(context, gameRootPath, extractFilter, outputFolder);
        }
        else
        {
            ExtractAnno1800(context, gameRootPath, extractFilter, outputFolder);
        }

        if (context.DebugMode && gameType.Contains("117", StringComparison.OrdinalIgnoreCase))
        {
            WriteDebugOutputInventory(context, outputFolder);
        }
    }
}
