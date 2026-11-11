using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    [Fact]
    public void RetainsNativeEarlyReturnForAnAlreadyReceivedIncomingItem()
    {
        var f = new PickupFixture();
        var input = f.Receipt.Collections[0].InventoryAdmissions[0].Item!;
        var incoming = XElement.Parse(input.SerializedStateXml); incoming.Element("hasBeenInInventory")!.Value = "true"; SetXml(input, incoming);
        foreach (var state in new[] { f.Receipt.InventoryPayloadBefore!, f.Receipt.InventoryPayloadAfter! })
        {
            var slot = state.Slots![0]; var xml = XElement.Parse(slot.SerializedStateXml!);
            xml.Element("hasBeenInInventory")!.Value = "false"; SetXml(slot, xml);
        }
        f.RefreshSnapshots();
        Assert.Equal(16, Assert.Single(f.Read().Slots).StackAfter);
    }

    [Fact]
    public void ExhaustsSequentialAdmissionsAcrossTwoFullNewPhysicalSlots()
    {
        var f = new PickupFixture(newSlot: true);
        SetOrderedAdmissions(f, 999, 999);
        var after = f.Receipt.InventoryPayloadAfter!;
        var extra = JsonSerializer.Deserialize<StardewAI.Contracts.Training.ActionOriginInventoryPayloadSlot>(JsonSerializer.Serialize(after.Slots![0]))!;
        extra.SlotIndex = 2; extra.ItemReferenceId = Guid.NewGuid().ToString("N");
        var xml = XElement.Parse(extra.SerializedStateXml!);
        xml.Descendants("value").Single().Element("string")!.Value = "B"; SetXml(extra, xml);
        SetSlotStack(extra, 999); SetSlotStack(after.Slots[0], 999); after.Slots[2] = extra;
        f.RefreshSnapshots();
        var slots = f.Read().Slots;
        Assert.Equal(new[] { 0, 2 }, slots.Select(slot => slot.SlotIndex));
        Assert.Equal(1998, slots.Sum(slot => slot.StackAfter - slot.StackBefore));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BindsMergedAndNewSlotsToOriginalXmlAndRawSnapshotWithoutProductionCredit(bool newSlot)
    {
        var f = new PickupFixture(newSlot);
        var summary = f.Read();
        var slot = Assert.Single(summary.Slots);
        Assert.Equal(newSlot ? 0 : 10, slot.StackBefore);
        Assert.Equal(newSlot ? 6 : 16, slot.StackAfter);
        Assert.True(summary.CaptureSlotEffectsVerified);
        Assert.Equal(0, summary.BeforeCaptureGapTicks);
        Assert.False(summary.PersistentResourceBoundaryVerified);
        Assert.False(summary.ResourceCreditAuthorized);
        Assert.False(summary.ProducerProvenanceVerified);
        Assert.False(summary.NativeOutcomeVerified);
        Assert.False(summary.FormalProductTrainingAuthorized);
    }

    [Fact]
    public void NativeStackingPreservesResidentModDataInsteadOfRequiringInputXmlEquality()
    {
        var f = new PickupFixture();
        var input = f.Receipt.Collections[0].InventoryAdmissions[0].Item!;
        var xml = XElement.Parse(input.SerializedStateXml); xml.Add(new XElement("sourceOnly", "not-resident")); SetXml(input, xml);
        f.Receipt.Collections[0].InventoryAdmissions[0].NativeReturn = false;
        Assert.Equal(16, Assert.Single(f.Read().Slots).StackAfter);
    }

    [Theory]
    [InlineData("xml-metadata")]
    [InlineData("raw-metadata")]
    [InlineData("reference")]
    [InlineData("name")]
    [InlineData("order-data")]
    [InlineData("history")]
    [InlineData("mail-flag")]
    [InlineData("missing-payload")]
    [InlineData("raw-slot")]
    [InlineData("stack-limit")]
    [InlineData("capacity")]
    [InlineData("slot-denominator")]
    [InlineData("raw-slot-swap")]
    [InlineData("raw-empty-metadata")]
    [InlineData("after-capture-gap")]
    [InlineData("input-depth")]
    public void RejectsQuantityConsistentButUnwitnessedSlotOrMetadataChanges(string change)
    {
        var f = new PickupFixture();
        var slot = f.Receipt.InventoryPayloadAfter!.Slots![0];
        var input = f.Receipt.Collections[0].InventoryAdmissions[0].Item!;
        switch (change)
        {
            case "input-depth":
                var deep = XElement.Parse(input.SerializedStateXml);
                deep.Add(XElement.Parse(string.Concat(Enumerable.Repeat("<n>", 65)) + string.Concat(Enumerable.Repeat("</n>", 65))));
                SetXml(input, deep); break;
            case "xml-metadata":
                var xml = XElement.Parse(slot.SerializedStateXml!); xml.Add(new XElement("foreign", "changed")); SetXml(slot, xml); break;
            case "reference": slot.ItemReferenceId = Guid.NewGuid().ToString("N"); break;
            case "name":
            case "order-data":
            case "mail-flag":
                var incoming = XElement.Parse(input.SerializedStateXml);
                if (change == "mail-flag") incoming.Add(new XElement("setFlagOnPickup", "foreign-mail"));
                else incoming.Element(change == "name" ? "name" : "orderData")!.Value = "different";
                SetXml(input, incoming); break;
            case "history":
                var history = XElement.Parse(slot.SerializedStateXml!); history.Element("hasBeenInInventory")!.Value = "false"; SetXml(slot, history); break;
            case "missing-payload": f.Request.ActionOriginInventoryPayload = false; break;
            case "slot-denominator": f.Receipt.InventoryPayloadAfter.Slots = [..f.Receipt.InventoryPayloadAfter.Slots, new() { SlotIndex = 3, IsEmpty = true }]; break;
        }
        f.RefreshSnapshots();
        if (change == "after-capture-gap")
        {
            f.After.GameTick++;
            _ = StardewAI.GoalConditionedBootstrap.NativeOutputObservationSnapshotBoundary.Read(
                f.Request, f.Receipt, f.Before, f.After, "Farm");
        }
        if (change is "raw-metadata" or "raw-slot" or "stack-limit" or "capacity" or "raw-slot-swap" or "raw-empty-metadata")
        {
            var player = JsonNode.Parse(f.After.State["player"].GetRawText())!;
            if (change == "capacity") player["inventory_capacity"]!["value"]!["max_items"] = 2;
            else if (change == "raw-empty-metadata") player["inventory"]!["value"]![2]!["special_state"] = "invented";
            else if (change == "raw-slot-swap")
            {
                var rows = player["inventory"]!["value"]!.AsArray();
                var first = rows[0]!.DeepClone(); rows[0] = rows[1]!.DeepClone(); rows[1] = first;
                rows[0]!["slot_index"] = 0; rows[1]!["slot_index"] = 1;
            }
            else player["inventory"]!["value"]![0]![change switch
                { "raw-metadata" => "special_state", "raw-slot" => "runtime_type", _ => "maximum_stack_size" }] =
                change == "stack-limit" ? JsonValue.Create(1) : JsonValue.Create("foreign");
            f.After.State["player"] = JsonSerializer.SerializeToElement(player);
            f.After.StateHash = StardewAI.Contracts.State.SnapshotHash.ComputeStateHash(f.After.State);
            if (change == "raw-slot-swap")
                _ = StardewAI.GoalConditionedBootstrap.NativeOutputObservationSnapshotBoundary.Read(
                    f.Request, f.Receipt, f.Before, f.After, "Farm");
        }
        Assert.Throws<InvalidDataException>(() => f.Read());
    }
}
