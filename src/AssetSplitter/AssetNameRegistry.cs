using System.Xml;

namespace AssetProcessor;

public static class AssetNameRegistry
{
    public static int Load(PipelineContext context)
    {
        if (context.DebugMode)
        {
            context.Log.Write("ASSETS", ConsoleMessages.Get("assetRegistryBuilding"));
            context.Log.Write("INFO", ConsoleMessages.Get("assetRegistryCataloging"));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugAssetRegistrySource"), context.SourceXmlFolder + "assets.xml"));
        }
        else
        {
            Console.WriteLine($"\n{ConsoleMessages.Get("buildingAssetRegistry")}");
            if (Console.IsOutputRedirected)
                Console.WriteLine(ConsoleMessages.Get("readingPropertiesFile"));
            else
                Console.Write(ConsoleMessages.Get("readingPropertiesFile"));
        }

        try
        {
            XmlDocument assetDoc = new();
            assetDoc.Load(context.SourceXmlFolder + "assets.xml");

            var pendingInheritance = new Dictionary<string, string>();
            int addedNames = 0;

            XmlNodeList? assetNodes = assetDoc.DocumentElement?.SelectNodes("//Asset");
            if (assetNodes is not null)
            {
                foreach (XmlNode assetNode in assetNodes)
                {
                    try
                    {
                        XmlNode? valuesNode = assetNode.SelectSingleNode("Values");
                        if (valuesNode == null)
                            continue;

                        string guid = XmlNodeText.GetValue(valuesNode, "Standard/GUID");
                        if (string.IsNullOrEmpty(guid))
                            continue;

                        string stdName = XmlNodeText.GetValue(valuesNode, "Standard/Name");
                        string oasisId = XmlNodeText.GetValue(valuesNode, "Text/OasisId");
                        string template = XmlNodeText.GetValue(assetNode, "Template");
                        string baseAssetGuid = XmlNodeText.GetValue(assetNode, "BaseAssetGUID");

                        string resolvedName = ResolveAssetName(context, oasisId, stdName, template);
                        if (!string.IsNullOrEmpty(resolvedName))
                        {
                            if (context.AssetNames.TryAdd(guid, resolvedName))
                                addedNames++;
                        }
                        else if (!string.IsNullOrEmpty(baseAssetGuid))
                        {
                            pendingInheritance[guid] = baseAssetGuid;
                        }
                    }
                    catch (Exception ex)
                    {
                        context.Log.Debug(string.Format(ConsoleMessages.Get("debugParseAssetFailed"), ex.Message));
                    }
                }
            }

            int pendingCount = pendingInheritance.Count;
            int inheritedCount = InheritMissingAssetNames(context, pendingInheritance);

            if (context.DebugMode)
            {
                context.Log.Write("COMPLETE", string.Format(ConsoleMessages.Get("assetRegistryComplete"), addedNames.ToString("N0")));
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugAssetRegistryPendingInheritance"), pendingCount.ToString("N0")));
                if (inheritedCount > 0)
                    context.Log.Write("INFO", string.Format(ConsoleMessages.Get("assetRegistryInherited"), inheritedCount.ToString("N0")));
                context.Log.Debug(ConsoleMessages.Get("debugRegistryIncludesAssets"));
            }

            LoadAudioGeneratedNames(context);

            if (!context.DebugMode)
                Console.WriteLine(string.Format(ConsoleMessages.Get("assetNamesLoadedCount"), addedNames.ToString("N0")));
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("failedToLoadAssetNames"), ex.Message));
            return 0;
        }
    }

    public static string ResolveAssetName(PipelineContext context, string oasisId, string standardName, string template)
    {
        if (!string.IsNullOrEmpty(oasisId) && context.Translator.TryGetValue(oasisId, out string? translated) && translated is not null)
            return translated;
        if (!string.IsNullOrEmpty(standardName))
            return standardName;
        if (!string.IsNullOrEmpty(template))
            return template;
        return string.Empty;
    }

    public static int InheritMissingAssetNames(PipelineContext context, Dictionary<string, string> pendingInheritance)
    {
        int inheritedCount = 0;
        const int maxIterations = 10;
        int iteration = 0;
        int previousCount;
        var resolvedThisPass = new List<string>(pendingInheritance.Count);

        do
        {
            previousCount = inheritedCount;
            resolvedThisPass.Clear();

            int totalPending = pendingInheritance.Count;
            int processed = 0;

            foreach (KeyValuePair<string, string> inheritancePair in pendingInheritance)
            {
                string childGuid = inheritancePair.Key;
                string parentGuid = inheritancePair.Value;
                processed++;
                if (DeveloperTrace.ShouldReportProgress(context, processed, totalPending, normalInterval: 1000))
                {
                    string inheritProgress = context.DebugMode
                        ? ConsoleMessages.Get("inheritingAssetNames")
                        : AssetProgressFormatter.Format("Inheriting", childGuid, parentGuid);
                    context.ProgressReporter.OutputFixer(inheritProgress, processed.ToString(), totalPending.ToString());
                }
                if (string.IsNullOrEmpty(parentGuid) || parentGuid.Equals(childGuid, StringComparison.OrdinalIgnoreCase))
                {
                    if (context.DebugMode)
                    {
                        context.Log.Debug(string.Format(
                            ConsoleMessages.Get("debugAssetInheritSkipped"),
                            childGuid,
                            "empty or self-referencing BaseAssetGUID"));
                    }
                    resolvedThisPass.Add(childGuid);
                    continue;
                }

                string parentName = context.AssetNames.TryGetValue(parentGuid, out string? existingParentName)
                  ? existingParentName
                    : context.Translator.TryGetValue(parentGuid, out string? translatedParent)
                      ? translatedParent
                      : string.Empty;

                if (string.IsNullOrEmpty(parentName))
                {
                    if (context.DebugMode)
                    {
                        context.Log.Debug(string.Format(
                            ConsoleMessages.Get("debugAssetInheritPending"),
                            childGuid,
                            parentGuid));
                    }
                    continue;
                }

                if (parentName.EndsWith(" (var)"))
                    parentName = parentName[..^6];

                if (context.AssetNames.TryAdd(childGuid, $"{parentName} (var)"))
                {
                    inheritedCount++;
                    if (context.DebugMode)
                    {
                        context.Log.Debug(string.Format(
                            ConsoleMessages.Get("debugAssetInheritedName"),
                            childGuid,
                            parentGuid,
                            $"{parentName} (var)"));
                    }
                }

                resolvedThisPass.Add(childGuid);
            }

            foreach (string guid in resolvedThisPass)
                pendingInheritance.Remove(guid);

            iteration++;
        } while (inheritedCount > previousCount && iteration < maxIterations && pendingInheritance.Count > 0);

        return inheritedCount;
    }

    public static void LoadAudioGeneratedNames(PipelineContext context)
    {
        string audioPath = context.SourceXmlFolder + "audio_generated.xml";
        if (!File.Exists(audioPath))
        {
            context.Log.Write("INFO", ConsoleMessages.Get("audioGeneratedMissing"));
            return;
        }

        try
        {
            XmlDocument audioDoc = new();
            audioDoc.Load(audioPath);

            int added = 0;
            XmlNodeList? audioAssets = audioDoc.DocumentElement?.SelectNodes("//Asset");
            if (audioAssets is not null)
            {
                foreach (XmlNode audioAsset in audioAssets)
                {
                    try
                    {
                        string guid = XmlNodeText.GetValue(audioAsset, "Values/Standard/GUID");
                        string name = XmlNodeText.GetValue(audioAsset, "Values/Standard/Name");
                        if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(name))
                        {
                            if (context.AssetNames.TryAdd(guid, name))
                                added++;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            context.Log.Debug(string.Format(ConsoleMessages.Get("debugAudioRegistrySource"), audioPath));
            context.Log.Write("COMPLETE", string.Format(ConsoleMessages.Get("audioRegistryComplete"), added.ToString("N0")));
        }
        catch (Exception ex)
        {
            context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("audioGeneratedLoadFailed"), ex.Message));
        }
    }
}
