using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class NativeTillingResourceSummary
{
    internal NativeTillingResourceSummary(NativePickupInventorySummary inventory) => Inventory = inventory;

    public NativePickupInventorySummary Inventory { get; }
    public bool PhysicalInventoryDeltaVerified => true;
    public bool MaterialGraphAndMirrorsVerified => true;
    public bool TillingEnergyTerrainAndStatisticVerified => true;
    public bool UnrelatedPersistentStateVerified => true;
    public bool ProducerProvenanceVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

public static partial class NativeOutputObservationBoundary
{
    public static NativeTillingResourceSummary ReadTillingResources(TrainingExecutionRequest request,
        TrainingExecutionResult result, SnapshotEnvelope before, SnapshotEnvelope after,
        SnapshotEnvelope observation, string locationId, MoneyRevenueCropPlantingTile target,
        int toolSlot, string toolId)
    {
        var inventory = ReadTillingPickupInventory(request, result, before, after, locationId, target, toolSlot, toolId);
        CropSeedResourceBoundary.VerifyObservedTillingResources(before, after, observation, locationId,
            target, toolSlot, toolId, inventory);
        return new(inventory);
    }
}
