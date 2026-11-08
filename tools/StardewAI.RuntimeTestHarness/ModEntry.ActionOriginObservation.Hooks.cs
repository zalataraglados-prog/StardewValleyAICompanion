using StardewAI.Contracts.Training;
using StardewAI.RuntimePrimitives;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed partial class NativeCollectionObservation
    {
        public sealed class CollectionContext
        {
            internal readonly NativeCollectionObservation Owner;
            internal readonly CollectionContext? Previous;
            internal readonly ActionOriginCollectionObservation Row;
            internal readonly BoundedNativeObservationBuffer<ActionOriginInventoryAdmission> Admissions = new(MaximumCollections);
            internal CollectionContext(NativeCollectionObservation owner, ActionOriginCollectionObservation row)
            { Owner = owner; Row = row; Previous = owner.collection; }
        }

        public sealed record AdmissionContext(CollectionContext Collection, Item Item, ActionOriginInventoryAdmission Row);

        public static void CollectPrefix(Debris __instance, Farmer farmer, Chunk? chunk, out CollectionContext? __state)
        {
            __state = null;
            var owner = current;
            if (owner is null || !owner.active) return;
            try
            {
                if (!ReferenceEquals(farmer, owner.farmer)) { owner.Reject("observation_other_farmer_collection"); return; }
                if (++owner.collectionCount > MaximumCollections) { owner.Reject("observation_collection_limit_exceeded"); return; }
                var row = new ActionOriginCollectionObservation
                {
                    Ordinal = owner.collectionCount, DebrisId = ReferenceIds.For(__instance),
                    ChunkId = chunk is null ? null : ReferenceIds.For(chunk), BeforeGameTick = Game1.ticks,
                    DebrisBefore = owner.ReadDebris(__instance)
                };
                if (!owner.collections.TryAdd(row)) { owner.Reject("observation_collection_limit_exceeded"); return; }
                __state = new CollectionContext(owner, row);
                owner.collection = __state;
            }
            catch (Exception error) { owner.Reject("observation_collect_prefix_failed:" + error.GetType().Name); }
        }

        public static void CollectPostfix(bool __result, CollectionContext? __state)
        {
            if (__state is null || !__state.Owner.active) return;
            var owner = __state.Owner;
            try
            {
                if (!ReferenceEquals(owner.collection, __state)) owner.Reject("observation_collect_context_mismatch");
                __state.Row.NativeReturn = __result;
                __state.Row.AfterGameTick = Game1.ticks;
                __state.Row.InventoryAdmissions = __state.Admissions.Snapshot();
            }
            catch (Exception error) { owner.Reject("observation_collect_postfix_failed:" + error.GetType().Name); }
            finally { owner.collection = __state.Previous; }
        }

        public static void AdmissionPrefix(Farmer __instance, Item? item, out AdmissionContext? __state)
        {
            __state = null;
            var owner = current;
            if (owner is null || !owner.active || owner.collection is null) return;
            try
            {
                if (!ReferenceEquals(__instance, owner.farmer) || item is null)
                { owner.Reject("observation_inventory_actor_or_item_invalid"); return; }
                if (++owner.admissionCount > MaximumCollections) { owner.Reject("observation_admission_limit_exceeded"); return; }
                var observed = owner.ReadItem(item);
                var row = new ActionOriginInventoryAdmission
                {
                    Item = observed, OfferedQuantity = item.Stack,
                    InventoryBefore = InventoryCount(__instance, observed)
                };
                if (!owner.collection.Admissions.TryAdd(row)) { owner.Reject("observation_admission_limit_exceeded"); return; }
                __state = new AdmissionContext(owner.collection, item, row);
            }
            catch (Exception error) { owner.Reject("observation_admission_prefix_failed:" + error.GetType().Name); }
        }

        public static void AdmissionPostfix(Farmer __instance, bool __result, AdmissionContext? __state)
        {
            if (__state is null || !__state.Collection.Owner.active) return;
            var owner = __state.Collection.Owner;
            try
            {
                if (!ReferenceEquals(owner.collection, __state.Collection)) owner.Reject("observation_admission_context_mismatch");
                __state.Row.NativeReturn = __result;
                __state.Row.ItemStackAfterCall = __state.Item.Stack;
                __state.Row.InventoryAfter = InventoryCount(__instance, __state.Row.Item!);
            }
            catch (Exception error) { owner.Reject("observation_admission_postfix_failed:" + error.GetType().Name); }
        }
    }
}
