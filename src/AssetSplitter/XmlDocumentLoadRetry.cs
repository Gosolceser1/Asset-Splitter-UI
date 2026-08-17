using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AssetProcessor;

/// <summary>Loads XML with DTD prohibited and short backoff for transient file locks.</summary>
internal static class XmlDocumentLoadRetry
{
    private const int FileRetryCount = 8;

    public static void Load(XmlDocument document, string filePath) =>
        Load(document, filePath, encoding: null);

    public static XDocument LoadXDocument(string filePath)
    {
        using var reader = XmlReader.Create(filePath, ProhibitDtdReaderSettings());
        return XDocument.Load(reader);
    }

    public static void Load(XmlDocument document, string filePath, Encoding? encoding)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                XmlReaderSettings settings = ProhibitDtdReaderSettings();
                if (encoding is null)
                {
                    using var reader = XmlReader.Create(filePath, settings);
                    document.Load(reader);
                }
                else
                {
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var textReader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
                    using var reader = XmlReader.Create(textReader, settings);
                    document.Load(reader);
                }

                return;
            }
            catch (Exception ex) when (IsTransientFileAccess(ex) && attempt < FileRetryCount)
            {
                Thread.Sleep(GetRetryDelay(attempt));
            }
        }
    }

    private static XmlReaderSettings ProhibitDtdReaderSettings() =>
        new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

    private static bool IsTransientFileAccess(Exception ex) =>
        ex is IOException or UnauthorizedAccessException;

    private static int GetRetryDelay(int attempt) =>
        Math.Min(1000, 50 * (1 << Math.Min(attempt - 1, 5)));
}
