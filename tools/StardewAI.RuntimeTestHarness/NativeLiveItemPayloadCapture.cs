using System.Security.Cryptography;
using System.Text;
using StardewAI.Contracts.Training;
using StardewAI.RuntimePrimitives;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Objects;
using StardewValley.SaveSerialization;

namespace StardewAI.RuntimeTestHarness;

internal sealed record NativeLiveItemPayload(string Xml, string Sha256);

// Persisted LIVE payload mechanics only. Admission policy, scope budgets and native purity
// calibration belong to the caller. In particular this is NOT a normalized unit-state hash.
internal static partial class NativeLiveItemPayloadCapture
{
    private const int MaximumGraphDepth = NativeInventoryGraphLimits.MaximumDepth;
    private const int MaximumGraphNodes = NativeInventoryGraphLimits.MaximumRootOccurrences;

    internal static NativeLiveItemPayload Capture(Item item, int maximumCharacters)
    {
        if (maximumCharacters is < 1 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        ArgumentNullException.ThrowIfNull(item);
        new GraphCheck().Visit(item, 1);
        using var writer = new CappedNativeObservationWriter(maximumCharacters);
        var payload = CaptureWithWriter(item, writer, maximumCharacters);
        ValidateXml(payload.Xml, maximumCharacters);
        return payload;
    }

    // Existing observation callers retain writer ownership and its limit-failure diagnostic.
    internal static NativeLiveItemPayload CaptureWithWriter(Item item, CappedNativeObservationWriter writer, int maximumCharacters)
    {
        if (maximumCharacters is < 1 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(writer);

        // Preserve the original observer's EXACT TextWriter overload/options/formatting.
        // Do not wrap in XmlWriter, construct/clone an Item, or normalize any live fields.
        SaveSerializer.GetSerializer(item.GetType()).Serialize(writer, item);
        var xml = writer.ToString();
        return new NativeLiveItemPayload(xml,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xml))).ToLowerInvariant());
    }

    // Exact native types with known Item edges. Other native graph families (e.g. Chest,
    // Furniture, Sign, Trinket) fail explicitly until their own edges have been vetted.
    private static bool SupportedItem(Type type) => type.Assembly == typeof(Item).Assembly &&
        NativeInventoryPayloadTypes.TryGet(type.FullName, out var descriptor) &&
        descriptor.IsTool == typeof(Tool).IsAssignableFrom(type);

}
