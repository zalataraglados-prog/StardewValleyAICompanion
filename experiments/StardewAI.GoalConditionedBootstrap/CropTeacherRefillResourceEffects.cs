using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class NativeRefillFrameResourceEffects
{
    internal NativeRefillFrameResourceEffects(NativeOutputResourceEvidence? evidence,
        NativeWildTreeClearanceSummary? tree, NativePickupResourceSummary? pickup, bool neutral)
    { Evidence = evidence; WildTree = tree; Pickup = pickup; ResourceNeutralBoundaryVerified = neutral; }

    public NativeOutputResourceEvidence? Evidence { get; }
    public NativeWildTreeClearanceSummary? WildTree { get; }
    public NativePickupResourceSummary? Pickup { get; }
    public bool ResourceNeutralBoundaryVerified { get; }
    public bool SpecificPersistentResourceBoundaryVerified => true;
    public bool ProducerProvenanceVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

internal static class CropTeacherRefillResourceEffects
{
    internal static NativeRefillFrameResourceEffects Read(TrainingExecutionRequest request,
        ActionOriginObservationReceipt? receipt, SnapshotEnvelope before, SnapshotEnvelope after, string locationId)
    {
        Require(request.OptionId is "executor.move_to_tile" or "executor.clear_obstacle",
            "original refill resource effects require a move or explicit clearance");
        if (request.OptionId == "executor.clear_obstacle")
        {
            Require(receipt is not null, "original clearance resource observation is unavailable");
            var tree = NativeOutputObservationBoundary.ReadWildTreeClearanceResources(request, receipt!, before, after, locationId);
            return new(tree.Terrain.Consumption.ResourceEvidence, tree, null, false);
        }
        Require(receipt is not null || request.ActionOriginObservationScopeId is null,
            "requested original move observation is unavailable");
        var evidence = receipt is null ? null : NativeOutputObservationBoundary.ReadResourceEvidence(request, receipt, before, after, locationId);
        if (evidence is null || evidence.ObservedPickupUnits == 0)
        {
            Require(FullShipmentRecoverySettlementBoundaryProjector.TryProject(before, out var first) &&
                    FullShipmentRecoverySettlementBoundaryProjector.TryProject(after, out var next) && first == next,
                "original move without observed pickup changed its persistent-resource boundary");
            return new(evidence, null, null, true);
        }
        var pickup = NativeOutputObservationBoundary.ReadPickupResources(request, receipt!, before, after, locationId, evidence);
        return new(evidence, null, pickup, false);
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException("Original refill resources: " + reason + ".");
    }
}
