using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace AssetProcessor;

internal sealed class TemplatePropertyCache(Action<string, string> writeMessage)
{
    private readonly Lock _syncRoot = new();
    private Dictionary<string, XElement>? _rawProperties;
    private Dictionary<string, IReadOnlyList<XNode>>? _filteredAnno117Properties;
    private bool _debugMode;

    public void Initialize(XmlNode templatesRoot, string gameType, IEnumerable<string> fixlist, bool debugMode)
    {
        if (_rawProperties != null)
            return;

        lock (_syncRoot)
        {
            if (_rawProperties != null)
                return;

            _debugMode = debugMode;
            string[] templateNames = [.. fixlist];
            bool isAnno117 = GameTypeDetector.IsAnno117(gameType);
            var rawCache = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
            var filteredCache = new Dictionary<string, IReadOnlyList<XNode>>(StringComparer.OrdinalIgnoreCase);

            if (_debugMode)
                writeMessage(string.Format(ConsoleMessages.Get("debugCacheBuildingTemplateProperties"), templateNames.Length) + $" | Game: {gameType}, Anno117: {isAnno117}", "CACHE");
            else
                WriteDebug(string.Format(ConsoleMessages.Get("debugCacheBuildingTemplateProperties"), templateNames.Length), "CACHE");

            int cachedCount = 0;
            foreach (string templateName in templateNames)
            {
                XmlNode? propertiesNode = templatesRoot.SelectSingleNode($"//Template[Name='{templateName}']/Properties");
                XPathNavigator? navigator = propertiesNode?.CreateNavigator();
                if (navigator is null)
                    continue;

                try
                {
                    XElement properties = XElement.Load(navigator.ReadSubtree());
                    rawCache[templateName] = properties;
                    cachedCount++;

                    if (isAnno117)
                        filteredCache[templateName] = FilterAnno117Properties(properties);
                }
                catch (Exception ex)
                {
                    WriteDebug(string.Format(ConsoleMessages.Get("debugCacheTemplateFailed"), templateName, ex.Message), "WARNING");
                }
            }

            _rawProperties = rawCache;
            _filteredAnno117Properties = isAnno117 ? filteredCache : null;

            WriteDebug(string.Format(ConsoleMessages.Get("debugCacheTemplatePropertiesComplete"), cachedCount, cachedCount * 15), "COMPLETE");
            if (_debugMode && isAnno117)
                writeMessage($"[DEBUG][CACHE] Filtered Anno117 properties for {filteredCache.Count} templates", "CACHE");
        }
    }

    public bool TryGetRawProperties(string templateName, out XElement? properties)
    {
        properties = null;
        lock (_syncRoot)
        {
            return _rawProperties?.TryGetValue(templateName, out properties) == true;
        }
    }

    public bool TryGetFilteredAnno117Properties(string templateName, out IReadOnlyList<XNode>? properties)
    {
        properties = null;
        lock (_syncRoot)
        {
            return _filteredAnno117Properties?.TryGetValue(templateName, out properties) == true;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _rawProperties = null;
            _filteredAnno117Properties = null;
        }
    }

    private static IReadOnlyList<XNode> FilterAnno117Properties(XElement properties)
    {
        return properties.Nodes()
            .Where(node => node is not XElement element ||
                (!element.Name.LocalName.Contains("Cost", StringComparison.Ordinal) &&
                 !element.Name.LocalName.Contains("Ingredient", StringComparison.Ordinal)))
            .ToList();
    }

    private void WriteDebug(string message, string messageType)
    {
        if (_debugMode)
            writeMessage(message, messageType);
    }
}
