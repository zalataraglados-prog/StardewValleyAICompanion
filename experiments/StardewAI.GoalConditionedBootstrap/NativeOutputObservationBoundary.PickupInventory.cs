using System.Xml.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record NativeObservedPickupSlot(int SlotIndex, string ItemReferenceId, string QualifiedItemId,
    int Quality, int StackBefore, int StackAfter);

public sealed record NativePickupInventorySummary(NativeOutputResourceEvidence Evidence,
    NativeInventoryPayloadSummary Payload, IReadOnlyList<NativeObservedPickupSlot> Slots, long FrameBeforeGameTick, long FrameAfterGameTick)
{
    public bool CaptureSlotEffectsVerified => true;
    public long BeforeCaptureGapTicks => Payload.Before.GameTick - FrameBeforeGameTick;
    public bool PersistentResourceBoundaryVerified => false;
    public bool ProducerProvenanceVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

public static partial class NativeOutputObservationBoundary
{
    // Slot effects consume the original admission and persisted-payload witnesses, not a new capture.
    public static NativePickupInventorySummary ReadPickupInventory(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, SnapshotEnvelope before, SnapshotEnvelope after, string locationId) =>
        ReadPickupInventory(request, receipt, before, after, locationId, ReadResourceEvidence(request, receipt, before, after, locationId));

    internal static NativePickupInventorySummary ReadPickupInventory(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, SnapshotEnvelope before, SnapshotEnvelope after, string locationId,
        NativeOutputResourceEvidence evidence)
    {
        var payload = ReadInventoryPayload(request, receipt, before.GameTick, after.GameTick, locationId,
            before.PlayerId.Value ?? throw new InvalidDataException("Pickup inventory actor is unavailable."));
        Require(payload.After.GameTick == after.GameTick,
            "pickup persisted endpoint is not anchored to the actual after-snapshot tick");
        Require(payload.Before.Slots.Count == payload.After.Slots.Count,
            "pickup changed the original inventory slot denominator");
        var firstRows = VerifyPickupSnapshotSlots(before, payload.Before);
        var nextRows = VerifyPickupSnapshotSlots(after, payload.After);
        var capacity = NativeExecutionSourceSupport.Value(before, "player", "inventory_capacity").GetProperty("max_items").GetInt32();
        Require(capacity is > 0 and <= 512 && capacity <= payload.Before.Slots.Count &&
                NativeExecutionSourceSupport.Value(after, "player", "inventory_capacity").GetProperty("max_items").GetInt32() == capacity,
            "pickup changed or lacks the original active slot capacity");
        var characters = 0;
        var admissions = receipt.Collections.SelectMany(row => row.InventoryAdmissions).Where(row => row.InventoryAfter > row.InventoryBefore)
            .Select(row =>
            {
                ValidateInventoryXmlDepth(row.Item!.SerializedStateXml);
                return new PendingPickupAdmission(row, ReadItemXml(row.Item.SerializedStateXml,
                    row.Item.SerializedStateSha256, ref characters));
            }).ToArray();
        var originalReferences = payload.Before.Slots.Where(slot => !slot.IsEmpty)
            .Select(slot => Guid.ParseExact(slot.ItemReferenceId!, "N")).ToHashSet();
        var states = new PickupInventorySlotState?[payload.Before.Slots.Count];
        for (var index = 0; index < states.Length; index++)
        {
            var slot = payload.Before.Slots[index];
            if (!slot.IsEmpty)
                states[index] = new(slot, ReadItemXml(slot.Xml, slot.Sha256, ref characters),
                    slot.Stack!.Value, firstRows[index].GetProperty("maximum_stack_size").GetInt32());
        }
        // Replay the native admission order: existing compatible stacks, then the first
        // empty active slot. The founding admission, never an endpoint match, owns its XML.
        foreach (var admission in admissions)
        {
            foreach (var state in states.Take(capacity))
            {
                if (admission.Remaining == 0) break;
                if (state is null || state.Maximum <= 1 || !PickupStackMatches(admission, state.Header, state.Xml)) continue;
                var quantity = Math.Min(admission.Remaining, state.Maximum - state.Count);
                if (quantity <= 0) continue;
                ApplyPickupReceiveFlags(state.Xml, admission.Xml);
                state.Count += quantity;
                admission.Remaining -= quantity;
            }
            if (admission.Remaining == 0) continue;
            var index = Array.FindIndex(states, 0, capacity, state => state is null);
            Require(index >= 0, "pickup admission exceeds its actual compatible stacks and active empty slots");
            var next = payload.After.Slots[index];
            Require(!next.IsEmpty && next.RuntimeType == admission.Item.RuntimeType &&
                    next.QualifiedItemId == admission.Item.QualifiedItemId && next.Quality == admission.Item.Quality &&
                    !originalReferences.Contains(Guid.ParseExact(next.ItemReferenceId!, "N")),
                "pickup first empty slot differs from its founding admission or reuses an existing reference");
            var maximum = nextRows[index].GetProperty("maximum_stack_size").GetInt32();
            Require(admission.Remaining <= maximum, "pickup founding stack exceeds the observed native maximum");
            var founded = new PickupInventorySlotState(next, new XElement(admission.Xml), admission.Remaining, maximum);
            ApplyPickupReceiveFlags(founded.Xml, admission.Xml);
            states[index] = founded;
            admission.Remaining = 0;
        }
        var changes = new List<NativeObservedPickupSlot>();
        for (var index = 0; index < states.Length; index++)
        {
            var first = payload.Before.Slots[index];
            var next = payload.After.Slots[index];
            var state = states[index];
            if (state is null)
            {
                Require(next.IsEmpty, "pickup created a slot without an allocated native admission");
                VerifyPickupRawEmpty(firstRows[index], nextRows[index]);
                continue;
            }
            Require(!next.IsEmpty && Guid.ParseExact(state.Header.ItemReferenceId!, "N") == Guid.ParseExact(next.ItemReferenceId!, "N") &&
                    state.Header.RuntimeType == next.RuntimeType && state.Header.QualifiedItemId == next.QualifiedItemId &&
                    state.Header.Quality == next.Quality && state.Count == next.Stack,
                "pickup changed the allocated physical slot, identity or exact native stack");
            SetPickupXmlStack(state.Xml, state.Count);
            var actual = ReadItemXml(next.Xml, next.Sha256, ref characters);
            Require(XNode.DeepEquals(state.Xml, actual), "pickup changed unexplained persisted inventory metadata");
            if (!first.IsEmpty) VerifyPickupRawRow(firstRows[index], nextRows[index], state.Count);
            var initial = first.Stack ?? 0;
            if (state.Count != initial)
                changes.Add(new(index, next.ItemReferenceId!, next.QualifiedItemId!, next.Quality!.Value, initial, state.Count));
        }
        return new(evidence, payload, changes.AsReadOnly(), before.GameTick, after.GameTick);
    }

    private sealed class PendingPickupAdmission
    {
        internal PendingPickupAdmission(ActionOriginInventoryAdmission admission, XElement xml)
        {
            Item = admission.Item!; Xml = xml;
            Remaining = admission.InventoryAfter - admission.InventoryBefore;
        }
        internal ActionOriginObservedItem Item { get; }
        internal XElement Xml { get; }
        internal int Remaining { get; set; }
    }

    private sealed class PickupInventorySlotState
    {
        internal PickupInventorySlotState(NativeObservedInventoryPayloadSlot header, XElement xml, int count, int maximum)
        { Header = header; Xml = xml; Count = count; Maximum = maximum; }
        internal NativeObservedInventoryPayloadSlot Header { get; }
        internal XElement Xml { get; }
        internal int Count { get; set; }
        internal int Maximum { get; }
    }
}
