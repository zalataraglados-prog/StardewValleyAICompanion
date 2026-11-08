using System.Globalization;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record NativeObservedTempDataEntry(string Key, string Kind, bool? BooleanValue, string? StringValue);
public sealed record NativeObservedItemCacheState(string? QualifiedItemId, bool ContextTagsDirty, IReadOnlyList<string?>? ContextTags);
public sealed record NativeObservedTransientActor(string ReferenceId, string ActorId, bool IsActor);
public sealed record NativeObservedToolTransientState(NativeObservedTransientActor? LastUser, bool PlayUseSounds,
    string? Description, string? DisplayName);
public sealed record NativeObservedPickaxeTransientState(int BoulderTileX, int BoulderTileY, int HitsToBoulder);
public sealed record NativeObservedItemTransientState(string? SetFlagOnPickup, IReadOnlyList<NativeObservedTempDataEntry>? TempData,
    NativeObservedItemCacheState Cache, NativeObservedToolTransientState? Tool, NativeObservedPickaxeTransientState? Pickaxe);
public sealed record NativeObservedTransientItem(string ReferenceId, string RuntimeType, bool IsTool);
public sealed record NativeObservedSelectionTransientState(string ActorReferenceId, bool ItemStowed,
    NativeObservedTransientItem? TemporaryItem, NativeObservedTransientItem? CurrentItem);
public sealed record NativeObservedInventoryTransientEndpoint(NativeObservedSelectionTransientState Selection,
    IReadOnlyList<NativeObservedItemTransientState?> Slots);
public sealed record NativeInventoryTransientSummary(string ScopeId, string CaptureSessionId,
    NativeObservedInventoryTransientEndpoint Before, NativeObservedInventoryTransientEndpoint After)
{
    public bool FullTransientStateVerified => false;
    public bool NativeOutcomeVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool FormalProductTrainingAuthorized => false;
}

public static partial class NativeOutputObservationBoundary
{
    public static NativeInventoryTransientSummary ReadInventoryTransients(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, long earliestTick, long latestTick, string locationId, string actorId)
    {
        Require(request.ActionOriginInventoryTransients, "native inventory transients were not explicitly requested");
        var characters = 0;
        var original = ReadInventoryPayloadWithBudget(request, receipt, earliestTick, latestTick, locationId, actorId, ref characters);
        var references = new Dictionary<Guid, string>();
        foreach (var slot in original.Before.Slots.Concat(original.After.Slots).Where(slot => !slot.IsEmpty))
            BindTransientReference(slot.ItemReferenceId, slot.RuntimeType!, references);
        var before = ReadTransientEndpoint(receipt.InventoryPayloadBefore!, original.Before, actorId, references, ref characters);
        var after = ReadTransientEndpoint(receipt.InventoryPayloadAfter!, original.After, actorId, references, ref characters);
        Require(before.Selection.ActorReferenceId == after.Selection.ActorReferenceId,
            "native transient actor physical reference changed across the original scope");
        return new(original.ScopeId, original.CaptureSessionId, before, after);
    }

    private static NativeObservedInventoryTransientEndpoint ReadTransientEndpoint(ActionOriginInventoryPayload wire,
        NativeObservedInventoryPayloadState original, string actorId, Dictionary<Guid, string> references, ref int characters)
    {
        var selection = ReadTransientSelection(wire.SelectionTransientState, original, references);
        var slots = new List<NativeObservedItemTransientState?>();
        for (var index = 0; index < original.Slots.Count; index++)
        {
            var slot = original.Slots[index];
            var state = wire.Slots![index].TransientState;
            if (slot.IsEmpty)
            {
                Require(state is null, "empty inventory slot has fabricated transient state");
                slots.Add(null);
            }
            else slots.Add(ReadItemTransient(state, slot, actorId, selection.ActorReferenceId, references, ref characters));
        }
        return new(selection, slots.AsReadOnly());
    }

    private static string? ReadTransientText(ActionOriginNullableText? field, ref int characters)
    {
        Require(field?.IsNull is not null, "native nullable text availability is missing");
        if (field!.IsNull!.Value)
        {
            Require(field.Value is null, "observed null text contains a value");
            return null;
        }
        CountTransientText(field.Value, 4096, ref characters);
        return field.Value!;
    }

    private static void CountTransientText(string? value, int maximum, ref int characters)
    {
        Require(value is not null && value.Length <= maximum && NativeObservationText.IsWellFormedUtf16(value) &&
                (characters += value.Length) <= 1024 * 1024,
            "native transient text is unavailable or exceeds the shared payload budget");
    }

    private static void BindTransientReference(string? reference, string runtimeType, Dictionary<Guid, string> references)
    {
        Require(reference is { Length: 32 } && Guid.TryParseExact(reference, "N", out _),
            "native transient physical reference is unavailable");
        var id = Guid.ParseExact(reference!, "N");
        Require(!references.TryGetValue(id, out var prior) || prior == runtimeType,
            "native transient physical reference changes its CLR type");
        references[id] = runtimeType;
    }

    private static NativeObservedTransientActor? ReadTransientActor(ActionOriginNativeActorReference? value,
        string actorId, string actorReference, Dictionary<Guid, string> references)
    {
        Require(value?.IsNull is not null, "native last-user availability is missing");
        if (value!.IsNull!.Value)
        {
            Require(value.ReferenceId is null && value.ActorId is null && value.IsActor is null,
                "null native last user contains fabricated fields");
            return null;
        }
        Require(value.ActorId is { Length: <= 32 } && long.TryParse(value.ActorId, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out _) && value.IsActor.HasValue,
            "native last-user identity or actor relation is unavailable");
        BindTransientReference(value.ReferenceId, "StardewValley.Farmer", references);
        var same = Guid.ParseExact(value.ReferenceId!, "N") == Guid.ParseExact(actorReference, "N");
        Require(same == value.IsActor && (!same || value.ActorId == actorId),
            "native last-user physical actor relation is inconsistent");
        return new(value.ReferenceId!, value.ActorId!, value.IsActor!.Value);
    }
}
