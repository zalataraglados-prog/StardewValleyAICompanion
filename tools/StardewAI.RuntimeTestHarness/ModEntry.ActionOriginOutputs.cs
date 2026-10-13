using Microsoft.Xna.Framework;
using System.Runtime.CompilerServices;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const int MaximumActionOriginQueueRecords = 64;
    private const int MaximumActionOriginQueues = 128;

    private readonly List<PendingExecution> pendingActionOriginCaptures =
        new();
    private readonly Dictionary<string, List<ActionOriginOutputRecord>>
        actionOriginOutputs = new(StringComparer.Ordinal);
    private long actionOriginSequence;

    private void BeginActionOriginOutputCapture(PendingExecution pending)
    {
        var request = pending.Request;
        if (!Context.IsWorldReady ||
            !IsActionOriginProducer(request) ||
            string.IsNullOrWhiteSpace(request.QueueId) ||
            string.IsNullOrWhiteSpace(request.QueueItemId) ||
            string.IsNullOrWhiteSpace(request.AcquisitionRouteKind) ||
            string.IsNullOrWhiteSpace(
                request.AcquisitionRouteOccurrenceId))
        {
            return;
        }

        var location = Game1.currentLocation;
        var preexistingChunkItems = CaptureDebrisChunkItems(location);
        var capture = new ActionOriginOutputCapture(
            location,
            request.AcquisitionRouteKind,
            request.AcquisitionRouteOccurrenceId,
            CaptureDebrisIdentitySet(location),
            preexistingChunkItems.Keys.ToHashSet(
                ReferenceIdentityComparer<Chunk>.Instance),
            preexistingChunkItems,
            CaptureInventoryItemCounts());
        pending.ActionOriginOutputCapture = capture;
        pendingActionOriginCaptures.Add(pending);
    }

    private void FinalizeActionOriginOutputCaptures()
    {
        for (var index = pendingActionOriginCaptures.Count - 1;
            index >= 0;
            index--)
        {
            var pending = pendingActionOriginCaptures[index];
            if (!pending.Completion.Task.IsCompleted)
            {
                continue;
            }

            pendingActionOriginCaptures.RemoveAt(index);
            var capture = pending.ActionOriginOutputCapture;
            pending.ActionOriginOutputCapture = null;
            if (capture is null)
            {
                continue;
            }

            var result = pending.Completion.Task.IsCompletedSuccessfully
                ? pending.Completion.Task.Result
                : null;
            var sameLocation = Context.IsWorldReady &&
                ReferenceEquals(Game1.currentLocation, capture.Location);
            var appliedAndVerified = result is not null &&
                result.Status == "applied" &&
                result.PrimitiveVerificationStatus == "verified";
            var producedDebris = sameLocation
                ? capture.Location.debris
                    .Where(debris => !capture.DebrisBefore.Contains(debris))
                    .ToHashSet(ReferenceIdentityComparer<Debris>.Instance)
                : new HashSet<Debris>(
                    ReferenceIdentityComparer<Debris>.Instance);
            var producedChunks = sameLocation
                ? capture.Location.debris
                    .SelectMany(debris => debris.Chunks)
                    .Where(chunk => !capture.ChunksBefore.Contains(chunk))
                    .ToHashSet(ReferenceIdentityComparer<Chunk>.Instance)
                : new HashSet<Chunk>(
                    ReferenceIdentityComparer<Chunk>.Instance);
            var record = new ActionOriginOutputRecord(
                pending.Request.QueueId,
                pending.Request.QueueItemId,
                capture.RouteKind,
                capture.RouteOccurrenceId,
                capture.Location,
                capture.InventoryBefore,
                producedDebris,
                producedChunks,
                capture.PreexistingChunkItems,
                appliedAndVerified,
                sameLocation,
                ++actionOriginSequence);
            StoreActionOriginOutput(record);
        }
    }

    private void StoreActionOriginOutput(ActionOriginOutputRecord record)
    {
        var key = ActionOriginKey(
            record.QueueId,
            record.RouteKind,
            record.RouteOccurrenceId);
        if (!actionOriginOutputs.TryGetValue(key, out var records))
        {
            records = new List<ActionOriginOutputRecord>();
            actionOriginOutputs[key] = records;
        }
        records.Add(record);
        if (records.Count > MaximumActionOriginQueueRecords)
        {
            records.RemoveRange(
                0,
                records.Count - MaximumActionOriginQueueRecords);
        }

        if (actionOriginOutputs.Count <= MaximumActionOriginQueues)
        {
            return;
        }
        var oldestKey = actionOriginOutputs
            .OrderBy(pair => pair.Value.Count == 0
                ? long.MinValue
                : pair.Value[^1].Sequence)
            .First().Key;
        actionOriginOutputs.Remove(oldestKey);
    }

    private bool TryResolveActionOriginOutput(
        TrainingExecutionRequest request,
        GameLocation location,
        string qualifiedItemId,
        int minimumQuality,
        int guaranteedMinimum,
        out ActionOriginOutputResolution resolution,
        out string reason)
    {
        resolution = null!;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(request.AcquisitionRouteKind) ||
            request.AcquisitionRouteKind !=
                request.DeferredPickupSourceKind ||
            string.IsNullOrWhiteSpace(
                request.AcquisitionRouteOccurrenceId))
        {
            reason = "deferred_pickup_action_origin_identity_missing";
            return false;
        }

        var key = ActionOriginKey(
            request.QueueId,
            request.AcquisitionRouteKind,
            request.AcquisitionRouteOccurrenceId);
        if (!actionOriginOutputs.TryGetValue(key, out var records) ||
            records.Count == 0)
        {
            reason = "deferred_pickup_action_origin_not_recorded";
            return false;
        }

        var record = records[^1];
        if (!record.AppliedAndVerified ||
            !record.SameLocation ||
            !ReferenceEquals(record.Location, location))
        {
            reason =
                "deferred_pickup_action_origin_source_not_verified";
            return false;
        }

        var currentProducedDebris = location.debris
            .Where(debris =>
                DebrisMatches(
                    debris,
                    qualifiedItemId,
                    minimumQuality) &&
                (record.ProducedDebris.Contains(debris) ||
                 debris.Chunks.Any(record.ProducedChunks.Contains)))
            .ToArray();
        var currentProducedChunks = currentProducedDebris
            .SelectMany(debris => debris.Chunks)
            .Where(record.ProducedChunks.Contains)
            .ToArray();
        var liveChunks = location.debris
            .SelectMany(debris => debris.Chunks)
            .ToHashSet(ReferenceIdentityComparer<Chunk>.Instance);
        var preexistingCollectionAmbiguous =
            record.PreexistingChunkItems.Any(pair =>
                string.Equals(
                    pair.Value.QualifiedItemId,
                    qualifiedItemId,
                    StringComparison.OrdinalIgnoreCase) &&
                pair.Value.Quality >= minimumQuality &&
                !liveChunks.Contains(pair.Key));
        var inventoryIncrease = Math.Max(
            0,
            CountInventoryItemAtOrAboveQuality(
                qualifiedItemId,
                minimumQuality) -
            InventoryQuantity(
                record.InventoryBefore,
                qualifiedItemId,
                minimumQuality));
        var directInventoryQuantity = preexistingCollectionAmbiguous
            ? 0
            : inventoryIncrease;
        var retainedDebrisQuantity = ProducedDebrisQuantity(
            record,
            currentProducedDebris,
            currentProducedChunks);
        if (directInventoryQuantity + retainedDebrisQuantity <
            guaranteedMinimum)
        {
            reason = preexistingCollectionAmbiguous && inventoryIncrease > 0
                ? "deferred_pickup_action_origin_preexisting_collection_ambiguous"
                : "deferred_pickup_action_origin_conservation_mismatch";
            return false;
        }

        resolution = new ActionOriginOutputResolution(
            record,
            currentProducedDebris,
            currentProducedChunks,
            directInventoryQuantity,
            retainedDebrisQuantity);
        return true;
    }

    private void ConsumeActionOriginOutput(TrainingExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QueueId) ||
            string.IsNullOrWhiteSpace(request.AcquisitionRouteKind) ||
            string.IsNullOrWhiteSpace(
                request.AcquisitionRouteOccurrenceId))
        {
            return;
        }
        actionOriginOutputs.Remove(ActionOriginKey(
            request.QueueId,
            request.AcquisitionRouteKind,
            request.AcquisitionRouteOccurrenceId));
    }

    private static bool IsActionOriginProducer(
        TrainingExecutionRequest request) =>
        request.AcquisitionRouteKind switch
        {
            "native_wild_tree_chop_drop" or
            "native_location_artifact_spot" or
            "native_tree_moss_harvest" =>
                request.OptionId == "executor.clear_obstacle",
            "native_radioactive_ore_node" =>
                request.OptionId == "executor.mine_stone",
            "native_monster_drop_table" => request.OptionId is
                "executor.combat_monster" or
                "executor.shoot_monster" or
                "executor.place_bomb",
            _ => false
        };

    private static string ActionOriginKey(
        string queueId,
        string routeKind,
        string routeOccurrenceId) =>
        queueId + "\u001f" + routeKind + "\u001f" + routeOccurrenceId;

    private static HashSet<Debris> CaptureDebrisIdentitySet(
        GameLocation location) => location.debris.ToHashSet(
            ReferenceIdentityComparer<Debris>.Instance);

    private static HashSet<Chunk> CaptureDebrisChunkIdentitySet(
        GameLocation location) => location.debris
            .SelectMany(debris => debris.Chunks)
            .ToHashSet(ReferenceIdentityComparer<Chunk>.Instance);

    private static Dictionary<Chunk, ActionOriginItemKey>
        CaptureDebrisChunkItems(GameLocation location) => location.debris
            .Where(debris => !string.IsNullOrWhiteSpace(
                DebrisQualifiedItemId(debris)))
            .SelectMany(debris => debris.Chunks.Select(chunk => new
            {
                Chunk = chunk,
                Item = new ActionOriginItemKey(
                    DebrisQualifiedItemId(debris),
                    debris.item?.Quality ?? debris.itemQuality)
            }))
            .ToDictionary(
                value => value.Chunk,
                value => value.Item,
                ReferenceIdentityComparer<Chunk>.Instance);

    private static Dictionary<ActionOriginItemKey, int>
        CaptureInventoryItemCounts() => Game1.player.Items
            .Where(item => item is not null)
            .GroupBy(item => new ActionOriginItemKey(
                item!.QualifiedItemId,
                item.Quality))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item!.Stack));

    private static int CountInventoryItemAtOrAboveQuality(
        string qualifiedItemId,
        int minimumQuality) => Game1.player.Items
            .Where(item => item is not null &&
                string.Equals(
                    item.QualifiedItemId,
                    qualifiedItemId,
                    StringComparison.OrdinalIgnoreCase) &&
                item.Quality >= minimumQuality)
            .Sum(item => item!.Stack);

    private static int InventoryQuantity(
        IReadOnlyDictionary<ActionOriginItemKey, int> counts,
        string qualifiedItemId,
        int minimumQuality) => counts
            .Where(pair => string.Equals(
                    pair.Key.QualifiedItemId,
                    qualifiedItemId,
                    StringComparison.OrdinalIgnoreCase) &&
                pair.Key.Quality >= minimumQuality)
            .Sum(pair => pair.Value);

    private static bool DebrisMatches(
        Debris debris,
        string qualifiedItemId,
        int minimumQuality) => string.Equals(
            DebrisQualifiedItemId(debris),
            qualifiedItemId,
            StringComparison.OrdinalIgnoreCase) &&
        (debris.item?.Quality ?? debris.itemQuality) >= minimumQuality;

    private static int ProducedDebrisQuantity(
        ActionOriginOutputRecord record,
        IReadOnlyCollection<Debris> debris,
        IReadOnlyCollection<Chunk> producedChunks)
    {
        var chunkQuantity = producedChunks.Count;
        var stackQuantity = debris
            .Where(record.ProducedDebris.Contains)
            .Sum(value => Math.Max(0, value.item?.Stack ?? 0));
        return Math.Max(chunkQuantity, stackQuantity);
    }

    private static Chunk? NearestActionOriginChunk(
        GameLocation location,
        IReadOnlySet<Debris> debrisBefore,
        IReadOnlySet<Chunk> chunksBefore,
        string qualifiedItemId,
        int exactQuality,
        Point playerTile) => location.debris
            .Where(debris =>
                string.Equals(
                    DebrisQualifiedItemId(debris),
                    qualifiedItemId,
                    StringComparison.OrdinalIgnoreCase) &&
                (debris.item?.Quality ?? debris.itemQuality) == exactQuality)
            .SelectMany(debris => debris.Chunks.Where(chunk =>
                !debrisBefore.Contains(debris) ||
                !chunksBefore.Contains(chunk)))
            .OrderBy(chunk => ManhattanDistance(
                playerTile,
                DebrisChunkTile(chunk)))
            .FirstOrDefault();

    private sealed record ActionOriginOutputCapture(
        GameLocation Location,
        string RouteKind,
        string RouteOccurrenceId,
        HashSet<Debris> DebrisBefore,
        HashSet<Chunk> ChunksBefore,
        IReadOnlyDictionary<Chunk, ActionOriginItemKey> PreexistingChunkItems,
        IReadOnlyDictionary<ActionOriginItemKey, int> InventoryBefore);

    private sealed record ActionOriginOutputRecord(
        string QueueId,
        string QueueItemId,
        string RouteKind,
        string RouteOccurrenceId,
        GameLocation Location,
        IReadOnlyDictionary<ActionOriginItemKey, int> InventoryBefore,
        HashSet<Debris> ProducedDebris,
        HashSet<Chunk> ProducedChunks,
        IReadOnlyDictionary<Chunk, ActionOriginItemKey> PreexistingChunkItems,
        bool AppliedAndVerified,
        bool SameLocation,
        long Sequence);

    private sealed record ActionOriginOutputResolution(
        ActionOriginOutputRecord Record,
        Debris[] CurrentProducedDebris,
        Chunk[] CurrentProducedChunks,
        int DirectInventoryQuantity,
        int RetainedDebrisQuantity);

    private readonly record struct ActionOriginItemKey(
        string QualifiedItemId,
        int Quality);

    private sealed class ReferenceIdentityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static ReferenceIdentityComparer<T> Instance { get; } = new();

        public bool Equals(T? left, T? right) =>
            ReferenceEquals(left, right);

        public int GetHashCode(T value) =>
            RuntimeHelpers.GetHashCode(value);
    }
}
