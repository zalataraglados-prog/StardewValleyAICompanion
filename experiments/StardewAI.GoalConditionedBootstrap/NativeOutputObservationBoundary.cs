using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record NativeObservedInventoryDelta(string QualifiedItemId, int Quality, int InventoryBefore,
    int InventoryAfter, int AdmissionCount);

public sealed record NativeObservedCollectionReference(int Ordinal, string DebrisId, string? ChunkId,
    bool DebrisPresentAtScopeStart, bool ChunkPresentAtScopeStart);

public sealed record NativeOutputObservationSummary(string ScopeId, string CaptureSessionId,
    IReadOnlyList<NativeObservedInventoryDelta> Inventory, IReadOnlyList<NativeObservedCollectionReference> Collections)
{
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

// Validates observations, not their attribution to a producer or full persistent resource effects.
public static partial class NativeOutputObservationBoundary
{
    private readonly record struct ItemKey(string Id, int Quality);

    public static NativeOutputObservationSummary Read(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt,
        long earliestTick, long latestTick, string locationId)
    {
        var payloadCharacters = 0;
        return ReadWithPayloadBudget(request, receipt, earliestTick, latestTick, locationId, ref payloadCharacters);
    }

    private static NativeOutputObservationSummary ReadWithPayloadBudget(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, long earliestTick, long latestTick, string locationId, ref int payloadCharacters)
    {
        Require(request.ExecutionMode == "training_singleplayer" &&
                (request.ActionOriginTillingResources ? NativeTillingObservationContract.IsCompleteRequest(request) :
                    request.OptionId is "executor.move_to_tile" or "executor.clear_obstacle") &&
                !string.IsNullOrWhiteSpace(request.SourcePlanningOptionId) && !string.IsNullOrWhiteSpace(request.SaveIsolationPath) &&
                !string.IsNullOrWhiteSpace(request.ActionOriginObservationScopeId) && request.ActionOriginObservationScopeId.Length <= 128 &&
                !request.ActionOriginObservationScopeId.Any(char.IsControl) &&
                receipt.SchemaVersion == "action_origin_observation.v1" && receipt.Status == "observed" &&
                receipt.Diagnostics is { Length: 0 } && ReferenceId(receipt.CaptureSessionId) &&
                receipt.ScopeId == request.ActionOriginObservationScopeId && receipt.RunId == request.RunId &&
                receipt.QueueId == request.QueueId && receipt.QueueItemId == request.QueueItemId &&
                receipt.RequestNonce == request.RequestNonce && receipt.BeforeStateHash == request.BeforeStateHash &&
                new[] { receipt.RunId, receipt.QueueId, receipt.QueueItemId, receipt.RequestNonce, receipt.BeforeStateHash, locationId }
                    .All(value => !string.IsNullOrWhiteSpace(value)) && receipt.LocationId == locationId &&
                earliestTick >= 0 && receipt.BeforeGameTick >= earliestTick && receipt.AfterGameTick >= receipt.BeforeGameTick &&
                receipt.AfterGameTick <= latestTick,
            "scope identity, status or native tick interval differs from the original request/frame");

        var chunkReferences = 0;
        var before = ReadDebris(receipt.BeforeDebris, ref payloadCharacters, ref chunkReferences);
        ReadDebris(receipt.AfterDebris, ref payloadCharacters, ref chunkReferences);
        var inventoryBefore = ReadInventory(receipt.InventoryBefore);
        var inventoryAfter = ReadInventory(receipt.InventoryAfter);
        Require(receipt.Collections is { Length: <= 256 }, "collection count is unavailable or exceeds its native bound");
        var admissions = new Dictionary<ItemKey, List<(int Before, int After)>>();
        var references = new List<NativeObservedCollectionReference>();
        var admissionCount = 0;
        var previousStartTick = receipt.BeforeGameTick;
        foreach (var collection in receipt.Collections!)
        {
            Require(collection is not null && collection.Ordinal == references.Count + 1 && collection.NativeReturn.HasValue &&
                    collection.BeforeGameTick >= previousStartTick && collection.AfterGameTick >= collection.BeforeGameTick &&
                    collection.AfterGameTick <= receipt.AfterGameTick && collection.DebrisBefore is not null,
                "collection ordinal, return or tick interval is incomplete");
            var debris = collection!.DebrisBefore!;
            previousStartTick = collection.BeforeGameTick;
            ReadDebris(new[] { debris }, ref payloadCharacters, ref chunkReferences);
            Require(collection.DebrisId == debris.DebrisId && (collection.ChunkId is null ||
                    ReferenceId(collection.ChunkId) && debris.ChunkIds.Contains(collection.ChunkId, StringComparer.Ordinal)),
                "collection does not reference its actual debris/chunk");
            before.TryGetValue(collection.DebrisId, out var original);
            references.Add(new(collection.Ordinal, collection.DebrisId, collection.ChunkId, original is not null,
                collection.ChunkId is not null && original is not null && original.ChunkIds.Contains(collection.ChunkId, StringComparer.Ordinal)));
            Require(collection.InventoryAdmissions is not null, "inventory admission list is missing");
            foreach (var admission in collection.InventoryAdmissions!)
            {
                Require(++admissionCount <= 256 && admission is not null && admission.NativeReturn.HasValue &&
                        admission.OfferedQuantity >= 0 && admission.ItemStackAfterCall >= 0 && admission.InventoryBefore >= 0 &&
                        admission.InventoryAfter >= admission.InventoryBefore &&
                        (long)admission.InventoryAfter - admission.InventoryBefore <= admission.OfferedQuantity,
                    "inventory admission is incomplete, unbounded or has an impossible quantity delta");
                var item = ReadItem(admission!.Item, admission.OfferedQuantity, ref payloadCharacters);
                if (!admissions.TryGetValue(item, out var transitions)) admissions[item] = transitions = new();
                transitions.Add((admission.InventoryBefore, admission.InventoryAfter));
            }
        }
        var keys = inventoryBefore.Keys.Concat(inventoryAfter.Keys).Concat(admissions.Keys).Distinct()
            .OrderBy(key => key.Id, StringComparer.Ordinal).ThenBy(key => key.Quality).ToArray();
        var rows = new List<NativeObservedInventoryDelta>();
        foreach (var key in keys)
        {
            var first = inventoryBefore.GetValueOrDefault(key);
            var last = inventoryAfter.GetValueOrDefault(key);
            var observed = admissions.GetValueOrDefault(key) ?? new();
            var cursor = first;
            foreach (var transition in observed)
            {
                Require(transition.Before == cursor, "inventory admission chain has a gap, overlap or foreign baseline");
                cursor = transition.After;
            }
            Require(cursor == last,
                "scope inventory change is not exhausted by the actual admission observations");
            rows.Add(new(key.Id, key.Quality, first, last, observed.Count));
        }
        return new(receipt.ScopeId, receipt.CaptureSessionId, rows.AsReadOnly(), references.AsReadOnly());
    }

    private static Dictionary<ItemKey, int> ReadInventory(ActionOriginInventoryCount[] rows)
    {
        Require(rows is { Length: <= 512 }, "inventory rows are missing or exceed the native bound");
        var result = new Dictionary<ItemKey, int>();
        foreach (var row in rows!)
            Require(row is not null && !string.IsNullOrWhiteSpace(row.QualifiedItemId) && row.Quantity >= 0 &&
                    result.TryAdd(new(row.QualifiedItemId, row.Quality), row.Quantity), "inventory identity repeats or has an invalid quantity");
        return result;
    }

    private static bool ReferenceId(string value) => Guid.TryParseExact(value, "N", out _);
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException("Native output observation: " + reason + ".");
    }
}
