using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    internal sealed class PickupFixture
    {
        internal readonly TrainingExecutionRequest Request;
        internal readonly ActionOriginObservationReceipt Receipt;
        internal SnapshotEnvelope Before = null!, After = null!;
        internal PickupFixture(bool newSlot = false)
        {
            (Request, Receipt) = NativeInventoryPayloadBoundaryTests.Fixture();
            Receipt.ClearanceToolCalls = [];
            foreach (var state in new[] { Receipt.InventoryPayloadBefore!, Receipt.InventoryPayloadAfter! })
                foreach (var slot in state.Slots!.Where(row => row.IsEmpty == false))
                {
                    var xml = XElement.Parse(slot.SerializedStateXml!);
                    xml.Add(new XElement("name", slot.QualifiedItemId == "(O)388" ? "Wood" : "Axe"),
                        new XElement("hasBeenInInventory", "true"), new XElement("orderData", ""));
                    SetXml(slot, xml);
                }
            var input = Receipt.Collections[0].InventoryAdmissions[0].Item!;
            var root = XElement.Parse(Receipt.InventoryPayloadBefore!.Slots![0].SerializedStateXml!);
            root.Element("stack")!.Value = "6";
            root.Element("hasBeenInInventory")!.Value = "false";
            SetXml(input, root);
            if (newSlot)
            {
                Receipt.InventoryPayloadBefore.Slots[0] = new() { SlotIndex = 0, IsEmpty = true };
                Receipt.InventoryPayloadAfter!.Slots![0].Stack = 6;
                SetSlotStack(Receipt.InventoryPayloadAfter.Slots[0], 6);
                Receipt.InventoryBefore[0].Quantity = 0;
                Receipt.InventoryAfter[0].Quantity = 6;
                var admission = Receipt.Collections[0].InventoryAdmissions[0];
                admission.InventoryBefore = 0; admission.InventoryAfter = 6;
            }
            RefreshSnapshots();
        }
        internal void RefreshSnapshots()
        {
            Before = Snapshot(Receipt.InventoryPayloadBefore!);
            After = Snapshot(Receipt.InventoryPayloadAfter!);
            Request.BeforeStateHash = Receipt.BeforeStateHash = Before.StateHash;
        }
        internal NativePickupInventorySummary Read() => NativeOutputObservationBoundary.ReadPickupInventory(Request, Receipt, Before, After, "Farm");
    }

    private static SnapshotEnvelope Snapshot(ActionOriginInventoryPayload payload)
    {
        var state = new Dictionary<string, JsonElement>
        {
            ["time"] = JsonSerializer.SerializeToElement(new { total_days = new { status = "available", value = 1 } }),
            ["player"] = JsonSerializer.SerializeToElement(new
            {
                location_id = new { status = "available", value = "Farm" },
                inventory_capacity = new { status = "available", value = new { max_items = 3 } },
                inventory = new { status = "available", value = payload.Slots!.Select(slot => new
                {
                    slot_index = slot.SlotIndex, is_empty = slot.IsEmpty, runtime_type = slot.RuntimeType,
                    qualified_item_id = slot.QualifiedItemId, quality = slot.Quality, stack = slot.Stack,
                    maximum_stack_size = slot.IsEmpty == true ? (int?)null : slot.IsTool == true ? 1 : 999,
                    special_state = slot.IsEmpty == true ? null : "retained"
                }).ToArray() }
            })
        };
        return new SnapshotEnvelope { BridgeVersion = "fixture", GameVersion = "1.6.15", GameTick = payload.GameTick ?? throw new InvalidDataException("Fixture tick missing."),
            SaveId = new() { Status = "available", Value = "save" }, PlayerId = new() { Status = "available", Value = "123" },
            State = state, StateHash = SnapshotHash.ComputeStateHash(state) };
    }

    private static void SetSlotStack(ActionOriginInventoryPayloadSlot slot, int count)
    {
        slot.Stack = count;
        var xml = XElement.Parse(slot.SerializedStateXml!); xml.Element("stack")!.Value = count.ToString(); SetXml(slot, xml);
    }
    private static void SetXml(ActionOriginInventoryPayloadSlot slot, XElement xml)
    {
        slot.SerializedStateXml = xml.ToString(SaveOptions.DisableFormatting);
        slot.SerializedStateSha256 = Hash(slot.SerializedStateXml);
    }
    private static void SetXml(ActionOriginObservedItem item, XElement xml)
    {
        item.SerializedStateXml = xml.ToString(SaveOptions.DisableFormatting);
        item.SerializedStateSha256 = Hash(item.SerializedStateXml);
    }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
