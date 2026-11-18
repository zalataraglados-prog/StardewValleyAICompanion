using System.Globalization;
using System.Xml.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private sealed record WateringToolEndpointSlot(
        int SlotIndex, string ItemReferenceId, string RuntimeType, string QualifiedItemId,
        int InitialParentTileIndex, int CurrentParentTileIndex, int SwingTicker,
        bool HasBeenInInventory, int? WaterLeft);

    private sealed record WateringToolObservedCall(
        string Kind, int FacingDirection, int? Direction, int? FarmerMotionFrame, bool? InventoryHistoryValue);

    private sealed record WateringToolMetadataContext(
        int PrimarySlotIndex, string PrimaryItemReferenceId,
        IReadOnlyDictionary<int, WateringToolEndpointSlot> AfterBySlot,
        IReadOnlyDictionary<string, WateringToolEndpointSlot> AfterByRef,
        IReadOnlyDictionary<string, IReadOnlyList<WateringToolObservedCall>> CallsByRef);

    private static WateringToolMetadataContext ReadWateringToolMetadataContext(
        ActionOriginObservationReceipt receipt, NativeInventoryGraphSummary graph, int toolSlot, string toolId)
    {
        if (receipt is null || graph?.Payload is null || graph.Transients is null)
            throw new InvalidDataException("Watering metadata lacks its original graph/receipt.");
        Require(!string.IsNullOrWhiteSpace(toolId) && !string.IsNullOrWhiteSpace(receipt.LocationId) &&
                receipt.WateringToolMetadataBefore is not null && receipt.WateringToolMetadataAfter is not null &&
                receipt.WateringToolMetadataCalls is not null,
            "watering tool metadata witness or inventory graph is missing");
        var beforeFrame = receipt!.WateringToolMetadataBefore!;
        var afterFrame = receipt.WateringToolMetadataAfter!;
        var rawCalls = receipt.WateringToolMetadataCalls!;
        var actorId = graph.Transients.Before.Selection.ActorReferenceId;
        Require(beforeFrame.SchemaVersion == NativeWateringObservationContract.MetadataSchemaVersion &&
                beforeFrame.SchemaVersion == afterFrame.SchemaVersion &&
                beforeFrame.GameTick == graph.Payload.Before.GameTick &&
                beforeFrame.GameTick == receipt.BeforeGameTick &&
                afterFrame.GameTick == graph.Payload.After.GameTick &&
                afterFrame.GameTick == receipt.AfterGameTick &&
                receipt.BeforeGameTick <= receipt.AfterGameTick &&
                !string.IsNullOrWhiteSpace(actorId) &&
                actorId == graph.Transients.After.Selection.ActorReferenceId &&
                actorId == graph.Before.ActorReferenceId &&
                actorId == graph.After.ActorReferenceId &&
                beforeFrame.ActorReferenceId == actorId &&
                afterFrame.ActorReferenceId == actorId,
            "watering tool metadata frame ticks, schema or actor reference mismatch");

        var payloadCharacters = 0;
        ReadWateringToolMetadataFrame(beforeFrame, graph.Payload.Before, ref payloadCharacters, out var beforeBySlot, out var beforeByRef);
        ReadWateringToolMetadataFrame(afterFrame, graph.Payload.After, ref payloadCharacters, out var afterBySlot, out var afterByRef);

        foreach (var beforeTool in beforeBySlot.Values)
        {
            Require(afterByRef.TryGetValue(beforeTool.ItemReferenceId, out var afterTool) &&
                    afterTool.SlotIndex == beforeTool.SlotIndex &&
                    afterTool.RuntimeType == beforeTool.RuntimeType &&
                    afterTool.QualifiedItemId == beforeTool.QualifiedItemId &&
                    afterTool.InitialParentTileIndex == beforeTool.InitialParentTileIndex,
                "existing inventory tool changed slot, identity or initial sprite index");
        }
        foreach (var afterTool in afterBySlot.Values)
        {
            if (!beforeByRef.ContainsKey(afterTool.ItemReferenceId))
                Require(afterTool.SlotIndex >= 0 && afterTool.SlotIndex < graph.Payload.Before.Slots.Count &&
                        graph.Payload.Before.Slots[afterTool.SlotIndex].IsEmpty,
                    "new after-frame tool occupies a previously non-empty slot");
        }
        Require(beforeBySlot.TryGetValue(toolSlot, out var primaryBefore) &&
                graph.Payload.Before.Slots.FirstOrDefault(slot => slot.RuntimeType == WateringCanRuntimeType)?.SlotIndex == toolSlot &&
                primaryBefore.RuntimeType == WateringCanRuntimeType &&
                primaryBefore.QualifiedItemId == toolId &&
                afterBySlot.TryGetValue(toolSlot, out var primaryAfter) &&
                primaryAfter.ItemReferenceId == primaryBefore.ItemReferenceId,
            "selected watering can slot, reference or qualified id mismatch");

        var callsByRef = ReadWateringToolMetadataCalls(rawCalls, receipt, actorId, beforeByRef, afterByRef);
        return new(toolSlot, primaryBefore!.ItemReferenceId, afterBySlot, afterByRef, callsByRef);
    }

    private static void ReadWateringToolMetadataFrame(
        NativeWateringToolMetadataFrame frame, NativeObservedInventoryPayloadState payload, ref int payloadCharacters,
        out IReadOnlyDictionary<int, WateringToolEndpointSlot> bySlot,
        out IReadOnlyDictionary<string, WateringToolEndpointSlot> byRef)
    {
        if (frame.Tools is not { Length: <= 512 } || payload.Slots is null)
            throw new InvalidDataException("Watering tool metadata frame slots are missing or unbounded.");
        var tools = frame.Tools!;
        var slotMap = new Dictionary<int, WateringToolEndpointSlot>();
        var refMap = new Dictionary<string, WateringToolEndpointSlot>(StringComparer.Ordinal);
        var rowIndex = 0;
        for (var i = 0; i < payload.Slots.Count; i++)
        {
            var slot = payload.Slots[i];
            Require(slot is not null && slot.SlotIndex == i, "inventory payload slot order is invalid");
            if (slot!.IsEmpty)
            {
                Require(slot.IsTool != true, "empty payload slot cannot be marked as tool");
                continue;
            }
            Require(slot.IsTool.HasValue, "occupied payload slot is missing IsTool classification");
            if (!slot.IsTool!.Value) continue;
            Require(rowIndex < tools.Length, "watering tool metadata frame is missing an occupied tool row");
            var row = tools[rowIndex++];
            Require(row is not null && row.SlotIndex == slot.SlotIndex &&
                    !string.IsNullOrWhiteSpace(slot.ItemReferenceId) && row.ItemReferenceId == slot.ItemReferenceId &&
                    !string.IsNullOrWhiteSpace(slot.RuntimeType) && row.RuntimeType == slot.RuntimeType &&
                    !string.IsNullOrWhiteSpace(slot.QualifiedItemId) && row.QualifiedItemId == slot.QualifiedItemId &&
                    row.InitialParentTileIndex.HasValue && row.CurrentParentTileIndex.HasValue &&
                    row.SwingTicker.HasValue && row.HasBeenInInventory.HasValue,
                "watering tool metadata row identity or scalar fields are incomplete");
            var xml = ReadItemXml(slot.Xml, slot.Sha256, ref payloadCharacters);
            var initLower = ParseToolXmlInt(xml, "initialParentTileIndex");
            var initUpper = ParseToolXmlInt(xml, "InitialParentTileIndex");
            var current = ParseToolXmlInt(xml, "currentParentTileIndex");
            var swing = ParseToolXmlInt(xml, "swingTicker");
            var history = ParseToolXmlBool(xml, "hasBeenInInventory");
            Require(initLower == row!.InitialParentTileIndex!.Value && initUpper == row.InitialParentTileIndex.Value &&
                    current == row.CurrentParentTileIndex!.Value && swing == row.SwingTicker!.Value &&
                    history == row.HasBeenInInventory!.Value,
                "watering tool metadata row disagrees with payload XML scalars");
            if (slot.RuntimeType == WateringCanRuntimeType)
                Require(row.WaterLeft.HasValue && ParseToolXmlInt(xml, "WaterLeft") == row.WaterLeft.Value,
                    "watering can metadata row WaterLeft disagrees with payload XML");
            else
                Require(!row.WaterLeft.HasValue && !xml.Elements("WaterLeft").Any(),
                    "non-watering-can tool cannot declare WaterLeft metadata");
            var entry = new WateringToolEndpointSlot(slot.SlotIndex, slot.ItemReferenceId!, slot.RuntimeType!,
                slot.QualifiedItemId!, initLower, current, swing, history, row.WaterLeft);
            Require(slotMap.TryAdd(entry.SlotIndex, entry) && refMap.TryAdd(entry.ItemReferenceId, entry),
                "duplicate watering tool slot or reference in frame");
        }
        Require(rowIndex == tools.Length, "watering tool metadata frame contains extra tool rows");
        bySlot = slotMap;
        byRef = refMap;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<WateringToolObservedCall>> ReadWateringToolMetadataCalls(
        NativeWateringToolMetadataCall[] rawCalls, ActionOriginObservationReceipt receipt, string actorId,
        IReadOnlyDictionary<string, WateringToolEndpointSlot> beforeByRef,
        IReadOnlyDictionary<string, WateringToolEndpointSlot> afterByRef)
    {
        Require(rawCalls.Length <= MaxWateringToolMetadataCalls, "watering tool metadata calls exceed maximum bound");
        var map = new Dictionary<string, List<WateringToolObservedCall>>(StringComparer.Ordinal);
        var lastTick = receipt.BeforeGameTick;
        for (var i = 0; i < rawCalls.Length; i++)
        {
            var call = rawCalls[i];
            Require(call is not null && call.Ordinal == i + 1 && call.Completed == true &&
                    call.GameTick.HasValue && call.GameTick.Value >= lastTick && call.GameTick.Value <= receipt.AfterGameTick &&
                    call.ActorReferenceId == actorId && call.LocationId == receipt.LocationId &&
                    !string.IsNullOrWhiteSpace(call.ItemReferenceId) &&
                    (afterByRef.TryGetValue(call.ItemReferenceId, out var bound) || beforeByRef.TryGetValue(call.ItemReferenceId, out bound)) &&
                    call.RuntimeType == bound!.RuntimeType && call.QualifiedItemId == bound.QualifiedItemId &&
                    call.FacingDirection.HasValue && call.ToolPower.HasValue && !string.IsNullOrWhiteSpace(call.Kind),
                "watering tool metadata call envelope or endpoint binding is invalid");
            lastTick = call!.GameTick!.Value;
            Require(call.Kind switch
            {
                "update" => call.Direction.HasValue && call.FarmerMotionFrame.HasValue && !call.InventoryHistoryValue.HasValue,
                "end_using" => !call.Direction.HasValue && !call.FarmerMotionFrame.HasValue && !call.InventoryHistoryValue.HasValue,
                "inventory_history" => call.InventoryHistoryValue.HasValue && !call.Direction.HasValue && !call.FarmerMotionFrame.HasValue,
                _ => false
            }, "watering tool metadata call kind or kind-specific parameters are invalid");
            if (!map.TryGetValue(call.ItemReferenceId!, out var list))
                map[call.ItemReferenceId!] = list = new List<WateringToolObservedCall>();
            list.Add(new(call.Kind!, call.FacingDirection!.Value, call.Direction, call.FarmerMotionFrame, call.InventoryHistoryValue));
        }
        return map.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<WateringToolObservedCall>)pair.Value.AsReadOnly(), StringComparer.Ordinal);
    }
}
