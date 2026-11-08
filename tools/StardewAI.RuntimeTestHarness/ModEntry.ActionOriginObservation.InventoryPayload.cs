using System.Globalization;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed partial class NativeCollectionObservation
    {
        private ActionOriginInventoryPayload? inventoryPayloadBefore;

        internal void BeginInventoryPayload() => inventoryPayloadBefore = CaptureInventoryPayload();

        private ActionOriginInventoryPayload? CaptureInventoryPayload()
        {
            try
            {
                if (!active || !ReferenceEquals(current, this) || Environment.CurrentManagedThreadId != threadId ||
                    !ReferenceEquals(Game1.player, farmer) || !ReferenceEquals(farmer.currentLocation, capture.Location) ||
                    farmer.Items.Count > MaximumDebrisRows)
                    throw new InvalidOperationException("Inventory payload lacks the original bounded native scope.");
                var tick = (long)Game1.ticks;
                var slotCount = farmer.Items.Count;
                var graph = request.ActionOriginInventoryGraph ? CaptureInventoryGraphState() : null;
                var graphItems = graph?.Nodes.Where(node => node.Item is not null)
                    .ToDictionary(node => node.Reference, node => node.Item!, ReferenceEqualityComparer.Instance);
                var slots = new List<ActionOriginInventoryPayloadSlot>();
                for (var index = 0; index < slotCount; index++)
                {
                    if (farmer.Items.Count != slotCount) throw new InvalidOperationException("Inventory capacity changed during capture.");
                    var item = farmer.Items[index];
                    if (item is null)
                    {
                        slots.Add(new() { SlotIndex = index, IsEmpty = true });
                        continue;
                    }
                    if (item.GetType().Assembly != typeof(Item).Assembly)
                        throw new InvalidOperationException("Custom item headers are not admitted for live payload capture.");
                    if (remainingPayloadCharacters <= 0) throw new InvalidDataException("Shared native payload budget exhausted.");
                    NativeLiveItemTransientState? transient = null;
                    if (request.ActionOriginInventoryTransients)
                    {
                        if (item.itemId.Value is null)
                            throw new InvalidDataException("Native ItemId would require a mutating legacy migration before observation.");
                        if (graph is null)
                        {
                            transient = NativeLiveItemTransientCapture.Capture(item, Math.Min(65536, remainingPayloadCharacters));
                            remainingPayloadCharacters -= transient.Characters;
                        }
                        else transient = graphItems!.TryGetValue(item, out var observed) ? observed :
                            throw new InvalidDataException("Graph root raw state is unavailable.");
                    }
                    var row = new ActionOriginInventoryPayloadSlot
                    {
                        SlotIndex = index, IsEmpty = false, ItemReferenceId = ReferenceIds.For(item),
                        RuntimeType = item.GetType().FullName, QualifiedItemId = item.QualifiedItemId,
                        IsTool = item is Tool, Quality = item.Quality, Stack = item.Stack
                    };
                    var payload = graph is null ? NativeLiveItemPayloadCapture.Capture(item, Math.Min(65536, remainingPayloadCharacters)) :
                        NativeLiveItemPayloadCapture.CaptureGraphRoot(item, graph, Math.Min(65536, remainingPayloadCharacters));
                    row.SerializedStateXml = payload.Xml; row.SerializedStateSha256 = payload.Sha256;
                    remainingPayloadCharacters -= payload.Xml.Length;
                    if (transient is not null) row.TransientState = ReadItemTransientPayload(transient);
                    if (!ReferenceEquals(farmer.Items[index], item) || item.Stack != row.Stack || item.Quality != row.Quality ||
                        item.QualifiedItemId != row.QualifiedItemId)
                        throw new InvalidOperationException("Live item header changed during inventory payload capture.");
                    slots.Add(row);
                }
                var selected = farmer.CurrentToolIndex;
                var selection = request.ActionOriginInventoryTransients ? NativeLiveItemTransientCapture.CaptureSelection(farmer) : null;
                if (selected < -1 || selected >= slots.Count || farmer.Items.Count != slotCount || Game1.ticks != tick ||
                    selection is not null && farmer.CurrentToolIndex != selected)
                    throw new InvalidOperationException("Inventory selection/tick changed during payload capture.");
                return new()
                {
                    SchemaVersion = "native_inventory_payload.v1", PayloadKind = "persisted_xml",
                    ActorId = farmer.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture),
                    LocationId = capture.Location.NameOrUniqueName, GameTick = tick, SelectionObserved = true,
                    SelectedSlotIndex = selected, SelectedItemReferenceId = selected < 0 ? null : slots[selected].ItemReferenceId,
                    CurrentToolReferenceId = (selection is null ? farmer.CurrentTool : selection.CurrentTool) is { } tool ? ReferenceIds.For(tool) : null,
                    SelectionTransientState = selection is null ? null : ReadSelectionTransientPayload(selection),
                    Slots = slots.ToArray(), GraphState = graph is null ? null : ReadInventoryGraphPayload(graph, slots)
                };
            }
            catch (Exception error)
            {
                remainingPayloadCharacters = 0;
                Reject("inventory_payload_capture_failed:" + error.GetType().Name);
                return null;
            }
        }
    }
}
