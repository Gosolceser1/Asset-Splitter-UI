using System.Xml;

namespace AssetProcessor;

/// <summary>Adds translation and InheritedIndex comments to asset XML during formatting / template annotate.</summary>
internal static class AssetXmlCommentAnnotator
{
    public static int AddTranslationComments(
        PipelineContext context,
        XmlDocument xmlDoc,
        XmlNode documentElement,
        PropertyScanResult propertyScan,
        InheritedIndexCommentCache? inheritedIndexCache = null)
    {
        XmlNodeList? allElements = documentElement.SelectNodes("//*");
        if (allElements is null)
        {
            return 0;
        }

        string? baseAssetGuid = documentElement.SelectSingleNode("//Asset/BaseAssetGUID")?.InnerText;
        InheritedIndexCommentCache cache = inheritedIndexCache ?? new InheritedIndexCommentCache(context);

        int commentsAdded = 0;
        foreach (XmlNode element in allElements)
        {
            if (element.Name == "InheritedIndex"
                && TryAddInheritedIndexComment(context, xmlDoc, element, baseAssetGuid, cache, ref commentsAdded))
            {
                continue;
            }

            if (TryAddTranslationComment(context, xmlDoc, element, propertyScan))
            {
                commentsAdded++;
            }
        }

        return commentsAdded;
    }

    public static void AnnotateFilesWithGuidComments(
        PipelineContext context,
        string[] filePaths,
        PropertyScanResult propertyScan)
    {
        int total = filePaths.Length;
        int processed = 0;
        Lock progressLock = new();

        System.Threading.Tasks.Parallel.ForEach(
            filePaths,
            new System.Threading.Tasks.ParallelOptions
            {
                MaxDegreeOfParallelism = PipelineParallelism.ForXmlFileIo(context),
                CancellationToken = context.CancellationToken,
            },
            filePath =>
            {
                try
                {
                    var xmlDoc = new XmlDocument();
                    XmlDocumentLoadRetry.Load(xmlDoc, filePath);
                    XmlNodeList? allElements = xmlDoc.DocumentElement?.SelectNodes("//*");
                    if (allElements == null)
                    {
                        return;
                    }

                    foreach (XmlNode element in allElements)
                    {
                        TryAddTranslationComment(context, xmlDoc, element, propertyScan);
                    }

                    GeneratedXmlFootprint.Save(xmlDoc, filePath, context);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
                {
                    context.Log.Debug(string.Format(ConsoleMessages.Get("debugAnnotateFileCommentsError"), Path.GetFileName(filePath), ex.Message));
                }
                finally
                {
                    lock (progressLock)
                    {
                        processed++;
                        if (DeveloperTrace.ShouldEmitProgressLine(context, processed, total))
                        {
                            string annotateProgress = context.DebugMode
                                ? ConsoleMessages.Get("annotatingTemplateComments")
                                : AssetProgressFormatter.FromAssetFileStem(
                                    "Annotating",
                                    Path.GetFileNameWithoutExtension(filePath));
                            context.ProgressReporter.ReportProgress(annotateProgress, processed.ToString(), total.ToString());
                        }
                    }
                }
            });
    }

    private static bool TryAddInheritedIndexComment(
        PipelineContext context,
        XmlDocument xmlDoc,
        XmlNode element,
        string? baseAssetGuid,
        InheritedIndexCommentCache cache,
        ref int commentsAdded)
    {
        if (element.InnerXml.Contains('<'))
        {
            return false;
        }

        try
        {
            if (!int.TryParse(element.InnerText.Trim(), out int inheritedIndex))
            {
                return false;
            }

            if (string.IsNullOrEmpty(baseAssetGuid))
            {
                return false;
            }

            XmlNode? itemParent = element.ParentNode?.ParentNode;
            if (itemParent is null)
            {
                return false;
            }

            string itemCollectionName = itemParent.Name;
            if (!cache.TryResolveInheritedProperty(
                    baseAssetGuid,
                    itemCollectionName,
                    inheritedIndex,
                    out string firstPropertyName,
                    out string propertyGuid))
            {
                return false;
            }

            string translatedName = TranslationRegistry.Translate(context, propertyGuid);
            string commentText = !string.IsNullOrWhiteSpace(translatedName)
                ? $" Inherits: {firstPropertyName} {propertyGuid} {translatedName} "
                : $" Inherits: {firstPropertyName} {propertyGuid} ";
            string cleanedComment = AssetTextSanitizer.ToSafeXmlCommentBody(commentText);
            XmlComment inheritComment = xmlDoc.CreateComment(cleanedComment);
            if (element.ParentNode is not null)
            {
                element.ParentNode.InsertAfter(inheritComment, element);
                commentsAdded++;
            }

            return true;
        }
        catch (Exception ex)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugInheritedIndexProcessingError"), ex.Message));
            return false;
        }
    }

    private static bool TryAddTranslationComment(
        PipelineContext context,
        XmlDocument xmlDoc,
        XmlNode element,
        PropertyScanResult propertyScan)
    {
        if (element.InnerXml.Contains('<'))
        {
            return false;
        }

        if (!propertyScan.EligibleProperties.Contains(element.Name))
        {
            return false;
        }

        string elementValue = element.InnerText.Trim();
        if (string.IsNullOrEmpty(elementValue))
        {
            return false;
        }

        if (int.TryParse(elementValue, out int numericValue))
        {
            bool isSmallNonWhitelistNumber =
                numericValue is >= -100 and <= 100
                && !propertyScan.WhitelistProperties.Contains(element.Name);
            if (isSmallNonWhitelistNumber)
            {
                return false;
            }
        }

        string translatedValue = TranslationRegistry.TranslateElement(context, element, elementValue);
        if (string.IsNullOrWhiteSpace(translatedValue) || translatedValue.Length < 2)
        {
            return false;
        }

        if (element.Name is "BaseAssetGUID" or "DestroyShipsPool" or "EnemyShipPool" or "PickUpObjects" or "EscortShipPool" or "PredefinedCityName")
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugElementTranslation"), element.Name, element.InnerText, translatedValue, translatedValue.Length));
        }

        string? commentText = AssetTextSanitizer.ToXmlCommentText(translatedValue);
        if (commentText is null)
        {
            return false;
        }

        XmlComment translationComment;
        try
        {
            translationComment = xmlDoc.CreateComment(commentText);
        }
        catch (Exception ex)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugInvalidCommentText"), commentText, ex.Message));
            return false;
        }

        XmlNode? parentNode = element.ParentNode;
        if (parentNode is null)
        {
            return false;
        }

        try
        {
            bool hasElementChildren = false;
            for (int i = 0; i < element.ChildNodes.Count; i++)
            {
                XmlNode? child = element.ChildNodes[i];
                if (child is { NodeType: XmlNodeType.Element })
                {
                    hasElementChildren = true;
                    break;
                }
            }

            if (hasElementChildren)
            {
                return false;
            }

            parentNode.InsertAfter(translationComment, element);
            return true;
        }
        catch (Exception ex)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugCommentInsertionError"), commentText, ex.Message));
            return false;
        }
    }
}
