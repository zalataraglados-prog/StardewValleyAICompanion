using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    internal static void VerifyOwnedHarvestSource(SnapshotEnvelope source, string location, MoneyRevenueCropPlantingTile tile,
        string seedId, string harvestId)
    {
        var crop = ReadCrop(source, location, tile.TileX, tile.TileY);
        Require(crop is not null && !crop.Dead && crop.ReadyForHarvest && CropIdentityMatches(crop, seedId, harvestId),
            "Owned harvest target is not its original ready live commitment crop.");
    }

    internal static int? VerifyOwnedHarvestTarget(SnapshotEnvelope before, SnapshotEnvelope after, string location, int x, int y)
    {
        var first = ReadCrop(before, location, x, y);
        var next = ReadCrop(after, location, x, y);
        Require(first is not null && !first.Dead, "Owned crop harvest target is unavailable or dead.");
        var crop = first!;
        Require(next is null || !next.Dead && CropIdentityMatches(next, NormalizeObjectId(crop.SeedId), crop.HarvestQualifiedItemId),
            "Owned crop harvest target identity drifted.");
        var catalog = ReadCropCatalogRow(before, NormalizeObjectId(crop.SeedId));
        ValidateNativeHarvestTransition(crop, next, catalog);
        return catalog.RegrowDays;
    }

    private static void ValidateNativeHarvestTransition(CropState before, CropState? after, CropCatalogRow catalog)
    {
        if (!catalog.RegrowDays.HasValue)
        {
            Require(before.ReadyForHarvest && after is null,
                "Money Revenue native one-shot harvest transition is not exact.");
            return;
        }
        Require(before.ReadyForHarvest, "Money Revenue crop harvest was not performed on a ready crop.");
        Require(after is not null && after.FullyGrown && !after.ReadyForHarvest &&
                after.DayOfCurrentPhase == catalog.RegrowDays && after.CurrentPhase == before.CurrentPhase,
            "Money Revenue native regrow harvest transition is not exact.");
    }
}
