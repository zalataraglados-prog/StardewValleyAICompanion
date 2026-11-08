using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

internal static partial class NativeLiveItemPayloadCapture
{
    private interface IGraphObserver
    {
        void Item(Item item);
        void Enchantment(BaseEnchantment enchantment);
        void Collection(object parent, string role, int count);
        void Edge(object parent, string role, int ordinal, object? target);
    }

    // The ORIGINAL traversal, hoisted with optional observations. No observer means the old
    // admission/namespace/cycle/depth/count policy, including its diagnostics, remains intact.
    private sealed class GraphCheck
    {
        private readonly HashSet<object> ancestors = new(ReferenceEqualityComparer.Instance);
        private readonly IGraphObserver? observer;
        private int nodes;
        internal GraphCheck(IGraphObserver? observer = null) => this.observer = observer;

        internal void Visit(Item? item, int depth)
        {
            if (item is null) return;
            var type = item.GetType();
            if (!SupportedItem(type))
                throw new InvalidDataException("Live item payload runtime type is unsupported: " + type.FullName);
            Enter(item, depth);
            try
            {
                observer?.Item(item);
                if (item is StardewValley.Object obj)
                {
                    if (observer is not null && (obj.heldObject is null || obj.lastInputItem is null))
                        throw new InvalidDataException("Native inventory graph Object child wrapper is unavailable.");
                    var held = obj.heldObject!.Value;
                    observer?.Edge(obj, NativeInventoryGraphRoles.HeldObject, 0, held);
                    Visit(held, depth + 1);
                    var input = obj.lastInputItem!.Value;
                    observer?.Edge(obj, NativeInventoryGraphRoles.LastInputItem, 0, input);
                    Visit(input, depth + 1);
                }
                if (item is Tool tool)
                {
                    if (observer is not null && (tool.attachments is null || tool.enchantments is null || tool.previousEnchantments is null))
                        throw new InvalidDataException("Native inventory graph Tool collection wrapper is unavailable.");
                    CheckCount(tool.attachments!.Length);
                    observer?.Collection(tool, NativeInventoryGraphRoles.Attachments, tool.attachments!.Length);
                    var ordinal = 0;
                    foreach (var attachment in tool.attachments!)
                    {
                        observer?.Edge(tool, NativeInventoryGraphRoles.Attachments, ordinal, attachment);
                        ordinal++;
                        Visit(attachment, depth + 1);
                    }
                    observer?.Collection(tool, NativeInventoryGraphRoles.Attachments, ordinal);
                    CheckCount(tool.enchantments!.Count);
                    observer?.Collection(tool, NativeInventoryGraphRoles.Enchantments, tool.enchantments!.Count);
                    ordinal = 0;
                    foreach (var enchantment in tool.enchantments!)
                    {
                        observer?.Edge(tool, NativeInventoryGraphRoles.Enchantments, ordinal, enchantment);
                        ordinal++;
                        VisitEnchantment(enchantment, depth + 1);
                    }
                    observer?.Collection(tool, NativeInventoryGraphRoles.Enchantments, ordinal);
                    CheckCount(tool.previousEnchantments!.Count);
                }
                if (item is CombinedRing ring)
                {
                    if (observer is not null && ring.combinedRings is null)
                        throw new InvalidDataException("Native inventory graph Ring collection wrapper is unavailable.");
                    CheckCount(ring.combinedRings!.Count);
                    observer?.Collection(ring, NativeInventoryGraphRoles.CombinedRings, ring.combinedRings!.Count);
                    var ordinal = 0;
                    foreach (var child in ring.combinedRings!)
                    {
                        observer?.Edge(ring, NativeInventoryGraphRoles.CombinedRings, ordinal, child);
                        ordinal++;
                        Visit(child, depth + 1);
                    }
                    observer?.Collection(ring, NativeInventoryGraphRoles.CombinedRings, ordinal);
                }
            }
            finally { ancestors.Remove(item); }
        }

        internal void VisitEnchantment(BaseEnchantment? enchantment, int depth)
        {
            if (enchantment is null) return;
            var type = enchantment.GetType();
            // Do not replace this legacy namespace guard with the new closed catalog. Only
            // the observing codec adds exact44-type admission before raw field reads.
            if (type.Assembly != typeof(BaseEnchantment).Assembly || type.Namespace != typeof(BaseEnchantment).Namespace)
                throw new InvalidDataException("Live item payload enchantment type is unsupported: " + type.FullName);
            Enter(enchantment, depth);
            try { observer?.Enchantment(enchantment); }
            finally { ancestors.Remove(enchantment); }
        }

        private static void CheckCount(int count)
        {
            if (count is < 0 or > MaximumGraphNodes)
                throw new InvalidDataException("Live item payload graph collection exceeds its bound.");
        }

        private void Enter(object value, int depth)
        {
            if (depth > MaximumGraphDepth || ++nodes > MaximumGraphNodes)
                throw new InvalidDataException("Live item payload graph depth/node bound exceeded.");
            if (!ancestors.Add(value))
                throw new InvalidDataException("Live item payload graph contains a cycle.");
        }
    }
}
