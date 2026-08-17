using System.Xml;
using System.Xml.Linq;

namespace AssetProcessor;

/// <summary>
/// Converts between <see cref="XDocument"/>/<see cref="XElement"/> (System.Xml.Linq) and
/// <see cref="XmlDocument"/>/<see cref="XmlNode"/> (System.Xml).
/// Used when the pipeline mixes LINQ-to-XML and legacy XmlDocument APIs (e.g. XPath, merge logic).
/// </summary>
public static class DocumentExtensions
{
    /// <summary>
    /// Converts an <see cref="XElement"/> to an <see cref="XmlNode"/> by loading it into a temporary document.
    /// </summary>
    public static XmlNode ToXmlNode(this XElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        XmlDocument document = new();
        using XmlReader reader = element.CreateReader();
        document.Load(reader);
        return document.DocumentElement ?? throw new InvalidOperationException("Failed to convert XElement to XmlNode");
    }
}
