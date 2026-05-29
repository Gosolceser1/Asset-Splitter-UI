using System.Xml;

namespace AssetProcessor
{
    public static class FormattingService
    {
        private const string GoldCoinGuid = "1010017";
        private const string TimberGuid = "1010207";
        private const string PlanksGuid = "1010218";
        private static readonly string[] BaseCostGuids = [GoldCoinGuid, TimberGuid, PlanksGuid];

        private readonly record struct FormatFileContext(
            PipelineContext Context, bool AllowMove, bool IsAnno1800,
            string[] BaseAfricanCosts, string[] BaseDefaultCosts,
            PropertyScanResult PropertyScan, int TotalFiles);

        public static int FormatXml(PipelineContext context, string[] xmlFilePaths, PropertyScanResult propertyScan, string gameType = "anno1800", bool allowMove = true, bool skipInitialMessage = false)
        {
            bool isAnno1800 = gameType.Equals("anno1800", StringComparison.OrdinalIgnoreCase);
            if (!skipInitialMessage)
                WriteFormattingStart(context, xmlFilePaths.Length, isAnno1800);

            string[] africanIngredients = AssetProcessorConfiguration.GetAfricanIngredients(context.RegionalIngredientsConfig, context);
            string[] defaultIngredients = AssetProcessorConfiguration.GetDefaultIngredients(context.RegionalIngredientsConfig, context);
            string[] baseAfricanCosts = BaseCostGuids.Concat(africanIngredients ?? Array.Empty<string>()).ToArray();
            string[] baseDefaultCosts = BaseCostGuids.Concat(defaultIngredients ?? Array.Empty<string>()).ToArray();
            int processedCount = 0;
            Lock progressLock = new();
            var fileCtx = new FormatFileContext(context, allowMove, isAnno1800, baseAfricanCosts, baseDefaultCosts, propertyScan, xmlFilePaths.Length);
            System.Threading.Tasks.Parallel.ForEach(xmlFilePaths,
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
                xmlFilePath => FormatSingleFile(fileCtx, xmlFilePath, ref processedCount, progressLock));
            fileCtx.Context.ProgressReporter.OutputFixer(ConsoleMessages.Get("formattingProgress"), xmlFilePaths.Length.ToString(), xmlFilePaths.Length.ToString());
            WriteFormattingComplete(context, processedCount, isAnno1800);
            return 0;
        }

        private static void WriteFormattingStart(PipelineContext context, int fileCount, bool isAnno1800)
        {
            if (context.DebugMode)
            {
                string features = ConsoleMessages.Get("formatFeatureVectorCleanup");
                if (isAnno1800) features += ConsoleMessages.Get("formatFeatureRegionalIngredients");
                features += ConsoleMessages.Get("formatFeatureCommentsFolders");
                context.Log.Write("FORMAT", string.Format(ConsoleMessages.Get("formatProcessingFiles"), fileCount.ToString("N0"), features));
                context.Log.Debug(context.AssetComments
                    ? ConsoleMessages.Get("debugStep3AddingComments")
                    : ConsoleMessages.Get("debugStep3SkippingComments"));
                context.Log.Debug(context.AssetTemplates
                    ? ConsoleMessages.Get("debugStep4OrganizingFolders")
                    : ConsoleMessages.Get("debugStep4KeepingMainDirectory"));
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine(string.Format(ConsoleMessages.Get("finalProcessingFiles"), fileCount.ToString("N0")));

                string applying = ConsoleMessages.Get("applyingXmlCleanup");
                if (isAnno1800) applying += ConsoleMessages.Get("applyingRegionalIngredients");
                if (context.AssetComments) applying += ConsoleMessages.Get("applyingTranslations");
                if (context.AssetTemplates) applying += ConsoleMessages.Get("applyingFolderOrganization");
                Console.WriteLine(applying);

                if (Console.IsOutputRedirected)
                    Console.WriteLine(ConsoleMessages.Get("formattingAssets"));
                else
                    Console.Write(ConsoleMessages.Get("formattingAssets"));
            }
        }

