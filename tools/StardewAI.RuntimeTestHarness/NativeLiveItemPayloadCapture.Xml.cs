using System.Xml;

namespace StardewAI.RuntimeTestHarness;

internal static partial class NativeLiveItemPayloadCapture
{
    private const int MaximumXmlDepth = 64;
    private const int MaximumXmlNodes = 8192;

    private static void ValidateXml(string xml, int maximumCharacters)
    {
        if (string.IsNullOrEmpty(xml) || xml.Length > maximumCharacters)
            throw new InvalidDataException("Live item payload XML is empty or exceeds its character bound.");
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            ConformanceLevel = ConformanceLevel.Document,
            CheckCharacters = true,
            MaxCharactersInDocument = maximumCharacters,
            MaxCharactersFromEntities = 1024,
            IgnoreWhitespace = false,
            IgnoreComments = false
        });
        int roots = 0;
        int nodes = 0;
        while (reader.Read())
        {
            nodes += 1 + reader.AttributeCount;
            if (reader.Depth > MaximumXmlDepth || nodes > MaximumXmlNodes)
                throw new InvalidDataException("Live item payload XML depth/node bound exceeded.");
            if (reader.NodeType == XmlNodeType.Element && reader.Depth == 0) roots++;
        }
        if (roots != 1)
            throw new InvalidDataException("Live item payload XML must contain exactly one root.");
    }
}
