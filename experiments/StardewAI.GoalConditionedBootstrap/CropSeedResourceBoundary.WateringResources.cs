using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static void VerifyObservedWateringResources(SnapshotEnvelope before, SnapshotEnvelope after,
        string location, MoneyRevenueCropPlantingTile tile, bool applied, NativePickupInventorySummary pickup)
    {
        Require(FullShipmentRecoverySettlementBoundaryProjector.TryProject(before, out _) &&
                FullShipmentRecoverySettlementBoundaryProjector.TryProject(after, out _),
            "Observed crop watering lacks the original readable persistent resource boundary.");
        var comparison = ProjectVerifiedWatering(before, after, location, tile, applied);
        // Slots, stock graph and derived mirrors still consume the raw frames.
        // Only the final invariant uses the independently checked watering view.
        VerifyObservedPickupCore(before, after, pickup, comparison);
    }
}
