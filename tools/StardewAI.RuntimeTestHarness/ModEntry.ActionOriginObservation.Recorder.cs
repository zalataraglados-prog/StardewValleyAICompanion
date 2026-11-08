using System.Security.Cryptography;
using System.Text;
using StardewAI.Contracts.Training;
using StardewAI.RuntimePrimitives;
using StardewModdingAPI;
using StardewValley;
using StardewValley.SaveSerialization;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed partial class NativeCollectionObservation
    {
        private const int MaximumCollections = 256;
        private const int MaximumDebrisRows = 512;
        private const int MaximumChunkIds = 4096;
        private static readonly string SessionId = Guid.NewGuid().ToString("N");
        private static readonly WeakNativeReferenceIds ReferenceIds = new();
        [ThreadStatic] private static NativeCollectionObservation? current;
        private readonly TrainingExecutionRequest request;
        private readonly ActionOriginOutputCapture capture;
        private readonly Farmer farmer;
        private readonly int threadId = Environment.CurrentManagedThreadId;
        private readonly long beforeTick = Game1.ticks;
        private readonly HashSet<string> diagnostics = new(StringComparer.Ordinal);
        private readonly BoundedNativeObservationBuffer<ActionOriginCollectionObservation> collections = new(MaximumCollections);
        private readonly ActionOriginObservedDebris[] beforeDebris;
        private CollectionContext? collection;
        private int collectionCount;
        private int admissionCount;
        private int remainingChunkIds = MaximumChunkIds;
        private int remainingPayloadCharacters = 1024 * 1024;
        private bool active = true;

        internal NativeCollectionObservation(TrainingExecutionRequest request, ActionOriginOutputCapture capture)
        {
            this.request = request; this.capture = capture; farmer = Game1.player;
            beforeDebris = ReadDebris(capture.Location);
        }

        internal static void Begin(NativeCollectionObservation value)
        {
            if (current is not null) throw new InvalidOperationException("Native output observation already owns this thread.");
            current = value;
        }

        internal static bool CanCapture(GameLocation location, Farmer actor)
        {
            if (location.debris.Count > MaximumDebrisRows || actor.Items.Count > MaximumDebrisRows) return false;
            var chunks = MaximumChunkIds;
            foreach (var debris in location.debris)
            {
                if (debris.Chunks.Count > chunks) return false;
                chunks -= debris.Chunks.Count;
            }
            return true;
        }

        internal void Stop()
        {
            active = false;
            if (ReferenceEquals(current, this)) current = null;
            collection = null;
        }

        private void Reject(string reason)
        {
            if (diagnostics.Count < 16) diagnostics.Add(reason);
        }

        internal ActionOriginObservationReceipt Finish(ActionOriginOutputCapture original)
        {
            if (!ReferenceEquals(original, capture) || !ReferenceEquals(current, this) ||
                Environment.CurrentManagedThreadId != threadId || collection is not null)
                Reject("observation_context_incomplete");
            Stop();
            if (collections.Snapshot().Any(row => row.NativeReturn is null || row.InventoryAdmissions.Any(admission => admission.NativeReturn is null)))
                Reject("observation_native_return_missing");
            var sameLocation = Context.IsWorldReady && ReferenceEquals(Game1.currentLocation, capture.Location) &&
                ReferenceEquals(Game1.player, farmer);
            if (!sameLocation) Reject("observation_location_or_actor_changed");
            var afterDebris = sameLocation ? ReadDebris(capture.Location) : Array.Empty<ActionOriginObservedDebris>();
            var boundedInventory = sameLocation && farmer.Items.Count <= MaximumDebrisRows;
            if (!boundedInventory) Reject("observation_inventory_limit_or_context_changed");
            return new ActionOriginObservationReceipt
            {
                ScopeId = request.ActionOriginObservationScopeId!, CaptureSessionId = SessionId,
                RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
                RequestNonce = request.RequestNonce, BeforeStateHash = request.BeforeStateHash,
                LocationId = capture.Location.NameOrUniqueName, BeforeGameTick = beforeTick, AfterGameTick = Game1.ticks,
                Status = diagnostics.Count == 0 ? "observed" : "unavailable", Diagnostics = diagnostics.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                BeforeDebris = beforeDebris, AfterDebris = afterDebris,
                InventoryBefore = InventoryRows(capture.InventoryBefore),
                InventoryAfter = boundedInventory ? InventoryRows(CaptureInventoryItemCounts()) : Array.Empty<ActionOriginInventoryCount>(),
                Collections = collections.Snapshot().OrderBy(row => row.Ordinal).ToArray()
            };
        }

        private static ActionOriginInventoryCount[] InventoryRows(IReadOnlyDictionary<ActionOriginItemKey, int> rows) => rows
            .OrderBy(pair => pair.Key.QualifiedItemId, StringComparer.Ordinal).ThenBy(pair => pair.Key.Quality)
            .Select(pair => new ActionOriginInventoryCount
            { QualifiedItemId = pair.Key.QualifiedItemId, Quality = pair.Key.Quality, Quantity = pair.Value }).ToArray();

        private ActionOriginObservedDebris[] ReadDebris(GameLocation location)
        {
            var rows = new List<ActionOriginObservedDebris>();
            foreach (var debris in location.debris)
            {
                if (rows.Count == MaximumDebrisRows) { Reject("observation_debris_limit_exceeded"); break; }
                rows.Add(ReadDebris(debris));
            }
            return rows.ToArray();
        }

        private ActionOriginObservedDebris ReadDebris(Debris debris)
        {
            if (debris.GetType() != typeof(Debris)) Reject("observation_custom_debris_runtime_type");
            if (debris.Chunks.Count > remainingChunkIds) Reject("observation_chunk_limit_exceeded");
            var chunks = debris.Chunks.Take(remainingChunkIds).Select(chunk => ReferenceIds.For(chunk)).ToArray();
            remainingChunkIds -= chunks.Length;
            return new ActionOriginObservedDebris
            {
                DebrisId = ReferenceIds.For(debris), RuntimeType = debris.GetType().FullName ?? debris.GetType().Name,
                DebrisType = debris.debrisType.Value.ToString(), ChunkType = debris.chunkType.Value,
                RawItemId = debris.itemId.Value ?? string.Empty, ItemQuality = debris.itemQuality,
                HeldItemStack = debris.item?.Stack, HeldItem = debris.item is null ? null : ReadItem(debris.item),
                ChunkIds = chunks
            };
        }

        private ActionOriginObservedItem ReadItem(Item item)
        {
            if (item.GetType() != typeof(StardewValley.Object))
            {
                Reject("observation_item_runtime_type_not_supported");
                return new ActionOriginObservedItem { RuntimeType = item.GetType().FullName ?? item.GetType().Name };
            }
            var result = new ActionOriginObservedItem
            {
                RuntimeType = item.GetType().FullName!, QualifiedItemId = item.QualifiedItemId, Quality = item.Quality
            };
            var objectItem = (StardewValley.Object)item;
            if (objectItem.heldObject.Value is not null || objectItem.lastInputItem.Value is not null)
            { Reject("observation_nested_item_graph_not_supported"); return result; }
            if (remainingPayloadCharacters == 0) { Reject("observation_item_payload_limit_exceeded"); return result; }
            using var writer = new CappedNativeObservationWriter(Math.Min(65536, remainingPayloadCharacters));
            try
            {
                // Observe the live payload: getOne() constructs Object and consumes native RNG.
                SaveSerializer.GetSerializer(item.GetType()).Serialize(writer, item);
                result.SerializedStateXml = writer.ToString();
                remainingPayloadCharacters -= result.SerializedStateXml.Length;
                result.SerializedStateSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.SerializedStateXml))).ToLowerInvariant();
            }
            catch (Exception error)
            {
                remainingPayloadCharacters = 0;
                Reject(writer.LimitExceeded ? "observation_item_payload_limit_exceeded" : "observation_item_payload_failed:" + error.GetType().Name);
            }
            return result;
        }

        private static int InventoryCount(Farmer actor, ActionOriginObservedItem item) => actor.Items.Where(value => value is not null &&
            value.QualifiedItemId == item.QualifiedItemId && value.Quality == item.Quality).Sum(value => value!.Stack);
    }
}
