using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private static Dictionary<string, ActionOriginObservedDebris> ReadDebris(ActionOriginObservedDebris[] rows,
        ref int payloadCharacters, ref int chunkReferences)
    {
        Require(rows is { Length: <= 512 }, "debris rows are missing or exceed the native bound");
        var result = new Dictionary<string, ActionOriginObservedDebris>(StringComparer.Ordinal);
        var chunks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows!)
        {
            Require(row is not null && ReferenceId(row.DebrisId) && row.RuntimeType == "StardewValley.Debris" &&
                    !string.IsNullOrWhiteSpace(row.DebrisType) && row.ChunkIds is not null && result.TryAdd(row.DebrisId, row),
                "debris identity/type is unavailable or repeated");
            var chunkIds = row!.ChunkIds!;
            Require(chunkIds.Length <= 4096 - chunkReferences &&
                    chunkIds.All(id => ReferenceId(id) && chunks.Add(id)),
                "chunk identity repeats or exceeds its total native receipt budget");
            chunkReferences += chunkIds.Length;
            Require((row.HeldItem is null) == (row.HeldItemStack is null), "held-item payload and stack availability differ");
            if (row.HeldItem is not null)
            {
                Require(row.HeldItemStack >= 0, "held-item stack is invalid");
                ReadItem(row.HeldItem, row.HeldItemStack!.Value, ref payloadCharacters);
            }
        }
        return result;
    }

    private static ItemKey ReadItem(ActionOriginObservedItem? item, int stack, ref int payloadCharacters)
    {
        Require(item is not null && item.RuntimeType == "StardewValley.Object" &&
                !string.IsNullOrWhiteSpace(item.QualifiedItemId),
            "live item type, XML payload or UTF-8 hash is invalid");
        var root = ReadItemXml(item!.SerializedStateXml, item.SerializedStateSha256, ref payloadCharacters);
        Require(root.Name == "Object" && root.Attribute(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance")) is null,
            "live item XML is not the exact recorded Object type");
        var id = ItemXmlValue(root, "itemId");
        Require(bool.TryParse(ItemXmlValue(root, "bigCraftable"), out var bigCraftable) && !string.IsNullOrWhiteSpace(id) &&
                item.QualifiedItemId == (bigCraftable ? "(BC)" : "(O)") + id &&
                int.TryParse(ItemXmlValue(root, "quality"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quality) && quality == item.Quality &&
                int.TryParse(ItemXmlValue(root, "stack"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var xmlStack) && xmlStack == stack,
            "live XML identity, quality or offered stack differs from its observation");
        return new(item.QualifiedItemId, item.Quality);
    }

    private static XElement ReadItemXml(string? xml, string? sha256, ref int payloadCharacters)
    {
        Require(!string.IsNullOrWhiteSpace(xml) && xml.Length <= 65536 &&
                (payloadCharacters += xml.Length) <= 1024 * 1024 &&
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xml))).ToLowerInvariant() == sha256,
            "live item type, XML payload or UTF-8 hash is invalid");
        using var text = new StringReader(xml!);
        using var reader = XmlReader.Create(text, new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
        try { return XDocument.Load(reader).Root ?? throw new InvalidDataException("Native item XML has no root."); }
        catch (XmlException error) { throw new InvalidDataException("Native output observation: invalid or unsafe item XML.", error); }
    }

    private static string ItemXmlValue(XElement root, string name)
    {
        var values = root.Elements(name).ToArray();
        Require(values.Length == 1 && !values[0].HasElements, "live item XML field is missing or repeated: " + name);
        return values[0].Value;
    }
}
