using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private static void PrepareRefillPickupRawSlots(JsonObject player)
    {
        var slots = player["inventory"]!["value"]!.AsArray();
        foreach (var slot in slots.OfType<JsonObject>().Where(row => row["is_empty"]!.GetValue<bool>() == false))
        {
            slot["runtime_type"] = "StardewValley.Object";
            slot["maximum_stack_size"] = 999;
        }
        while (slots.Count < 12) slots.Add(new JsonObject
        {
            ["slot_index"] = slots.Count, ["is_empty"] = true,
            ["qualified_item_id"] = null, ["quality"] = null, ["stack"] = null
        });
        player["inventory_capacity"] = JsonSerializer.SerializeToNode(new { status = "available", value = new { max_items = 12 } });
    }

    private static ActionOriginInventoryPayload RefillPickupPayload(SnapshotEnvelope snapshot, string[] references)
    {
        var slots = snapshot.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray().Select(row =>
        {
            var index = row.GetProperty("slot_index").GetInt32();
            if (row.GetProperty("is_empty").GetBoolean()) return new ActionOriginInventoryPayloadSlot { SlotIndex = index, IsEmpty = true };
            var id = row.GetProperty("qualified_item_id").GetString()!;
            var quality = row.GetProperty("quality").GetInt32(); var stack = row.GetProperty("stack").GetInt32();
            var xml = $"<Object><itemId>{id[3..]}</itemId><quality>{quality}</quality><stack>{stack}</stack>" +
                $"<bigCraftable>false</bigCraftable><name>{id[3..]}</name><hasBeenInInventory>true</hasBeenInInventory><orderData/></Object>";
            return new ActionOriginInventoryPayloadSlot { SlotIndex = index, IsEmpty = false, ItemReferenceId = references[index],
                RuntimeType = "StardewValley.Object", QualifiedItemId = id, Quality = quality, Stack = stack, IsTool = false,
                SerializedStateXml = xml, SerializedStateSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xml))).ToLowerInvariant() };
        }).ToArray();
        return new() { SchemaVersion = "native_inventory_payload.v1", PayloadKind = "persisted_xml", GameTick = snapshot.GameTick,
            ActorId = snapshot.PlayerId.Value ?? throw new InvalidDataException("Fixture actor missing."), LocationId = "Farm", SelectionObserved = true, SelectedSlotIndex = 0,
            SelectedItemReferenceId = references[0], CurrentToolReferenceId = null, Slots = slots };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefillPickupInventoryUsesOriginalCompiledFramesAndRetainedHashes(bool repair)
    {
        using var f = new RefillTraceFixture(repair, completeSnapshotInventory: true, observePickupInventory: true);
        await f.Build();
        var compilation = f.Approach.Open();
        var trace = CropTeacherRefillApproachNativeTrace.OpenWithPickupInventory(compilation, f.ReceiptPath, f.AfterPath,
            f.Approach.Watering.Shared.Runtime.LiveOptions.RunId);
        Assert.True(trace.PickupInventoryCaptureVerified);
        Assert.True(trace.SnapshotInventoryContinuityVerified);
        Assert.Equal(f.Approach.Queue.Items.Select(row => row.QueueItemId), trace.PickupInventory.Select(row => row.QueueItemId));
        Assert.All(trace.PickupInventory, row => { Assert.True(row.Inventory.CaptureSlotEffectsVerified); Assert.Empty(row.Inventory.Slots); });
        Assert.Equal(f.OpenWithResourceEvidence().InputHashes.OrderBy(row => row.Key), trace.InputHashes.OrderBy(row => row.Key));
        Assert.False(trace.ResourceCreditAuthorized);
        Assert.False(trace.NativeOutcomeVerified);
        Assert.False(trace.FormalProductTrainingAuthorized);
        File.AppendAllText(f.Journals[0] + ".gz", " ");
        Assert.Throws<InvalidDataException>(trace.ValidateInputsUnchanged);
    }

    [Fact]
    public async Task RefillPickupInventoryDoesNotTurnOptionalCalibrationIntoAnOldEntryRequirement()
    {
        using var f = new RefillTraceFixture(repair: false, completeSnapshotInventory: true);
        await f.Build();
        Assert.False(f.OpenWithResourceEvidence().PickupInventoryCaptureVerified);
        Assert.Throws<InvalidDataException>(() => CropTeacherRefillApproachNativeTrace.OpenWithPickupInventory(
            f.Approach.Open(), f.ReceiptPath, f.AfterPath, f.Approach.Watering.Shared.Runtime.LiveOptions.RunId));
    }
}
