using System.Xml;

namespace AssetProcessor;

/// <summary>Vector cleanup and Anno 1800 regional ingredient/cost rewriting during Phase 7.</summary>
internal static class AssetRegionalCostFormatter
{
    public static int RemoveVectorElements(XmlNode? documentElement)
    {
        if (documentElement is null)
        {
            return 0;
        }

        XmlNodeList? vectorElements = documentElement.SelectNodes("//VectorElement");
        if (vectorElements is null)
        {
            return 0;
        }

        int count = vectorElements.Count;
        foreach (XmlNode vectorElement in vectorElements)
        {
            vectorElement.ParentNode?.RemoveChild(vectorElement);
        }

        return count;
    }

    public static void ApplyRegionalIngredients(
        PipelineContext context,
        XmlDocument xmlDoc,
        string[] baseAfricanCosts,
        string[] baseDefaultCosts)
    {
        XmlNode? associatedRegionsNode = xmlDoc.SelectSingleNode("//Building/AssociatedRegions");
        if (associatedRegionsNode is null)
        {
            return;
        }

        string regionType = IsAfricanRegion(associatedRegionsNode.InnerText)
            ? ConsoleMessages.Get("regionAfrican")
            : ConsoleMessages.Get("regionDefaultEuropean");
        context.Log.Debug(string.Format(ConsoleMessages.Get("debugBuildingRegionDetected"), regionType, associatedRegionsNode.InnerText));

        int applied = ApplyIngredients(xmlDoc, associatedRegionsNode, baseAfricanCosts, baseDefaultCosts);
        if (applied > 0)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugAppliedIngredients"), applied, regionType));
        }
    }

    private static int ApplyIngredients(
        XmlDocument assetDoc,
        XmlNode? associatedRegionsNode,
        string[] africaIngredients,
        string[] defaultIngredients)
    {
        int applied = 0;
        bool isAfrica = associatedRegionsNode != null && IsAfricanRegion(associatedRegionsNode.InnerText);

        for (int index = 1; index <= 6; index++)
        {
            XmlNode? costItem = assetDoc.SelectSingleNode($"//Cost/Costs/Item[{index}]");
            if (costItem == null)
            {
                continue;
            }

            XmlNode? vector = costItem.SelectSingleNode("VectorElement");
            if (vector != null)
            {
                costItem.RemoveChild(vector);
            }

            XmlElement ingredient = assetDoc.CreateElement("Ingredient");
            string[] primaryIngredients = isAfrica ? africaIngredients : defaultIngredients;
            string[] fallbackIngredients = defaultIngredients;
            if (primaryIngredients.Length == 0)
            {
                if (fallbackIngredients.Length == 0)
                {
                    continue;
                }

                primaryIngredients = fallbackIngredients;
            }

            // Do not repeat the last ingredient for slots beyond the regional list (the Africa list
            // is one shorter, so slot 6 used to get Mud Bricks a second time).
            if (index - 1 >= primaryIngredients.Length)
            {
                continue;
            }

            ingredient.InnerText = primaryIngredients[index - 1];

            XmlNode? existing = costItem.SelectSingleNode("Ingredient");
            if (existing != null && !string.IsNullOrWhiteSpace(existing.InnerText))
            {
                continue;
            }

            if (existing == null)
            {
                costItem.AppendChild(ingredient);
            }
            else
            {
                costItem.ReplaceChild(ingredient, existing);
            }

            applied++;
        }

        return applied;
    }

    /// <summary>
    /// Anno stores regions as tokens (`Moderate`, `Africa`, `Colony01`, …), often semicolon-separated.
    /// Substring match would treat <c>NotAfrica</c> as African.
    /// </summary>
    internal static bool IsAfricanRegion(string associatedRegions)
    {
        if (string.IsNullOrWhiteSpace(associatedRegions))
        {
            return false;
        }

        foreach (string token in associatedRegions.Split(
                     [';', ',', '|', ' '],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Equals("Africa", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
