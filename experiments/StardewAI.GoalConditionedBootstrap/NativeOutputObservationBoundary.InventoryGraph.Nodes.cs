using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private static NativeObservedInventoryGraphNode ReadInventoryGraphNode(ActionOriginInventoryGraphNode raw,
        Dictionary<Guid, NativeObservedInventoryPayloadSlot> roots, NativeObservedInventoryTransientEndpoint transients,
        string actorId, string actorReference, Dictionary<Guid, string> references, ref int characters)
    {
        Require(raw.Kind is "item" or "enchantment" && raw.RuntimeType is { Length: <= 256 }, "graph node kind or CLR type is unavailable");
        var item = raw.Kind == "item";
        var type = default(NativeInventoryPayloadType);
        Require(item ? NativeInventoryPayloadTypes.TryGet(raw.RuntimeType, out type) : NativeInventoryEnchantmentTypes.IsSupported(raw.RuntimeType),
            "inventory graph runtime type is outside its exact native domain");
        BindTransientReference(raw.ReferenceId, raw.RuntimeType!, references);
        var id = GraphId(raw.ReferenceId);
        if (!item)
        {
            Require(raw.QualifiedItemId is null && raw.IsTool is null && raw.Quality is null && raw.Stack is null &&
                raw.ItemState is null && raw.AttachmentCount is null && raw.EnchantmentCount is null && raw.CombinedRingCount is null &&
                raw.EnchantmentState?.Applied is not null && raw.EnchantmentState.Level.HasValue,
                "enchantment node contains Item fields or lacks raw state");
            var state = raw.EnchantmentState!;
            return new(raw.ReferenceId!, raw.RuntimeType!, raw.Kind!, null, null,
                new(state.Applied!.Value, ReadTransientText(state.DisplayName, ref characters), state.Level!.Value), null, null, null);
        }
        Require(raw.EnchantmentState is null && raw.IsTool == type.IsTool && raw.Quality.HasValue && raw.Stack.HasValue &&
            raw.QualifiedItemId is { Length: <= 256 } && !string.IsNullOrWhiteSpace(raw.QualifiedItemId) && !raw.QualifiedItemId.Any(char.IsControl),
            "graph Item header or subtype classification is unavailable");
        GraphCollectionCount(raw.AttachmentCount, type.IsTool);
        GraphCollectionCount(raw.EnchantmentCount, type.HasEnchantmentGraphCollection);
        GraphCollectionCount(raw.CombinedRingCount, type.XmlRoot == "CombinedRing");
        NativeObservedInventoryPayloadSlot header;
        NativeObservedItemTransientState stateItem;
        if (roots.TryGetValue(id, out var root))
        {
            Require(raw.ItemState is null && raw.RuntimeType == root.RuntimeType && raw.QualifiedItemId == root.QualifiedItemId &&
                raw.IsTool == root.IsTool && raw.Quality == root.Quality && raw.Stack == root.Stack,
                "graph root duplicates raw state or differs from original slot header");
            header = root;
            stateItem = transients.Slots[root.SlotIndex]!;
        }
        else
        {
            header = new(-1, false, raw.ReferenceId, raw.RuntimeType, raw.QualifiedItemId, raw.IsTool, raw.Quality, raw.Stack, null, null);
            stateItem = ReadItemTransient(raw.ItemState, header, actorId, actorReference, references, ref characters, requireObjectState: true);
        }
        return new(raw.ReferenceId!, raw.RuntimeType!, raw.Kind!, header, stateItem, null,
            raw.AttachmentCount, raw.EnchantmentCount, raw.CombinedRingCount);
    }

    private static void GraphCollectionCount(int? count, bool applicable) => Require(applicable
        ? count is >= 0 and <= NativeInventoryGraphLimits.MaximumCollection : count is null,
        "graph collection count is unavailable or belongs to an incompatible type");
}
