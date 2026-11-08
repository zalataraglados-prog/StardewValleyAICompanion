using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record NativeObservedInventoryPayloadSlot(int SlotIndex, bool IsEmpty, string? ItemReferenceId,
    string? RuntimeType, string? QualifiedItemId, bool? IsTool, int? Quality, int? Stack, string? Xml, string? Sha256);

public sealed record NativeObservedInventoryPayloadState(long GameTick, int SelectedSlotIndex,
    string? SelectedItemReferenceId, string? CurrentToolReferenceId, IReadOnlyList<NativeObservedInventoryPayloadSlot> Slots);

public sealed record NativeInventoryPayloadSummary(string ScopeId, string CaptureSessionId,
    NativeObservedInventoryPayloadState Before, NativeObservedInventoryPayloadState After)
{
    public bool FullTransientStateVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

public static partial class NativeOutputObservationBoundary
{
    public static NativeInventoryPayloadSummary ReadInventoryPayload(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, long earliestTick, long latestTick, string locationId, string actorId)
    {
        var payloadCharacters = 0;
        return ReadInventoryPayloadWithBudget(request, receipt, earliestTick, latestTick, locationId, actorId, ref payloadCharacters);
    }

    private static NativeInventoryPayloadSummary ReadInventoryPayloadWithBudget(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, long earliestTick, long latestTick, string locationId, string actorId,
        ref int payloadCharacters, Dictionary<Guid, XElement>? beforeXml = null, Dictionary<Guid, XElement>? afterXml = null)
    {
        Require(request.ActionOriginInventoryPayload && request.ActionOriginToolInputs && request.ActionOriginSnapshotAnchor &&
                !string.IsNullOrWhiteSpace(actorId), "persisted inventory payload was not explicitly requested with its original scope");
        var original = ReadWithPayloadBudget(request, receipt, earliestTick, latestTick, locationId, ref payloadCharacters);
        var types = new Dictionary<Guid, string>();
        var before = ReadInventoryPayloadState(receipt.InventoryPayloadBefore, receipt.BeforeGameTick, actorId,
            locationId, receipt.InventoryBefore, types, ref payloadCharacters, beforeXml);
        var after = ReadInventoryPayloadState(receipt.InventoryPayloadAfter, receipt.AfterGameTick, actorId,
            locationId, receipt.InventoryAfter, types, ref payloadCharacters, afterXml);
        return new(original.ScopeId, original.CaptureSessionId, before, after);
    }

    private static NativeObservedInventoryPayloadState ReadInventoryPayloadState(ActionOriginInventoryPayload? state,
        long tick, string actorId, string locationId, ActionOriginInventoryCount[] inventory,
        Dictionary<Guid, string> types, ref int payloadCharacters, Dictionary<Guid, XElement>? xmlRoots)
    {
        Require(state is not null && state.SchemaVersion == "native_inventory_payload.v1" && state.PayloadKind == "persisted_xml" &&
                state.GameTick == tick && state.ActorId == actorId && state.LocationId == locationId &&
                state.SelectionObserved == true && state.Slots is { Length: <= 512 } &&
                state.SelectedSlotIndex is >= -1 && state.SelectedSlotIndex < state.Slots.Length,
            "inventory payload header, selection or native endpoint differs");
        var slots = new List<NativeObservedInventoryPayloadSlot>();
        var references = new HashSet<Guid>();
        var quantities = new Dictionary<ItemKey, int>();
        foreach (var slot in state!.Slots!)
        {
            Require(slot is not null && slot.SlotIndex == slots.Count && slot.IsEmpty.HasValue,
                "inventory slot ordering or availability is incomplete");
            if (slot!.IsEmpty!.Value)
            {
                Require(slot.ItemReferenceId is null && slot.RuntimeType is null && slot.QualifiedItemId is null &&
                        slot.IsTool is null && slot.Quality is null && slot.Stack is null && slot.SerializedStateXml is null &&
                        slot.SerializedStateSha256 is null, "empty inventory slot contains fabricated payload/header fields");
                slots.Add(new(slots.Count, true, null, null, null, null, null, null, null, null));
                continue;
            }
            Require(slot.ItemReferenceId is { Length: 32 } && Guid.TryParseExact(slot.ItemReferenceId, "N", out _) &&
                    slot.RuntimeType is { Length: <= 256 } && slot.RuntimeType.StartsWith("StardewValley.", StringComparison.Ordinal) &&
                    !slot.RuntimeType.Any(char.IsControl) && slot.QualifiedItemId is { Length: <= 256 } &&
                    !string.IsNullOrWhiteSpace(slot.QualifiedItemId) && !slot.QualifiedItemId.Any(char.IsControl) &&
                    slot.IsTool.HasValue && slot.Quality.HasValue && slot.Stack is > 0,
                "occupied inventory slot is missing its actual identity, type or quantity");
            var reference = Guid.ParseExact(slot.ItemReferenceId!, "N");
            var runtimeType = slot.RuntimeType!;
            Require(references.Add(reference) && (!types.TryGetValue(reference, out var prior) || prior == slot.RuntimeType),
                "inventory reference is aliased within slots or changed its CLR type");
            types[reference] = runtimeType;
            Require(NativeInventoryPayloadTypes.TryGet(runtimeType, out var descriptor) && slot.IsTool == descriptor.IsTool,
                "inventory runtime type is unsupported or its tool classification differs");
            var root = ReadItemXml(slot.SerializedStateXml, slot.SerializedStateSha256, ref payloadCharacters);
            Require(root.Name == descriptor.XmlRoot && root.Attribute(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance")) is null,
                "persisted inventory XML root does not match its actual runtime type");
            ValidateInventoryXmlDepth(slot.SerializedStateXml!);
            ValidateInventoryXmlIdentity(root, descriptor, slot.QualifiedItemId!, slot.Quality!.Value, slot.Stack!.Value);
            xmlRoots?.Add(reference, root);
            var key = new ItemKey(slot.QualifiedItemId!, slot.Quality!.Value);
            var total = (long)quantities.GetValueOrDefault(key) + slot.Stack!.Value;
            Require(total <= int.MaxValue, "inventory payload quantity exceeds its native range");
            quantities[key] = (int)total;
            slots.Add(new(slots.Count, false, slot.ItemReferenceId, slot.RuntimeType, slot.QualifiedItemId,
                slot.IsTool, slot.Quality, slot.Stack, slot.SerializedStateXml, slot.SerializedStateSha256));
        }
        var observed = ReadInventory(inventory);
        Require(quantities.Keys.Concat(observed.Keys).Distinct().All(key =>
                quantities.GetValueOrDefault(key) == observed.GetValueOrDefault(key)),
            "complete slot quantities differ from the original observation denominator");
        var selected = state.SelectedSlotIndex!.Value;
        Require(state.SelectedItemReferenceId == (selected < 0 ? null : slots[selected].ItemReferenceId),
            "selected inventory reference differs from the actual selected slot");
        Require(state.CurrentToolReferenceId is null || slots.Any(slot => !slot.IsEmpty && slot.IsTool == true &&
                slot.ItemReferenceId == state.CurrentToolReferenceId),
            "current native tool is not covered by the recorded inventory slots");
        return new(tick, selected, state.SelectedItemReferenceId, state.CurrentToolReferenceId, slots.AsReadOnly());
    }

    private static void ValidateInventoryXmlDepth(string xml)
    {
        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
        var count = 0;
        while (reader.Read())
        {
            count += 1 + reader.AttributeCount;
            Require(reader.Depth <= 64 && count <= 8192, "inventory XML depth/node budget exceeded");
        }
    }
}
