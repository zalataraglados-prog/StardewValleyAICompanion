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
        ActionOriginObservationReceipt receipt, SnapshotEnvelope before, SnapshotEnvelope after, string locationId)
    {
        var evidence = ReadResourceEvidence(request, receipt, before, after, locationId);
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
        var changes = new List<NativeObservedPickupSlot>();
        var originalReferences = payload.Before.Slots.Where(slot => !slot.IsEmpty)
            .Select(slot => Guid.ParseExact(slot.ItemReferenceId!, "N")).ToHashSet();
        for (var index = 0; index < payload.Before.Slots.Count; index++)
        {
            var first = payload.Before.Slots[index];
            var next = payload.After.Slots[index];
            if (first.IsEmpty && next.IsEmpty)
            {
                VerifyPickupRawEmpty(firstRows[index], nextRows[index]);
                continue;
            }
            Require(!next.IsEmpty, "pickup removed an existing inventory item");
            var nextXml = ReadItemXml(next.Xml, next.Sha256, ref characters);
            XElement expected;
            var initial = first.Stack ?? 0;
            if (first.IsEmpty)
            {
                Require(!originalReferences.Contains(Guid.ParseExact(next.ItemReferenceId!, "N")),
                    "pickup moved an existing physical item into another slot");
                expected = NewPickupSlot(next, nextXml, admissions);
            }
            else
            {
                Require(Guid.ParseExact(first.ItemReferenceId!, "N") == Guid.ParseExact(next.ItemReferenceId!, "N") &&
                        first.RuntimeType == next.RuntimeType && first.QualifiedItemId == next.QualifiedItemId && first.Quality == next.Quality &&
                        next.Stack >= first.Stack, "pickup replaced, relocated or consumed an existing inventory item");
                expected = ReadItemXml(first.Xml, first.Sha256, ref characters);
            }
            var remaining = next.Stack!.Value - initial;
            Require(remaining == 0 || index < capacity, "pickup added stock outside the actor's active inventory capacity");
            Require(first.IsEmpty || remaining == 0 || firstRows[index].GetProperty("maximum_stack_size").GetInt32() > 1,
                "pickup merged a non-stackable inventory item");
            foreach (var admission in admissions.Where(row => row.Remaining > 0))
            {
                if (remaining == 0) break;
                if (!PickupStackMatches(admission, next, expected)) continue;
                var quantity = Math.Min(remaining, admission.Remaining);
                ApplyPickupReceiveFlags(expected, admission.Xml);
                admission.Remaining -= quantity;
                remaining -= quantity;
            }
            Require(remaining == 0, "pickup slot increase lacks its actual compatible admission");
            SetPickupXmlStack(expected, next.Stack.Value);
            Require(XNode.DeepEquals(expected, nextXml), "pickup changed unexplained persisted inventory metadata");
            if (!first.IsEmpty) VerifyPickupRawRow(firstRows[index], nextRows[index], next.Stack.Value);
            if (next.Stack != initial)
                changes.Add(new(index, next.ItemReferenceId!, next.QualifiedItemId!, next.Quality!.Value, initial, next.Stack.Value));
        }
        Require(admissions.All(row => row.Remaining == 0), "pickup admissions exceed the verified native slot increases");
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
}
