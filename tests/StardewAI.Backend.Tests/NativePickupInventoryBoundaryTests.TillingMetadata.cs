using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    [Fact]
    public void TillingMetadataWireStaysAbsentByDefaultAndMissingInputsCannotDefault()
    {
        var raw = JsonSerializer.Serialize(new ActionOriginObservationReceipt());
        Assert.DoesNotContain("tilling_tool_metadata_before", raw); Assert.DoesNotContain("tilling_tool_metadata_after", raw);
        var context = JsonSerializer.Deserialize<NativeTillingToolMetadataContext>("{}")!;
        Assert.Null(context.SchemaVersion); Assert.Null(context.LastEquippedToolIsNull);
        Assert.Null(context.SwingTicker); Assert.Null(context.HasBeenInInventory);
    }

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(1, false, true)]
    [InlineData(2, false, true)]
    [InlineData(3, false, true)]
    [InlineData(3, true, false)]
    [InlineData(3, false, false)]
    public void SelectedTillBindsThreeExactMetadataChangesAndLeavesLegacyPickupStrict(int facing, bool history, bool equipped)
    {
        var f = TillingMetadataFixture(facing, history, equipped);
        var originals = JsonSerializer.Serialize(new { f.Request, f.Result, f.Before, f.After });
        Assert.Throws<InvalidDataException>(() => NativeOutputObservationBoundary.ReadPickupInventory(f.Request, f.Result.ActionOriginObservation!, f.Before, f.After, "Farm"));
        var verified = ReadTillingMetadata(f);
        Assert.Empty(verified.Slots); Assert.False(verified.ResourceCreditAuthorized); Assert.False(verified.FormalProductTrainingAuthorized);
        Assert.Equal(originals, JsonSerializer.Serialize(new { f.Request, f.Result, f.Before, f.After }));
    }

    [Theory]
    [InlineData("missing_before")]
    [InlineData("missing_schema")]
    [InlineData("actor")]
    [InlineData("tick")]
    [InlineData("slot")]
    [InlineData("last_equipped")]
    [InlineData("last_type")]
    [InlineData("last_null")]
    [InlineData("facing")]
    [InlineData("forged_facing_and_xml")]
    [InlineData("power")]
    [InlineData("using")]
    [InlineData("animation")]
    [InlineData("counter")]
    [InlineData("frame")]
    [InlineData("history")]
    [InlineData("unknown_xml")]
    [InlineData("result_anchor")]
    [InlineData("result_target")]
    [InlineData("result_power")]
    public void SelectedTillRejectsUnboundContextMetadataOrUnrelatedXml(string change)
    {
        var f = TillingMetadataFixture(); var receipt = f.Result.ActionOriginObservation!;
        var after = receipt.TillingToolMetadataAfter!;
        switch (change)
        {
            case "missing_before": receipt.TillingToolMetadataBefore = null; break;
            case "missing_schema": after.SchemaVersion = null; break;
            case "actor": after.ActorReferenceId = Guid.NewGuid().ToString("N"); break;
            case "tick": after.GameTick++; break;
            case "slot": after.HoeSlotIndex = 0; break;
            case "last_equipped": after.LastEquippedToolReferenceId = Guid.NewGuid().ToString("N"); break;
            case "last_type": after.LastEquippedToolRuntimeType = null; break;
            case "last_null": after.LastEquippedToolIsNull = null; break;
            case "facing": after.FacingDirection = 4; break;
            case "forged_facing_and_xml":
                after.FacingDirection = 0; after.CurrentParentTileIndex = 25;
                var rotated = receipt.InventoryPayloadAfter!.Slots![1]; var rotatedXml = XElement.Parse(rotated.SerializedStateXml!);
                rotatedXml.Element("currentParentTileIndex")!.Value = "25"; SetXml(rotated, rotatedXml); break;
            case "power": after.ToolPower = 1; break;
            case "using": after.UsingTool = true; break;
            case "animation": after.PauseForSingleAnimation = true; break;
            case "counter": after.SwingTicker++; break;
            case "frame": after.CurrentParentTileIndex++; break;
            case "history": after.HasBeenInInventory = false; break;
            case "unknown_xml":
                var slot = receipt.InventoryPayloadAfter!.Slots![1]; var xml = XElement.Parse(slot.SerializedStateXml!);
                xml.Add(new XElement("unknown_persistent", 1)); SetXml(slot, xml); break;
            case "result_anchor": f.Result.ProductAfterStateHash = "wrong"; break;
            case "result_target": f.Result.TargetTileX++; break;
            case "result_power": f.Result.ToolPower = 1; break;
        }
        Assert.Throws<InvalidDataException>(() => ReadTillingMetadata(f));
    }

    [Fact]
    public void CompletedSouthBindsLiteralNativeResetAndRejectsLastAnimatingSprite()
    {
        var f = TillingMetadataFixture(facing: 2);
        var receipt = f.Result.ActionOriginObservation!;
        var slot = receipt.InventoryPayloadAfter!.Slots![1];
        var xml = XElement.Parse(slot.SerializedStateXml!);
        // Native J22 ends at21 after the completion/Halt reset, not the animating22.
        receipt.TillingToolMetadataAfter!.CurrentParentTileIndex = 21;
        xml.Element("currentParentTileIndex")!.Value = "21"; SetXml(slot, xml);
        _ = ReadTillingMetadata(f);
        receipt.TillingToolMetadataAfter.CurrentParentTileIndex = 22;
        xml.Element("currentParentTileIndex")!.Value = "22"; SetXml(slot, xml);
        var error = Assert.Throws<InvalidDataException>(() => ReadTillingMetadata(f));
        Assert.Contains("completed-cycle projection", error.Message);
    }

    private sealed record TillingFixture(TrainingExecutionRequest Request, TrainingExecutionResult Result, SnapshotEnvelope Before, SnapshotEnvelope After);
    private static NativePickupInventorySummary ReadTillingMetadata(TillingFixture f) => NativeOutputObservationBoundary.ReadTillingPickupInventory(
        f.Request, f.Result, f.Before, f.After, "Farm", new(71, 16), 1, "(T)Hoe");

    private static TillingFixture TillingMetadataFixture(int facing = 3, bool history = false, bool equipped = true)
    {
        var (request, receipt) = NativeInventoryGraphBoundaryTests.Fixture();
        request.ActionOriginTillingResources = request.ActionOriginDebrisState = true;
        request.OptionId = request.SourcePlanningOptionId = "executor.till_soil"; request.ToolSlotIndex = 1;
        receipt.BeforeDebris = receipt.AfterDebris = []; receipt.Collections = []; receipt.ClearanceToolCalls = [];
        var before = receipt.InventoryPayloadBefore!; var after = receipt.InventoryPayloadAfter!;
        after.Slots![0].Stack = before.Slots![0].Stack; after.Slots[0].SerializedStateXml = before.Slots[0].SerializedStateXml;
        after.Slots[0].SerializedStateSha256 = before.Slots[0].SerializedStateSha256;
        after.GraphState!.Nodes![0].Stack = after.Slots[0].Stack;
        foreach (var endpoint in new[] { before, after })
        {
            var tool = endpoint.Slots![1]; tool.RuntimeType = "StardewValley.Tools.Hoe"; tool.QualifiedItemId = "(T)Hoe";
            endpoint.GraphState!.Nodes![1].RuntimeType = tool.RuntimeType; endpoint.GraphState.Nodes[1].QualifiedItemId = tool.QualifiedItemId;
            endpoint.SelectionTransientState!.CurrentItem!.RuntimeType = tool.RuntimeType;
            var isAfter = ReferenceEquals(endpoint, after); var xml = XElement.Parse(tool.SerializedStateXml!); xml.Name = "Hoe";
            xml.Element("itemId")!.Value = "Hoe";
            var sprite = isAfter ? NativeHoeCompletedCycleMetadata.ExpectedSpriteIndex(21, facing) : 21;
            var hasHistory = isAfter ? history || equipped : history;
            xml.Add(new XElement("initialParentTileIndex", 21), new XElement("InitialParentTileIndex", 21),
                new XElement("currentParentTileIndex", sprite), new XElement("swingTicker", isAfter ? 8 : 7),
                new XElement("hasBeenInInventory", hasHistory ? "true" : "false")); SetXml(tool, xml);
            var context = new NativeTillingToolMetadataContext { SchemaVersion = "native_tilling_tool_metadata_context.v1", GameTick = endpoint.GameTick,
                ActorReferenceId = endpoint.SelectionTransientState.ActorReferenceId, HoeReferenceId = tool.ItemReferenceId, HoeSlotIndex = 1,
                LastEquippedToolIsNull = !isAfter && equipped, LastEquippedToolReferenceId = !isAfter && equipped ? null : tool.ItemReferenceId,
                LastEquippedToolRuntimeType = !isAfter && equipped ? null : tool.RuntimeType, FacingDirection = facing,
                CurrentToolSlotIndex = 1, ToolPower = 0, UsingTool = false, PauseForSingleAnimation = false, CanReleaseTool = false,
                InitialParentTileIndex = 21, CurrentParentTileIndex = sprite, SwingTicker = isAfter ? 8 : 7, HasBeenInInventory = hasHistory };
            if (isAfter) receipt.TillingToolMetadataAfter = context; else receipt.TillingToolMetadataBefore = context;
        }
        ActionOriginInventoryCount[] Counts(ActionOriginInventoryPayload endpoint) => endpoint.Slots!.Where(slot => slot.IsEmpty == false)
            .Select(slot => new ActionOriginInventoryCount { QualifiedItemId = slot.QualifiedItemId!, Quality = slot.Quality!.Value, Quantity = slot.Stack!.Value }).ToArray();
        receipt.InventoryBefore = Counts(before); receipt.InventoryAfter = Counts(after);
        var first = Snapshot(before); var next = Snapshot(after);
        var player = JsonNode.Parse(next.State["player"].GetRawText())!.AsObject();
        player["facing_direction"] = JsonSerializer.SerializeToNode(new { status = "available", value = facing });
        next.State["player"] = JsonSerializer.SerializeToElement(player); next.StateHash = SnapshotHash.ComputeStateHash(next.State);
        request.BeforeStateHash = receipt.BeforeStateHash = first.StateHash;
        var result = new TrainingExecutionResult { RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = first.StateHash, OptionId = request.OptionId, Status = "applied", PrimitiveKind = "till_soil",
            PrimitiveVerificationStatus = "verified", ToolPower = 0, ToolQualifiedItemId = "(T)Hoe", TargetLocation = "Farm",
            TargetTileX = 71, TargetTileY = 16, ProductAfterStateHash = next.StateHash, ProductAfterGameTick = next.GameTick,
            ActionOriginObservation = receipt };
        return new(request, result, first, next);
    }
}
