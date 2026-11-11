using System.Text.Json;
using System.Xml.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    [Fact]
    public void PickupFillsCompatibleLaterResidentBeforeAnEarlierEmptySlot()
    {
        var f = new PickupFixture();
        foreach (var state in new[] { f.Receipt.InventoryPayloadBefore!, f.Receipt.InventoryPayloadAfter! })
        {
            var moved = JsonSerializer.Deserialize<ActionOriginInventoryPayloadSlot>(JsonSerializer.Serialize(state.Slots![0]))!;
            moved.SlotIndex = 2; state.Slots[2] = moved;
            state.Slots[0] = new() { SlotIndex = 0, IsEmpty = true };
        }
        f.RefreshSnapshots();
        Assert.Equal(2, Assert.Single(f.Read().Slots).SlotIndex);
    }

    [Fact]
    public void PickupKeepsIncompatibleOrderDataInDistinctUnderfilledNewStacks()
    {
        var f = new PickupFixture(newSlot: true);
        SetOrderedAdmissions(f, 3, 3);
        foreach (var admission in f.Receipt.Collections[0].InventoryAdmissions)
        {
            var xml = XElement.Parse(admission.Item!.SerializedStateXml);
            xml.Element("orderData")!.Value = xml.Descendants("value").Single().Element("string")!.Value;
            SetXml(admission.Item, xml);
        }
        var first = f.Receipt.InventoryPayloadAfter!.Slots![0]; SetSlotStack(first, 3);
        var root = XElement.Parse(first.SerializedStateXml!); root.Element("orderData")!.Value = "A"; SetXml(first, root);
        var second = JsonSerializer.Deserialize<ActionOriginInventoryPayloadSlot>(JsonSerializer.Serialize(first))!;
        second.SlotIndex = 2; second.ItemReferenceId = Guid.NewGuid().ToString("N");
        var next = XElement.Parse(second.SerializedStateXml!); next.Element("orderData")!.Value = "B";
        next.Descendants("value").Single().Element("string")!.Value = "B"; SetXml(second, next);
        f.Receipt.InventoryPayloadAfter.Slots[2] = second;
        f.RefreshSnapshots();
        Assert.Equal(new[] { 3, 3 }, f.Read().Slots.Select(slot => slot.StackAfter));
    }

    [Fact]
    public void RejectsInventedSplitIntoTwoUnderfilledCompatibleNewSlots()
    {
        var f = new PickupFixture(newSlot: true);
        var after = f.Receipt.InventoryPayloadAfter!;
        var extra = JsonSerializer.Deserialize<ActionOriginInventoryPayloadSlot>(JsonSerializer.Serialize(after.Slots![0]))!;
        extra.SlotIndex = 2; extra.ItemReferenceId = Guid.NewGuid().ToString("N");
        SetSlotStack(extra, 3); SetSlotStack(after.Slots[0], 3); after.Slots[2] = extra;
        f.RefreshSnapshots();
        Assert.Throws<InvalidDataException>(() => f.Read());
    }

    [Theory]
    [InlineData("A", true)]
    [InlineData("B", false)]
    public void PickupNewSlotMustRetainTheFirstActuallyAllocatedAdmissionMetadata(string residentMetadata, bool allowed)
    {
        var f = new PickupFixture(newSlot: true);
        SetOrderedAdmissions(f, 3, 3);
        var slot = f.Receipt.InventoryPayloadAfter!.Slots![0];
        var xml = XElement.Parse(slot.SerializedStateXml!);
        xml.Descendants("value").Single().Element("string")!.Value = residentMetadata;
        SetXml(slot, xml);
        f.RefreshSnapshots();
        if (allowed) Assert.Equal(6, Assert.Single(f.Read().Slots).StackAfter);
        else Assert.Throws<InvalidDataException>(() => f.Read());
    }

    private static void SetOrderedAdmissions(PickupFixture fixture, int firstCount, int secondCount)
    {
        var original = fixture.Receipt.Collections[0].InventoryAdmissions[0];
        var rows = new List<ActionOriginInventoryAdmission>();
        var before = 0;
        foreach (var (count, marker) in new[] { (firstCount, "A"), (secondCount, "B") })
        {
            var row = JsonSerializer.Deserialize<ActionOriginInventoryAdmission>(JsonSerializer.Serialize(original))!;
            var xml = XElement.Parse(row.Item!.SerializedStateXml);
            xml.Element("stack")!.Value = count.ToString();
            xml.Descendants("value").Single().Element("string")!.Value = marker;
            SetXml(row.Item, xml);
            row.OfferedQuantity = count; row.InventoryBefore = before; row.InventoryAfter = before + count;
            rows.Add(row); before += count;
        }
        fixture.Receipt.Collections[0].InventoryAdmissions = rows.ToArray();
        fixture.Receipt.InventoryAfter[0].Quantity = before;
        var after = fixture.Receipt.InventoryPayloadAfter!.Slots![0];
        SetSlotStack(after, before);
        var root = XElement.Parse(after.SerializedStateXml!);
        root.Descendants("value").Single().Element("string")!.Value = "A";
        SetXml(after, root);
    }
}
