using System.Globalization;
using System.Xml.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    // New selected-till consumer; the original pickup entry keeps its exact no-tool-mutation policy.
    public static NativePickupInventorySummary ReadTillingPickupInventory(TrainingExecutionRequest request,
        TrainingExecutionResult result, SnapshotEnvelope before, SnapshotEnvelope after, string locationId,
        MoneyRevenueCropPlantingTile target, int toolSlot, string toolId)
    {
        Require(NativeTillingObservationContract.IsCompleteRequest(request) && locationId == "Farm" &&
            result.RunId == request.RunId && result.QueueId == request.QueueId && result.QueueItemId == request.QueueItemId &&
            result.BeforeStateHash == before.StateHash && result.BeforeStateHash == request.BeforeStateHash &&
            result.OptionId == request.OptionId && result.Status == "applied" && result.PrimitiveKind == "till_soil" &&
            result.PrimitiveVerificationStatus == "verified" && result.ToolPower == 0 && result.ToolQualifiedItemId == toolId &&
            result.TargetLocation == locationId && result.TargetTileX == target.TileX && result.TargetTileY == target.TileY &&
            result.ProductAfterStateHash == after.StateHash && result.ProductAfterGameTick == after.GameTick &&
            result.ActionOriginObservation is not null,
            "tilling metadata requires its original successful power-zero till and exact native anchor");
        var receipt = result.ActionOriginObservation!;
        var evidence = ReadResourceEvidence(request, receipt, before, after, locationId);
        var raw = ReadDebrisState(request, receipt, before.GameTick, after.GameTick, locationId, before.PlayerId.Value!);
        var payload = raw.InventoryGraph.Payload;
        var facing = NativeExecutionSourceSupport.Value(after, "player", "facing_direction");
        Require(facing.TryGetInt32(out var actualFacing) && receipt.TillingToolMetadataAfter?.FacingDirection == actualFacing,
            "tilling metadata facing differs from the original anchored actor snapshot");
        var metadata = VerifyTillingToolMetadata(request, receipt, raw.InventoryGraph, toolSlot, toolId);
        return ReadPickupInventoryCore(request, receipt, before, after, locationId, evidence, payload, metadata);
    }

    private sealed record VerifiedTillingToolMetadata(int Slot, string Reference, int Sprite, int Swing, bool History)
    {
        internal XElement ProjectExpected(NativeObservedInventoryPayloadSlot slot, XElement original)
        {
            if (slot.SlotIndex != Slot) return original;
            Require(slot.ItemReferenceId == Reference && slot.RuntimeType == "StardewValley.Tools.Hoe",
                "tilling metadata projection left its verified physical Hoe");
            var expected = new XElement(original);
            _ = ItemXmlValue(expected, "currentParentTileIndex"); _ = ItemXmlValue(expected, "swingTicker");
            _ = ItemXmlValue(expected, "hasBeenInInventory");
            expected.Element("currentParentTileIndex")!.Value = Sprite.ToString(CultureInfo.InvariantCulture);
            expected.Element("swingTicker")!.Value = Swing.ToString(CultureInfo.InvariantCulture);
            expected.Element("hasBeenInInventory")!.Value = History ? "true" : "false";
            return expected;
        }
    }
}
