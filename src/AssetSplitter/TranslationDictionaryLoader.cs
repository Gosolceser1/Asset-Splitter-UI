using System.Xml;

namespace AssetProcessor;

public static class TranslationDictionaryLoader
{
    public static int Load(PipelineContext context)
    {
        if (context.DebugMode)
        {
            context.Log.Write("TRANS", string.Format(ConsoleMessages.Get("translationMappingsLoading"), context.AssetLanguage));
            context.Log.Write("INFO", ConsoleMessages.Get("translationDictionaryBuilding"));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugTransSourceFile"), context.SourceXmlFolder + context.AssetLanguage));
        }
        else
        {
            string languageCode = context.AssetLanguage.Replace("texts_", "").Replace(".xml", "");
            string msg = !string.IsNullOrEmpty(languageCode)
                ? $"{ConsoleMessages.Get("readingLanguageFile")} ({char.ToUpper(languageCode[0]) + languageCode[1..]})..."
                : ConsoleMessages.Get("readingLanguageFile") + "...";
            if (Console.IsOutputRedirected)
                Console.WriteLine(msg);
            else
                Console.Write(msg);
        }

        XmlDocument langDoc = new();
        try
        {
            string xml = File.ReadAllText(context.SourceXmlFolder + context.AssetLanguage, Encoding.UTF8);
            langDoc.LoadXml(xml);
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("translationFileLoadFailed"), context.AssetLanguage, ex.Message));
            Console.WriteLine(ConsoleMessages.Get("continuingWithoutTranslations"));
            return 0;
        }

        int addedCount = 0;
        bool usesLineId = langDoc.SelectSingleNode("//Text/LineId") is not null;
        string keyNodeName = usesLineId ? "LineId" : "GUID";
        if (context.DebugMode)
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugTransKeyMode"), keyNodeName));
        XmlNodeList? xmlNodeList = langDoc.DocumentElement?.SelectNodes("//Texts/Text");
        if (xmlNodeList is not null)
        {
            foreach (XmlNode textNode in xmlNodeList)
            {
                XmlNode? keyNode = textNode.SelectSingleNode(keyNodeName);
                XmlNode? valueNode = textNode.SelectSingleNode("Text");
                if (keyNode != null && valueNode != null)
                {
                    if (context.Translator.TryAdd(keyNode.InnerText, valueNode.InnerText))
                        addedCount++;
                }
            }
        }

        if (context.DebugMode)
        {
            context.Log.Write("COMPLETE", string.Format(ConsoleMessages.Get("translationDictionaryComplete"), addedCount.ToString("N0")));
            context.Log.Debug(ConsoleMessages.Get("debugTranslationExamples"));
        }
        else
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("translationsLoadedCount"), addedCount.ToString("N0")));
        }

        return 1;
    }
}
