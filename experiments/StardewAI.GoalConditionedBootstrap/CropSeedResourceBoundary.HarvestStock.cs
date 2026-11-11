using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed record CropHarvestStockSummary(ExactInventoryReceiptEvidence Inventory, int ChangedSlotCount)
{
    public bool PhysicalInventoryAndGraphQuantityVerified => true;
    public bool PersistedItemStateVerified => false;
    public bool ProducerProvenanceVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

internal static partial class CropSeedResourceBoundary
{
    // The original caller supplies the output/minimum. This observes stock, not a persisted-item or producer witness.
    internal static CropHarvestStockSummary VerifyHarvestStock(SnapshotEnvelope before, SnapshotEnvelope after,
        string qualifiedItemId, int minimumUnits)
    {
        Require(before.SaveId.Value == after.SaveId.Value && before.PlayerId.Value == after.PlayerId.Value &&
                before.GameVersion == after.GameVersion && after.GameTick > before.GameTick,
            "Harvest stock snapshot identity/tick changed.");
        var inventory = ExactInventoryReceiptVerifier.Verify(before, after, qualifiedItemId, minimumUnits, minimumQuality: 0);
        Require(inventory.Resolved && inventory.Verified, "Harvest retained stock is unavailable or insufficient: " +
            string.Join(",", inventory.BlockingReasons));
        var (first, next, priorPlayer, player) = ReadStockGraphs(before, after);
        Require(priorPlayer.Capacity is > 0 and <= 512 && player.Capacity == priorPlayer.Capacity,
            "Harvest player inventory capacity changed.");
        var changes = VerifyHarvestRawStock(before, after, qualifiedItemId, player.Capacity);
        Require(changes.Sum(row => (long)row.StackAfter - row.StackBefore) == inventory.QuantityIncrease,
            "Harvest slot deltas differ from the original retained-stock receipt.");
        VerifyHarvestStockGraph(before, after, first, next, priorPlayer, player, changes);
        VerifyCapacity(before, after, priorPlayer, player);
        foreach (var name in new[] { "money", "shop_currency_balances", "multiplayer_wallet" })
            SameField(before, after, "player", name, required: name != "multiplayer_wallet");
        var rows = Rows(Value(Field(after, "player", "inventory")!));
        if (changes.Any(row => rows[row.SlotIndex]["category"] is JsonValue category && category.TryGetValue<int>(out var value) && value == -74))
            VerifySeedInventory(before, after, priorPlayer, player, qualifiedItemId,
                rows[changes[0].SlotIndex]["item_id"]!.GetValue<string>());
        else SameField(before, after, "player", "seed_inventory", required: false);
        return new(inventory, changes.Count);
    }

    private static IReadOnlyList<PhysicalStockDelta> VerifyHarvestRawStock(SnapshotEnvelope before, SnapshotEnvelope after, string itemId, int capacity)
    {
        var expected = Field(before, "player", "inventory")!;
        var actual = Field(after, "player", "inventory")!;
        var first = Rows(Value(expected)); var next = Rows(Value(actual));
        Require(first.Keys.All(next.ContainsKey), "Harvest removed an original raw inventory slot.");
        var changes = new List<PhysicalStockDelta>();
        foreach (var (index, row) in next)
        {
            var hadRow = first.TryGetValue(index, out var prior);
            var wasEmpty = !hadRow || HarvestMirrorBoolean(prior!, "is_empty");
            var empty = HarvestMirrorBoolean(row, "is_empty");
            if (hadRow && JsonNode.DeepEquals(prior, row)) continue;
            Require(!empty && index < capacity && row["qualified_item_id"]?.GetValue<string>() == itemId,
                "Harvest changed an unrelated, empty or inactive inventory slot.");
            var oldCount = wasEmpty ? 0 : Int(prior!, "stack");
            var count = Int(row, "stack"); var quality = Int(row, "quality");
            Require(oldCount >= 0 && count > oldCount && quality >= 0 && count <= Int(row, "maximum_stack_size"),
                "Harvest retained stack is invalid or decreased.");
            if (!wasEmpty)
            {
                prior!["stack"] = count;
                Require(JsonNode.DeepEquals(prior, row), "Harvest changed existing inventory item metadata.");
            }
            changes.Add(new(index, itemId, quality, oldCount, count));
        }
        expected["value"] = Value(actual).DeepClone();
        EqualField(expected, actual, "harvest raw inventory envelope");
        return changes.AsReadOnly();
    }
}
