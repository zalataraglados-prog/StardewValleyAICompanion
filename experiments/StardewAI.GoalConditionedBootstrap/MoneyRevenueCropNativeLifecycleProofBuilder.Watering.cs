using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    internal static void VerifyMaintenanceLocation(SnapshotEnvelope before, SnapshotEnvelope after, string location) =>
        Require(ReadPlayerLocation(before) == location && ReadPlayerLocation(after) == location,
            "Crop watering maintenance left its original action location.");

    internal static MoneyRevenueCropPlantingTile ReadMaintenanceTarget(ActionQueueItem item, SnapshotEnvelope source,
        string location, string commitmentId)
    {
        Require(UniqueParameter(item.NormalizedCommand.Parameters, "action_commitment_id") == commitmentId,
            "Crop watering queue owner differs from its original batch.");
        var tile = ReadTarget(item, location, source);
        return new(tile.X, tile.Y);
    }

    internal static void VerifyWateringMaintenanceTile(SnapshotEnvelope before, SnapshotEnvelope after,
        string location, MoneyRevenueCropPlantingTile tile, CropPlantingCommitment crop)
    {
        var first = ReadCrop(before, location, tile.TileX, tile.TileY);
        var next = ReadCrop(after, location, tile.TileX, tile.TileY);
        Require(first is not null && next is not null && !first.Dead && !next.Dead && !first.Watered &&
                CropIdentityMatches(first, crop.SeedQualifiedItemId, crop.HarvestItemQualifiedId) &&
                CropIdentityMatches(next, crop.SeedQualifiedItemId, crop.HarvestItemQualifiedId),
            "Crop watering maintenance target is not the original live dry crop.");
        ValidateNativeWatering(first!, next!);
    }
}
