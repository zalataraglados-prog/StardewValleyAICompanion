using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record NativePickupResourceSummary(NativePickupInventorySummary Inventory)
{
    public bool PhysicalInventoryDeltaVerified => true;
    public bool MaterialGraphAndMirrorsVerified => true;
    public bool UnrelatedPersistentStateVerified => true;
    public bool ProducerProvenanceVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

public static partial class NativeOutputObservationBoundary
{
    // Physical stock is not production attribution or an owner/ledger commit.
    public static NativePickupResourceSummary ReadPickupResources(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, SnapshotEnvelope before, SnapshotEnvelope after, string locationId) =>
        ReadPickupResources(request, receipt, before, after, locationId, ReadResourceEvidence(request, receipt, before, after, locationId));

    internal static NativePickupResourceSummary ReadPickupResources(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, SnapshotEnvelope before, SnapshotEnvelope after, string locationId,
        NativeOutputResourceEvidence evidence)
    {
        var inventory = ReadPickupInventory(request, receipt, before, after, locationId, evidence);
        CropSeedResourceBoundary.VerifyObservedPickup(before, after, inventory);
        return new(inventory);
    }
}