        private static void WriteFormattingComplete(PipelineContext context, int processedCount, bool isAnno1800)
        {
            if (!context.DebugMode)
            {
                Console.WriteLine(string.Format(ConsoleMessages.Get("filesFormattedCount"), processedCount.ToString("N0")));
                return;
            }

            context.Log.Write("COMPLETE", ConsoleMessages.Get("finalProcessingCompleted"));
            context.Log.Write("INFO", string.Format(ConsoleMessages.Get("vectorCleanupApplied"), processedCount.ToString("N0")));
            if (isAnno1800)
                context.Log.Write("INFO", ConsoleMessages.Get("regionalIngredientsApplied"));
            context.Log.Write("INFO", string.Format(ConsoleMessages.Get("translationCommentsSummary"), context.AssetComments ? ConsoleMessages.Get("addedCFlag") : ConsoleMessages.Get("skippedNoCFlag")));
            context.Log.Write("INFO", string.Format(ConsoleMessages.Get("templateOrganizationSummary"), context.AssetTemplates ? ConsoleMessages.Get("appliedTFlag") : ConsoleMessages.Get("skippedNoTFlag")));
            context.DebugStats.WriteFormattingSummary(context.Log);
        }

        private static void FormatSingleFile(FormatFileContext ctx, string xmlFilePath, ref int processedCount, Lock progressLock)
        {
            try
            {
                if (ctx.Context.DebugMode)
                {
                    ctx.Context.Log.Debug(string.Format(
                        ConsoleMessages.Get("debugFormatProcessingFile"),
                        Path.GetFileName(xmlFilePath)));
                }

                XmlDocument xmlDoc = new();
                try
                {
                    int currentFileIndex;
                    lock (progressLock)
                    {
                        processedCount++;
                        currentFileIndex = processedCount;
                    }

                    if (ShouldReportProgress(ctx.Context, currentFileIndex, ctx.TotalFiles))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(xmlFilePath);
                        string displayName = AssetProcessorFileSystem.ExtractDisplayName(fileName);
                        string? progressTemplate = ctx.Context.DebugMode
                            ? null
                            : AssetProcessorFileSystem.TryReadTemplateFromAssetFile(xmlFilePath);
                        string formatProgress = ctx.Context.DebugMode
                            ? string.Format(ConsoleMessages.Get("processingAssetProgress"), displayName)
                            : AssetProgressFormatter.FromAssetFileStem("Processing", fileName, progressTemplate);
                        ctx.Context.ProgressReporter.OutputFixer(formatProgress, currentFileIndex.ToString(), ctx.TotalFiles.ToString());
                    }

                    try
                    {
                        xmlDoc.Load(xmlFilePath);
                    }
                    catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
                    {
                        ctx.Context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("failedToLoadXmlFile"), xmlFilePath), always: true);
                        ctx.Context.Log.Debug(string.Format(ConsoleMessages.Get("debugXmlLoadError"), ex.Message));
                        return;
                    }

                    XmlNode? documentElement = xmlDoc.DocumentElement;
                    int vectorElementsRemoved = RemoveVectorElements(documentElement);
                    if (vectorElementsRemoved > 0)
                        ctx.Context.Log.Debug(string.Format(ConsoleMessages.Get("debugVectorElementsRemoved"), vectorElementsRemoved));

                    XmlNode? templateNode = xmlDoc.SelectSingleNode("//Asset/Template");
                    string templateName = templateNode?.InnerText ?? "Unknown";
                    if (templateNode is null)
                    {
                        ctx.Context.DebugStats.RecordFormatMissingTemplateNode();
                        if (ctx.Context.DebugMode)
                        {
                            ctx.Context.Log.Debug(string.Format(
                                ConsoleMessages.Get("debugFormatNoTemplateNode"),
                                Path.GetFileName(xmlFilePath)));
                        }
                    }

