using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    // Selected crop growth only; the caller binds the original sleep/save transition.
    internal static void VerifyOwnedDayGrowth(SnapshotEnvelope before, SnapshotEnvelope after, string location,
        MoneyRevenueCropPlantingTile tile, string seedId, string harvestId)
    {
        var first = ReadCrop(before, location, tile.TileX, tile.TileY);
        var next = ReadCrop(after, location, tile.TileX, tile.TileY);
        Require(first is not null && next is not null && !first.Dead && !next.Dead &&
                CropIdentityMatches(first, seedId, harvestId) && CropIdentityMatches(next, seedId, harvestId),
            "Owned crop day-growth target is unavailable, dead or differs from its original commitment crop.");
        var catalog = ReadCropCatalogRow(before, NormalizeObjectId(first!.SeedId));
        ValidateNativeDayGrowth(first, next, tile, seedId, harvestId, catalog.NeedsWatering);
    }

    private static void ValidateNativeDayGrowth(CropState? before, CropState? after,
        MoneyRevenueCropPlantingTile tile, string seedId, string harvestId, bool needsWatering)
    {
        Require(before is not null && after is not null &&
                CropIdentityMatches(before, seedId, harvestId) &&
                CropIdentityMatches(after, seedId, harvestId),
            "Money Revenue crop disappeared across native sleep.");
        var verifiedBefore = before ?? throw new InvalidDataException(
            "Money Revenue crop disappeared across native sleep.");
        var verifiedAfter = after ?? throw new InvalidDataException(
            "Money Revenue crop disappeared across native sleep.");
        var projected = ProjectNativeDay(verifiedBefore, needsWatering);
        Require(SameNativeDayState(projected, verifiedAfter),
            "Money Revenue crop state did not follow Crop.newDay on tile " +
            tile.TileX + "," + tile.TileY + ".");
    }
}
