using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    private sealed record WateringWireFixture(PickupFixture Stock, TrainingExecutionResult Result)
    {
        internal ActionOriginObservationReceipt Receipt => Result.ActionOriginObservation!;
        internal NativeWateringResourceSummary Read() => NativeOutputObservationBoundary.ReadWateringResources(
            Stock.Request, Result, Stock.Before, Stock.After, "Farm", new(71, 16));
        internal string Raw() => JsonSerializer.Serialize(new { Stock.Request, Result, Stock.Before, Stock.After });
    }

    private static WateringWireFixture WateringMetadataFixture(bool newSlot = false, int water = 20, int cycles = 1,
        bool applied = true, bool refillOnly = false, bool bottomless = false, bool enchanted = false, int counter = 7)
    {
        var stock = WateringResourceFixture(newSlot: newSlot, water: water, bottomless: bottomless, enchanted: enchanted,
            applied: applied, refillOnly: refillOnly).Raw;
        var receipt = NativeDebrisStateBoundaryTests.Fixture().Receipt;
        NativeWateringObservationContractTests.Enable(stock.Request);
        var calls = new List<NativeWateringToolMetadataCall>();
        foreach (var (source, target, snapshot, isAfter) in new[]
        {
            (stock.Receipt.InventoryPayloadBefore!, receipt.InventoryPayloadBefore!, stock.Before, false),
            (stock.Receipt.InventoryPayloadAfter!, receipt.InventoryPayloadAfter!, stock.After, true)
        })
        {
            var removed = new HashSet<string>();
            for (var i = 0; i < source.Slots!.Length; i++)
            {
                var original = source.Slots[i];
                var slot = target.Slots![i];
                if (original.IsEmpty == true)
                {
                    if (slot.ItemReferenceId is not null) removed.Add(slot.ItemReferenceId);
                    target.Slots[i] = new() { SlotIndex = i, IsEmpty = true };
                    continue;
                }
                slot.RuntimeType = original.RuntimeType; slot.QualifiedItemId = original.QualifiedItemId;
                slot.Quality = original.Quality; slot.Stack = original.Stack;
                var xml = XElement.Parse(original.SerializedStateXml!);
                if (i == 1)
                {
                    slot.RuntimeType = "StardewValley.Tools.WateringCan"; slot.QualifiedItemId = "(T)WateringCan";
                    xml.Name = "WateringCan";
                    xml.Element("itemId")!.Value = "WateringCan"; xml.Element("name")!.Value = "WateringCan";
                    xml.Element("hasBeenInInventory")!.Value = isAfter && cycles > 0 ? "true" : "false";
                    if (xml.Element("attachments") is null) xml.Add(new XElement("attachments"));
                    var left = snapshot.State["player"].GetProperty("watering_can_state").GetProperty("value").GetProperty("water_left").GetInt32();
                    xml.Add(new XElement("initialParentTileIndex", 273), new XElement("InitialParentTileIndex", 273),
                        new XElement("currentParentTileIndex", isAfter && cycles > 0 ? 275 : 273),
                        new XElement("swingTicker", isAfter ? unchecked(counter + cycles) : counter), new XElement("WaterLeft", left));
                    if (target.SelectionTransientState!.CurrentItem?.ReferenceId == slot.ItemReferenceId)
                        target.SelectionTransientState.CurrentItem!.RuntimeType = slot.RuntimeType;
                }
                SetXml(slot, xml);
            }
            var graph = target.GraphState!;
            graph.Nodes = graph.Nodes!.Where(row => !removed.Contains(row.ReferenceId!)).ToArray();
            graph.Edges = graph.Edges!.Where(row => !removed.Contains(row.ParentReferenceId!)).ToArray();
            foreach (var node in graph.Nodes)
            {
                var header = target.Slots!.Single(row => row.ItemReferenceId == node.ReferenceId);
                node.RuntimeType = header.RuntimeType; node.QualifiedItemId = header.QualifiedItemId;
                node.Stack = header.Stack; node.Quality = header.Quality;
            }
            graph.NodeOccurrences = graph.Nodes.Length;
            graph.EdgeOccurrences = graph.Edges.Length + graph.ActorEnchantments!.Length;
            var frame = new NativeWateringToolMetadataFrame
            {
                SchemaVersion = NativeWateringObservationContract.MetadataSchemaVersion, GameTick = target.GameTick,
                ActorReferenceId = target.SelectionTransientState!.ActorReferenceId,
                Tools = target.Slots!.Where(row => row.IsTool == true).Select(row => WateringMetadataRow(row)).ToArray()
            };
            if (isAfter) receipt.WateringToolMetadataAfter = frame; else receipt.WateringToolMetadataBefore = frame;
        }
        foreach (var snapshot in new[] { stock.Before, stock.After })
        {
            void Convert(JsonObject row)
            {
                row["runtime_type"] = "StardewValley.Tools.WateringCan"; row["qualified_item_id"] = "(T)WateringCan";
                row["item_id"] = "WateringCan"; row["display_name"] = "WateringCan";
                row["context_tags"] = new JsonArray("item_watering_can");
            }
            Edit(snapshot, "player", "inventory", rows => Convert(rows.AsArray().OfType<JsonObject>().Single(row => row["slot_index"]!.GetValue<int>() == 1)));
            Edit(snapshot, "farm", "material_inventory_graph", graph =>
            {
                Convert(graph["inventory_nodes"]![0]!["slots"]!.AsArray().OfType<JsonObject>().Single(row => row["slot_index"]!.GetValue<int>() == 1));
                graph["quantity_rows"]!.AsArray().OfType<JsonObject>().Single(row => row["qualified_item_id"]!.GetValue<string>() == "(T)Axe")["qualified_item_id"] = "(T)WateringCan";
            });
        }
        foreach (var inventory in new[] { stock.Receipt.InventoryBefore, stock.Receipt.InventoryAfter })
            inventory.Single(row => row.QualifiedItemId == "(T)Axe").QualifiedItemId = "(T)WateringCan";
        receipt.InventoryBefore = CloneWatering(stock.Receipt.InventoryBefore); receipt.InventoryAfter = CloneWatering(stock.Receipt.InventoryAfter);
        var incoming = receipt.Collections[0].InventoryAdmissions[0].Item!;
        var admission = receipt.Collections[0].InventoryAdmissions[0];
        var sourceAdmission = stock.Receipt.Collections[0].InventoryAdmissions[0];
        admission.InventoryBefore = sourceAdmission.InventoryBefore; admission.InventoryAfter = sourceAdmission.InventoryAfter;
        var incomingXml = XElement.Parse(stock.Receipt.Collections[0].InventoryAdmissions[0].Item!.SerializedStateXml);
        SetXml(incoming, incomingXml);
        receipt.ClearanceToolCalls = [];
        var can = receipt.InventoryPayloadBefore!.Slots![1];
        for (var i = 0; i < cycles; i++)
        {
            if (i == 0) Add("inventory_history", history: true);
            Add("update", direction: 1, frame: 0);
            Add("end_using");
        }
        receipt.WateringToolMetadataCalls = calls.ToArray();
        receipt.ScopeId = stock.Request.ActionOriginObservationScopeId!;
        receipt.RunId = stock.Request.RunId; receipt.QueueId = stock.Request.QueueId; receipt.QueueItemId = stock.Request.QueueItemId;
        receipt.RequestNonce = stock.Request.RequestNonce;
        Rebind(stock); receipt.BeforeStateHash = stock.Before.StateHash;
        var result = new TrainingExecutionResult
        {
            RunId = stock.Request.RunId, QueueId = stock.Request.QueueId, QueueItemId = stock.Request.QueueItemId,
            BeforeStateHash = stock.Before.StateHash, OptionId = stock.Request.OptionId, Status = applied ? "applied" : "blocked",
            PrimitiveKind = "water_crop", PrimitiveVerificationStatus = applied ? "verified" : "blocked", WateredCount = applied ? 1 : 0,
            ToolQualifiedItemId = "(T)WateringCan", ToolPower = 0, TargetLocation = "Farm", TargetTileX = 71, TargetTileY = 16,
            ProductAfterStateHash = stock.After.StateHash, ProductAfterGameTick = stock.After.GameTick, ActionOriginObservation = receipt
        };
        return new(stock, result);

        void Add(string kind, int? direction = null, int? frame = null, bool? history = null) => calls.Add(new()
        {
            Ordinal = calls.Count + 1, Kind = kind, Completed = true, GameTick = receipt.BeforeGameTick + calls.Count,
            ActorReferenceId = receipt.WateringToolMetadataBefore!.ActorReferenceId, LocationId = "Farm",
            ItemReferenceId = can.ItemReferenceId, RuntimeType = can.RuntimeType, QualifiedItemId = can.QualifiedItemId,
            FacingDirection = 1, ToolPower = 0, Direction = direction, FarmerMotionFrame = frame, InventoryHistoryValue = history
        });
    }

    private static NativeWateringToolMetadataSlot WateringMetadataRow(ActionOriginInventoryPayloadSlot row)
    {
        var xml = XElement.Parse(row.SerializedStateXml!);
        int Int(string name) => int.Parse(xml.Element(name)!.Value, System.Globalization.CultureInfo.InvariantCulture);
        return new() { SlotIndex = row.SlotIndex, ItemReferenceId = row.ItemReferenceId, RuntimeType = row.RuntimeType,
            QualifiedItemId = row.QualifiedItemId, InitialParentTileIndex = Int("initialParentTileIndex"),
            CurrentParentTileIndex = Int("currentParentTileIndex"), SwingTicker = Int("swingTicker"),
            HasBeenInInventory = bool.Parse(xml.Element("hasBeenInInventory")!.Value), WaterLeft = Int("WaterLeft") };
    }

    private static T CloneWatering<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
