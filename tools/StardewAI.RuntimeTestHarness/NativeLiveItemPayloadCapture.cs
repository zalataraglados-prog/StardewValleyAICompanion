using System.Security.Cryptography;
using System.Text;
using StardewAI.RuntimePrimitives;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Objects;
using StardewValley.SaveSerialization;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

internal sealed record NativeLiveItemPayload(string Xml, string Sha256);

// Persisted LIVE payload mechanics only. Admission policy, scope budgets and native purity
// calibration belong to the caller. In particular this is NOT a normalized unit-state hash.
internal static partial class NativeLiveItemPayloadCapture
{
    private const int MaximumGraphDepth = 16;
    private const int MaximumGraphNodes = 256;

    internal static NativeLiveItemPayload Capture(Item item, int maximumCharacters)
    {
        if (maximumCharacters is < 1 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        ArgumentNullException.ThrowIfNull(item);
        new GraphCheck().Visit(item, 1);

        // Preserve the original observer's EXACT TextWriter overload/options/formatting.
        // Do not wrap in XmlWriter, construct/clone an Item, or normalize any live fields.
        using var writer = new CappedNativeObservationWriter(maximumCharacters);
        SaveSerializer.GetSerializer(item.GetType()).Serialize(writer, item);
        var xml = writer.ToString();
        ValidateXml(xml, maximumCharacters);
        return new NativeLiveItemPayload(xml,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xml))).ToLowerInvariant());
    }

    // Exact native types with known Item edges. Other native graph families (e.g. Chest,
    // Furniture, Sign, Trinket) fail explicitly until their own edges have been vetted.
    private static bool SupportedItem(Type type) => type.Assembly == typeof(Item).Assembly &&
        (type == typeof(StardewValley.Object) || type == typeof(ColoredObject) ||
         type == typeof(Boots) || type == typeof(Clothing) || type == typeof(Hat) ||
         type == typeof(Ring) || type == typeof(CombinedRing) || type == typeof(SpecialItem) ||
         type == typeof(Axe) || type == typeof(ErrorTool) || type == typeof(FishingRod) ||
         type == typeof(GenericTool) || type == typeof(Hoe) || type == typeof(MeleeWeapon) ||
         type == typeof(MilkPail) || type == typeof(Pan) || type == typeof(Pickaxe) ||
         type == typeof(Shears) || type == typeof(Slingshot) || type == typeof(Wand) || type == typeof(WateringCan));

    private sealed class GraphCheck
    {
        private readonly HashSet<object> ancestors = new(ReferenceEqualityComparer.Instance);
        private int nodes;

        internal void Visit(Item? item, int depth)
        {
            if (item is null) return;
            var type = item.GetType(); // No virtual Item getter/serializer before type admission.
            if (!SupportedItem(type))
                throw new InvalidDataException("Live item payload runtime type is unsupported: " + type.FullName);
            Enter(item, depth);
            try
            {
                if (item is StardewValley.Object obj)
                {
                    Visit(obj.heldObject.Value, depth + 1);
                    Visit(obj.lastInputItem.Value, depth + 1);
                }
                if (item is Tool tool)
                {
                    CheckCount(tool.attachments.Length);
                    foreach (var attachment in tool.attachments) Visit(attachment, depth + 1);
                    CheckCount(tool.enchantments.Count);
                    foreach (var enchantment in tool.enchantments)
                    {
                        if (enchantment is null) continue;
                        var enchantmentType = enchantment.GetType();
                        // Native enchantments have persisted scalar state (BaseEnchantment.Level),
                        // not Item children. Never call custom enchantment properties or serializers.
                        if (enchantmentType.Assembly != typeof(BaseEnchantment).Assembly ||
                            enchantmentType.Namespace != typeof(BaseEnchantment).Namespace)
                            throw new InvalidDataException("Live item payload enchantment type is unsupported: " + enchantmentType.FullName);
                        Enter(enchantment, depth + 1);
                        ancestors.Remove(enchantment);
                    }
                    CheckCount(tool.previousEnchantments.Count);
                }
                if (item is CombinedRing ring)
                {
                    CheckCount(ring.combinedRings.Count);
                    foreach (var child in ring.combinedRings) Visit(child, depth + 1);
                }
            }
            finally { ancestors.Remove(item); }
        }

        private static void CheckCount(int count)
        {
            if (count is < 0 or > MaximumGraphNodes)
                throw new InvalidDataException("Live item payload graph collection exceeds its bound.");
        }

        private void Enter(object value, int depth)
        {
            // Count every serialized occurrence, including repeated aliases, not just unique
            // references: XmlSerializer expands aliases rather than retaining graph identity.
            if (depth > MaximumGraphDepth || ++nodes > MaximumGraphNodes)
                throw new InvalidDataException("Live item payload graph depth/node bound exceeded.");
            if (!ancestors.Add(value))
                throw new InvalidDataException("Live item payload graph contains a cycle.");
        }
    }
}
