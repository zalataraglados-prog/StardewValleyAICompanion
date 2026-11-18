using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class NativeWateringResourceSummary
{
    internal NativeWateringResourceSummary(NativePickupInventorySummary inventory) => Inventory = inventory;
    public NativePickupInventorySummary Inventory { get; }
    public bool PhysicalInventoryDeltaVerified => true;
    public bool MaterialGraphAndMirrorsVerified => true;
    public bool WateringCropCanEnergyAndBowlsVerified => true;
    public bool PersistedToolMetadataVerified => true;
    public bool UnrelatedPersistentStateVerified => true;
    public bool ProducerProvenanceVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

public static partial class NativeOutputObservationBoundary
{
    public static NativeWateringResourceSummary ReadWateringResources(TrainingExecutionRequest request,
        TrainingExecutionResult result, SnapshotEnvelope before, SnapshotEnvelope after,
        string locationId, MoneyRevenueCropPlantingTile target)
    {
        var applied = result.Status == "applied";
        Require(NativeWateringObservationContract.IsCompleteRequest(request) &&
            result.RunId == request.RunId && result.QueueId == request.QueueId && result.QueueItemId == request.QueueItemId &&
            result.BeforeStateHash == request.BeforeStateHash && result.BeforeStateHash == before.StateHash &&
            result.OptionId == request.OptionId && result.Status is "applied" or "blocked" &&
            result.PrimitiveKind == "water_crop" && (!applied || result.PrimitiveVerificationStatus == "verified") &&
            result.WateredCount == (applied ? 1 : 0) && (!applied || result.TargetLocation == locationId &&
                result.TargetTileX == target.TileX && result.TargetTileY == target.TileY) &&
            result.ProductAfterStateHash == after.StateHash && result.ProductAfterGameTick == after.GameTick &&
            result.ActionOriginObservation is not null,
            "watering resources require the original native water frame and exact snapshot anchor");
        var receipt = result.ActionOriginObservation!;
        var evidence = ReadResourceEvidence(request, receipt, before, after, locationId);
        var graph = ReadDebrisState(request, receipt, before.GameTick, after.GameTick, locationId,
            before.PlayerId.Value ?? throw new InvalidDataException("Watering actor is unavailable.")).InventoryGraph;
        var can = NativeExecutionSourceSupport.Value(before, "player", "watering_can_state");
        Require(can.TryGetProperty("slot_index", out var slotValue) && slotValue.TryGetInt32(out _),
            "watering resources lack the original selected can slot");
        var slot = slotValue.GetInt32();
        Require(can.TryGetProperty("water_left", out var waterValue) && waterValue.TryGetInt32(out _),
            "watering resources lack the original can water baseline");
        var tool = graph.Payload.Before.Slots.SingleOrDefault(row => row.SlotIndex == slot);
        Require(tool is { IsEmpty: false, IsTool: true, RuntimeType: "StardewValley.Tools.WateringCan" } &&
            (!applied || tool.QualifiedItemId == result.ToolQualifiedItemId), "watering resources changed the original native can identity");
        // Verify primary deltas once, then use their checked water value while replaying
        // XML metadata. Only a fully verified pickup reaches the final raw stock checks.
        var pickup = CropSeedResourceBoundary.VerifyObservedWateringWithPickup(before, after, locationId, target, applied,
            expectedWater => ReadPickupInventoryCore(request, receipt, before, after, locationId, evidence, graph.Payload,
                VerifyWateringToolMetadata(receipt, graph, slot, tool!.QualifiedItemId!, waterValue.GetInt32(), expectedWater)));
        return new(pickup);
    }
}
