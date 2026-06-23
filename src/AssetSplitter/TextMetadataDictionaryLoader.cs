using System.Xml;

namespace AssetProcessor;

public static class TextMetadataDictionaryLoader
{
    public const string MetadataFileName = "texts_metadata.xml";

    public static int Load(PipelineContext context)
    {
        if (!GameTypeDetector.IsAnno117(context.DetectedGameType))
        {
            return 1;
        }

        string path = context.SourceXmlFolder + MetadataFileName;
        if (!File.Exists(path))
        {
            if (context.DebugMode)
            {
                context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("textMetadataMissing"), path));
            }

            return 0;
        }

        if (context.DebugMode)
        {
            context.Log.Write("TRANS", ConsoleMessages.Get("textMetadataMappingsLoading"));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugTextMetadataSource"), path));
        }

        XmlDocument doc = new();
        try
        {
            doc.LoadXml(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            if (context.DebugMode)
            {
                context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("textMetadataLoadFailed"), ex.Message));
            }

            return 0;
        }

        int added = 0;
        XmlNodeList? entries = doc.DocumentElement?.SelectNodes("//Texts/Text");
        if (entries is not null)
        {
            foreach (XmlNode textNode in entries)
            {
                string? lineId = textNode.SelectSingleNode("LineId")?.InnerText?.Trim();
                string? name = textNode.SelectSingleNode("Name")?.InnerText?.Trim();
                if (string.IsNullOrEmpty(lineId) || string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (context.MetadataNames.TryAdd(lineId, name))
                {
                    added++;
                }
            }
        }

        if (context.DebugMode)
        {
            context.Log.Write("COMPLETE", string.Format(ConsoleMessages.Get("textMetadataDictionaryComplete"), added.ToString("N0")));
        }
        else if (added > 0)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("textMetadataDictionaryComplete"), added.ToString("N0")));
        }

        return 1;
    }
}
