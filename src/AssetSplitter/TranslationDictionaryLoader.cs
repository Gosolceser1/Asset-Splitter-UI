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
            {
                Console.WriteLine(msg);
            }
            else
            {
                Console.Write(msg);
            }
        }

        string languagePath = context.SourceXmlFolder + context.AssetLanguage;
        int addedCount = 0;
        string keyNodeName = "";
        try
        {
            using FileStream stream = new(languagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            });

            long totalBytes = Math.Max(1L, stream.Length);
            int lastProgressUnit = 0;
            ReportLanguageReadProgress(context, stream.Position, totalBytes, ref lastProgressUnit);

            while (reader.ReadToFollowing("Text"))
            {
                if (TryReadTextEntry(reader, out string key, out string value, out string entryKeyNodeName))
                {
                    if (keyNodeName.Length == 0)
                    {
                        keyNodeName = entryKeyNodeName;
                    }

                    if (context.Translator.TryAdd(key, value))
                    {
                        addedCount++;
                    }
                }

                reader.Skip();
                ReportLanguageReadProgress(context, stream.Position, totalBytes, ref lastProgressUnit);
            }

            ReportLanguageReadProgress(context, totalBytes, totalBytes, ref lastProgressUnit, force: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("translationFileLoadFailed"), context.AssetLanguage, ex.Message));
            Console.WriteLine(ConsoleMessages.Get("continuingWithoutTranslations"));
            return 0;
        }

        if (context.DebugMode)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugTransKeyMode"), keyNodeName.Length == 0 ? "unknown" : keyNodeName));
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

    private static bool TryReadTextEntry(XmlReader reader, out string key, out string value, out string keyNodeName)
    {
        key = "";
        value = "";
        keyNodeName = "";

        using XmlReader textReader = reader.ReadSubtree();
        string lineId = "";
        string guid = "";
        bool hasValueNode = false;
        bool skippedRootTextElement = false;

        while (textReader.Read())
        {
            if (textReader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (textReader.Name.Equals("LineId", StringComparison.OrdinalIgnoreCase))
            {
                lineId = textReader.ReadElementContentAsString().Trim();
            }
            else if (textReader.Name.Equals("GUID", StringComparison.OrdinalIgnoreCase))
            {
                guid = textReader.ReadElementContentAsString().Trim();
            }
            else if (textReader.Name.Equals("Text", StringComparison.OrdinalIgnoreCase))
            {
                if (!skippedRootTextElement)
                {
                    skippedRootTextElement = true;
                    continue;
                }

                value = textReader.ReadElementContentAsString();
                hasValueNode = true;
            }
        }

        if (!string.IsNullOrEmpty(lineId))
        {
            key = lineId;
            keyNodeName = "LineId";
        }
        else
        {
            key = guid;
            keyNodeName = "GUID";
        }

        return !string.IsNullOrEmpty(key) && hasValueNode;
    }

    private static void ReportLanguageReadProgress(
        PipelineContext context,
        long bytesRead,
        long totalBytes,
        ref int lastProgressUnit,
        bool force = false)
    {
        const int totalUnits = 10_000;
        int progressUnit = force
            ? totalUnits
            : Math.Clamp((int)(bytesRead * totalUnits / Math.Max(1L, totalBytes)), 1, totalUnits);

        if (!force && progressUnit > 1)
        {
            progressUnit = Math.Max(1, progressUnit / 500 * 500);
        }

        if (!force && progressUnit <= lastProgressUnit)
        {
            return;
        }

        lastProgressUnit = progressUnit;
        context.ProgressReporter.OutputFixer(
            ConsoleMessages.Get("readingLanguageFile"),
            progressUnit.ToString(),
            totalUnits.ToString());
    }
}
