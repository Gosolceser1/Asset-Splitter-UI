namespace AssetProcessor;

public static class Program
{
    private static int Main(string[] args)
    {
        if (Console.IsOutputRedirected)
        {
            // The GUI parses numbers out of redirected stdout ("[ 12,200/31,565]", "{0:N0}" messages).
            // Culture-specific separators (de-DE ".", fr-FR U+202F) would break that parsing.
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        }

        return PipelineOrchestrator.Run(args);
    }
}
