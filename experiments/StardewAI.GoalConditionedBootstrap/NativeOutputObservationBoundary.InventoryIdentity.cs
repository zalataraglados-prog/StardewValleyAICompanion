using System.Globalization;
using System.Xml.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private static void ValidateInventoryXmlIdentity(XElement root, NativeInventoryPayloadType descriptor,
        string qualifiedItemId, int quality, int stack)
    {
        var id = ItemXmlValue(root, "itemId");
        var qualifierEnd = qualifiedItemId.IndexOf(')');
        Require(qualifiedItemId.StartsWith('(') && qualifierEnd > 1 && qualifiedItemId[(qualifierEnd + 1)..] == id &&
                !string.IsNullOrWhiteSpace(id) &&
                int.TryParse(ItemXmlValue(root, "quality"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var xmlQuality) && xmlQuality == quality &&
                int.TryParse(ItemXmlValue(root, "stack"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var xmlStack) && xmlStack == stack,
            "persisted inventory XML identity, quality or stack differs from its actual header");
        var qualifier = descriptor.FixedQualifier;
        if (descriptor.IdentityRule == NativeInventoryPayloadIdentityRule.ObjectBigCraftable)
        {
            Require(bool.TryParse(ItemXmlValue(root, "bigCraftable"), out var big), "inventory Object bigCraftable discriminator is unavailable");
            qualifier = big ? "(BC)" : "(O)";
        }
        else if (descriptor.IdentityRule == NativeInventoryPayloadIdentityRule.ClothingType)
        {
            var value = ItemXmlValue(root, "clothesType");
            Require(value is "SHIRT" or "PANTS" || int.TryParse(value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out _), "inventory clothing type discriminator is not a native enum value");
            qualifier = value == "PANTS" || int.TryParse(value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var numeric) && numeric == 1 ? "(P)" : "(S)";
        }
        Require(qualifiedItemId == qualifier + id, "inventory qualification differs from its native type/XML");
    }
}