                    if (ctx.IsAnno1800)
                        ApplyRegionalIngredients(ctx.Context, xmlDoc, ctx.BaseAfricanCosts, ctx.BaseDefaultCosts);

                    int commentsAdded = 0;
                    if (ctx.Context.AssetComments && documentElement is not null)
                    {
                        commentsAdded += AddTranslationComments(ctx.Context, xmlDoc, documentElement, ctx.PropertyScan);
                        if (commentsAdded > 0)
                        {
                            ctx.Context.DebugStats.RecordFormatComments(commentsAdded);
                            if (ctx.Context.DebugMode)
                                ctx.Context.Log.Debug(string.Format(ConsoleMessages.Get("debugTranslatedCommentsAdded"), commentsAdded));
                        }
                    }

                    xmlDoc.Save(xmlFilePath);
                    MoveToTemplateFolder(ctx.Context, xmlFilePath, templateName, ctx.AllowMove);
                }
                catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
                {
                    ctx.Context.Issues.ReportUnexpectedFileError(ex.Message);
                    ctx.Context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("unexpectedFileProcessingError"), ex.Message), always: true);
                    ctx.Context.Log.Debug($"[DEBUG] Stack trace: {ex.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                ctx.Context.Issues.ReportFormatFileFailed(xmlFilePath, ex.Message);
                ctx.Context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("formatSingleFileFailed"), xmlFilePath, ex.Message));
            }
        }

        private static int RemoveVectorElements(XmlNode? documentElement)
        {
            if (documentElement is null)
                return 0;

            XmlNodeList? vectorElements = documentElement.SelectNodes("//VectorElement");
            if (vectorElements is null)
                return 0;

            int count = vectorElements.Count;
            foreach (XmlNode vectorElement in vectorElements)
                vectorElement.ParentNode?.RemoveChild(vectorElement);
            return count;
        }

        private static void ApplyRegionalIngredients(PipelineContext context, XmlDocument xmlDoc, string[] baseAfricanCosts, string[] baseDefaultCosts)
        {
            XmlNode? associatedRegionsNode = xmlDoc.SelectSingleNode("//Building/AssociatedRegions");
            if (associatedRegionsNode is null)
                return;

            string regionType = associatedRegionsNode.InnerText.Contains("Africa") ? ConsoleMessages.Get("regionAfrican") : ConsoleMessages.Get("regionDefaultEuropean");
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugBuildingRegionDetected"), regionType, associatedRegionsNode.InnerText));

            int applied = ApplyIngredients(xmlDoc, associatedRegionsNode, baseAfricanCosts, baseDefaultCosts);
            if (applied > 0)
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugAppliedIngredients"), applied, regionType));
        }

        private static void MoveToTemplateFolder(PipelineContext context, string xmlFilePath, string templateName, bool allowMove)
        {
            if (!context.AssetTemplates || !allowMove)
                return;

            context.DebugStats.RecordFormatTemplateMove();
            if (context.DebugMode)
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugStep4MovingFileToTemplateFolder"), templateName));
            string destDir = Path.Combine(context.AssetOut, templateName);
            Directory.CreateDirectory(destDir);
            string destFile = Path.Combine(destDir, Path.GetFileName(xmlFilePath));
            try
            {
                File.Move(xmlFilePath, destFile);
            }
            catch (IOException ex)
            {
                context.Issues.ReportMoveToTemplateFolderFailed(ex.Message);
                context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("couldNotMoveToTemplateFolder"), ex.Message), always: true);
            }
        }

        private static int ApplyIngredients(XmlDocument assetDoc, XmlNode? associatedRegionsNode, string[] africaIngredients, string[] defaultIngredients)
        {
            int applied = 0;
            bool isAfrica = associatedRegionsNode != null && associatedRegionsNode.InnerText.Contains("Africa");

            for (int index = 1; index <= 6; index++)
            {
                XmlNode? costItem = assetDoc.SelectSingleNode($"//Cost/Costs/Item[{index}]");
                if (costItem == null)
                    continue;

                XmlNode? vector = costItem.SelectSingleNode("VectorElement");
                if (vector != null)
                    costItem.RemoveChild(vector);

                XmlElement ingredient = assetDoc.CreateElement("Ingredient");
                string[] primaryIngredients = isAfrica ? africaIngredients : defaultIngredients;
                string[] fallbackIngredients = defaultIngredients;
                if (primaryIngredients.Length == 0)
                {
                    if (fallbackIngredients.Length == 0)
                        continue;
                    primaryIngredients = fallbackIngredients;
                }

                int arrayIndex = Math.Min(index - 1, primaryIngredients.Length - 1);
                ingredient.InnerText = primaryIngredients[arrayIndex];

                XmlNode? existing = costItem.SelectSingleNode("Ingredient");
                if (existing == null)
                    costItem.AppendChild(ingredient);
                else
                    costItem.ReplaceChild(ingredient, existing);

                applied++;
            }

            return applied;
        }

        private static int AddTranslationComments(PipelineContext context, XmlDocument xmlDoc, XmlNode documentElement, PropertyScanResult propertyScan)
        {
            XmlNodeList? allElements = documentElement.SelectNodes("//*");
            if (allElements is null)
                return 0;

            int commentsAdded = 0;
            foreach (XmlNode element in allElements)
            {
                if (element.Name == "InheritedIndex" && TryAddInheritedIndexComment(context, xmlDoc, element, documentElement, ref commentsAdded))
                    continue;

                if (TryAddTranslationComment(context, xmlDoc, element, propertyScan))
                    commentsAdded++;
            }
            return commentsAdded;
        }

        private static bool TryAddInheritedIndexComment(PipelineContext context, XmlDocument xmlDoc, XmlNode element, XmlNode documentElement, ref int commentsAdded)
        {
            if (element.InnerXml.Contains("<"))
                return false;

            try
            {
                if (!int.TryParse(element.InnerText.Trim(), out int inheritedIndex))
                    return false;

                string? baseAssetGuid = documentElement.SelectSingleNode("//Asset/BaseAssetGUID")?.InnerText;
                if (string.IsNullOrEmpty(baseAssetGuid))
                    return false;

                XmlNode? itemParent = element.ParentNode?.ParentNode;
                if (itemParent is null)
                    return false;

                string itemCollectionName = itemParent.Name;
                string? parentAssetFile = context.GuidIndex?.Find(baseAssetGuid);
                if (string.IsNullOrEmpty(parentAssetFile))
                    return false;

                XmlDocument parentDoc = new();
                parentDoc.Load(parentAssetFile);
                XmlNodeList? parentItems = parentDoc.SelectNodes($"//Asset/Values//{itemCollectionName}/Item");
                if (parentItems is null || inheritedIndex >= parentItems.Count)
                    return false;

                if (parentItems[inheritedIndex] is not XmlNode parentItem)
                {
                    context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("parentItemNull"), inheritedIndex));
                    return false;
                }

                XmlNode? firstProperty = null;
                string firstPropertyName = "";
                foreach (XmlNode child in parentItem.ChildNodes)
                {
                    if (child.NodeType == XmlNodeType.Element && !string.IsNullOrWhiteSpace(child.InnerText) && !child.InnerXml.Contains("<") && child.InnerText.Trim().Length > 0)
                    {
                        firstProperty = child;
                        firstPropertyName = child.Name;
                        break;
                    }
                }
                if (firstProperty is null)
                    return false;

                string propertyGuid = firstProperty.InnerText.Trim();
                string translatedName = TranslationRegistry.Translate(context, propertyGuid);
                string commentText = !string.IsNullOrWhiteSpace(translatedName)
                    ? $" Inherits: {firstPropertyName} {propertyGuid} {translatedName} "
                    : $" Inherits: {firstPropertyName} {propertyGuid} ";
                string cleanedComment = commentText.Replace("--", "-").Trim();
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

        private static bool TryAddTranslationComment(PipelineContext context, XmlDocument xmlDoc, XmlNode element, PropertyScanResult propertyScan)
        {
            if (element.InnerXml.Contains("<"))
                return false;
            if (!propertyScan.EligibleProperties.Contains(element.Name))
                return false;

            string elementValue = element.InnerText.Trim();
            if (string.IsNullOrEmpty(elementValue))
                return false;

            if (int.TryParse(elementValue, out int numericValue))
            {
                if (numericValue >= -100 && numericValue <= 100 && !propertyScan.WhitelistProperties.Contains(element.Name))
                    return false;
            }

            string translatedValue = TranslationRegistry.Translate(context, elementValue);
            if (string.IsNullOrWhiteSpace(translatedValue) || translatedValue.Length < 2)
                return false;

            if (element.Name == "BaseAssetGUID" || element.Name == "DestroyShipsPool" || element.Name == "EnemyShipPool" || element.Name == "PickUpObjects" || element.Name == "EscortShipPool" || element.Name == "PredefinedCityName")
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugElementTranslation"), element.Name, element.InnerText, translatedValue, translatedValue.Length));

            string? commentText = AssetTextSanitizer.ToXmlCommentText(translatedValue);
            if (commentText is null)
                return false;

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
                return false;

            try
            {
                bool hasElementChildren = false;
                for (int i = 0; i < element.ChildNodes.Count; i++)
                {
                    if (element.ChildNodes[i]!.NodeType == XmlNodeType.Element)
                    {
                        hasElementChildren = true;
                        break;
                    }
                }
                if (hasElementChildren)
                    return false;

                parentNode.InsertAfter(translationComment, element);
                return true;
            }
            catch (Exception ex)
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugCommentInsertionError"), commentText, ex.Message));
                return false;
            }
        }

        public static void AnnotateFilesWithGuidComments(PipelineContext context, string[] filePaths, PropertyScanResult propertyScan)
        {
            int total = filePaths.Length;
            int processed = 0;
            Lock progressLock = new();

            System.Threading.Tasks.Parallel.ForEach(filePaths,
              new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
              filePath =>
            {
                try
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.Load(filePath);
                    XmlNodeList? allElements = xmlDoc.DocumentElement?.SelectNodes("//*");
                    if (allElements == null) return;

                    foreach (XmlNode element in allElements)
                    {
                        TryAddTranslationComment(context, xmlDoc, element, propertyScan);
                    }

                    xmlDoc.Save(filePath);

                    lock (progressLock)
                    {
                        processed++;
                        if (DeveloperTrace.ShouldReportProgress(context, processed, total))
                        {
                            string? annotateTemplate = context.DebugMode
                                ? null
                                : AssetProcessorFileSystem.TryReadTemplateFromAssetFile(filePath);
                            string annotateProgress = context.DebugMode
                                ? ConsoleMessages.Get("annotatingTemplateComments")
                                : AssetProgressFormatter.FromAssetFileStem("Annotating", Path.GetFileNameWithoutExtension(filePath), annotateTemplate);
                            context.ProgressReporter.OutputFixer(annotateProgress, processed.ToString(), total.ToString());
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
                {
                    context.Log.Debug(string.Format(ConsoleMessages.Get("debugAnnotateFileCommentsError"), Path.GetFileName(filePath), ex.Message));
                }
            });
        }

        public static bool ShouldReportProgress(PipelineContext context, int currentIndex, int totalFiles) =>
            DeveloperTrace.ShouldReportProgress(context, currentIndex, totalFiles);
    }
}
