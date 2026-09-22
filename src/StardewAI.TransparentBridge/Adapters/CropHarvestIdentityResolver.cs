using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

internal static class CropHarvestIdentityResolver
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>>
        NativeWildSeedOutputs = new Dictionary<string, HashSet<string>>(
            StringComparer.Ordinal)
        {
            ["495"] = new(StringComparer.Ordinal)
                { "(O)16", "(O)18", "(O)20", "(O)22" },
            ["496"] = new(StringComparer.Ordinal)
                { "(O)396", "(O)398", "(O)402" },
            ["497"] = new(StringComparer.Ordinal)
                { "(O)404", "(O)406", "(O)408", "(O)410" },
            ["498"] = new(StringComparer.Ordinal)
                { "(O)412", "(O)414", "(O)416", "(O)418" }
        };

    public static CropHarvestIdentity Resolve(Crop crop)
    {
        if (crop.GetType() == typeof(Crop) &&
            crop.forageCrop.Value &&
            string.Equals(
                crop.whichForageCrop.Value,
                Crop.forageCrop_springOnionID,
                StringComparison.Ordinal))
        {
            return Exact(
                "399",
                "exact_from_decompiled_native_spring_onion_branch",
                false,
                string.Empty);
        }

        var sourceSeedId = crop.whichForageCrop.Value ?? string.Empty;
        if (crop.GetType() == typeof(Crop) &&
            crop.isWildSeedCrop() &&
            NativeWildSeedOutputs.TryGetValue(sourceSeedId, out var domain))
        {
            var resolvedOutput = crop.replaceWithObjectOnFullGrown.Value ??
                string.Empty;
            var qualifiedOutput = QualifyObjectId(resolvedOutput);
            if (domain.Contains(qualifiedOutput))
            {
                return Exact(
                    UnqualifyObjectId(qualifiedOutput),
                    "exact_from_live_native_wild_seed_replacement",
                    true,
                    sourceSeedId);
            }

            return new CropHarvestIdentity(
                string.Empty,
                string.Empty,
                string.IsNullOrWhiteSpace(resolvedOutput)
                    ? "unavailable_native_wild_seed_replacement_missing"
                    : "unavailable_native_wild_seed_replacement_outside_decompiled_domain",
                true,
                false,
                sourceSeedId);
        }

        var itemId = crop.indexOfHarvest.Value ?? string.Empty;
        return string.IsNullOrWhiteSpace(itemId)
            ? new CropHarvestIdentity(
                string.Empty,
                string.Empty,
                "unavailable_no_live_harvest_item_id",
                false,
                false,
                string.Empty)
            : Exact(
                itemId,
                "exact_from_live_index_of_harvest",
                false,
                string.Empty);
    }

    private static CropHarvestIdentity Exact(
        string itemId,
        string status,
        bool stochasticOutcome,
        string sourceSeedId) => new(
            UnqualifyObjectId(itemId),
            QualifyObjectId(itemId),
            status,
            stochasticOutcome,
            true,
            sourceSeedId);

    private static string QualifyObjectId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return string.Empty;
        return ItemRegistry.QualifyItemId(itemId) ??
            (itemId.StartsWith('(') ? itemId : "(O)" + itemId);
    }

    private static string UnqualifyObjectId(string itemId) =>
        itemId.StartsWith("(O)", StringComparison.Ordinal)
            ? itemId[3..]
            : itemId;
}

internal sealed record CropHarvestIdentity(
    string ItemId,
    string QualifiedItemId,
    string ProjectionStatus,
    bool StochasticOutcome,
    bool StochasticOutcomeResolved,
    string SourceSeedId);
