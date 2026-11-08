using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private static NativeObservedSelectionTransientState ReadTransientSelection(ActionOriginSelectionTransientState? state,
        NativeObservedInventoryPayloadState inventory, Dictionary<Guid, string> references)
    {
        Require(state is not null && state.SchemaVersion == "native_selection_transients.v1" && state.ItemStowed.HasValue,
            "native selection transient branch is unavailable");
        BindTransientReference(state!.ActorReferenceId, "StardewValley.Farmer", references);
        var temporary = ReadTransientItemReference(state.TemporaryItem, references);
        var current = ReadTransientItemReference(state.CurrentItem, references);
        NativeObservedTransientItem? expected = temporary;
        if (temporary is null && !state.ItemStowed!.Value)
        {
            Require(inventory.SelectedSlotIndex >= 0, "native unstowed CurrentItem cannot read a negative inventory index");
            var slot = inventory.Slots[inventory.SelectedSlotIndex];
            expected = slot.IsEmpty ? null : new(slot.ItemReferenceId!, slot.RuntimeType!, slot.IsTool!.Value);
        }
        Require(current == expected, "native CurrentItem differs from its temporary/stowed/selected branch");
        Require(inventory.CurrentToolReferenceId == (current?.IsTool == true ? current.ReferenceId : null),
            "native CurrentTool differs from the resolved actual CurrentItem");
        return new(state.ActorReferenceId!, state.ItemStowed!.Value, temporary, current);
    }

    private static NativeObservedTransientItem? ReadTransientItemReference(ActionOriginNativeItemReference? value,
        Dictionary<Guid, string> references)
    {
        Require(value?.IsNull is not null, "native item-reference availability is missing");
        if (value!.IsNull!.Value)
        {
            Require(value.ReferenceId is null && value.RuntimeType is null && value.IsTool is null,
                "null native item reference contains fabricated fields");
            return null;
        }
        Require(NativeInventoryPayloadTypes.TryGet(value.RuntimeType, out var descriptor) &&
                value.IsTool == descriptor.IsTool, "native transient item-reference type is unavailable or unsupported");
        BindTransientReference(value.ReferenceId, value.RuntimeType!, references);
        return new(value.ReferenceId!, value.RuntimeType!, value.IsTool!.Value);
    }
}
