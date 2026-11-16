using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static void VerifyObservedTillingResources(SnapshotEnvelope before, SnapshotEnvelope after,
        SnapshotEnvelope observation, string location, MoneyRevenueCropPlantingTile tile,
        int toolSlot, string toolId, NativePickupInventorySummary pickup)
    {
        var energy = ProjectVerifiedTillingEnergy(before, after, observation, toolSlot, toolId);
        var comparison = ProjectVerifiedTillingTerrainAndStatistic(before, energy, location, tile);
        // Graph, slot, capacity and house checks keep the raw frames. Only the final
        // persistent invariant consumes the already verified till comparison view.
        VerifyObservedPickupCore(before, after, pickup, comparison);
    }
}
