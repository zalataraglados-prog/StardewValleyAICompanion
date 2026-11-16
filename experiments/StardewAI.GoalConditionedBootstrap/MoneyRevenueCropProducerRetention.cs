using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MoneyRevenueCropRetainedVariant(
    [property: JsonPropertyName("inventory_identity_sha256")] string InventoryIdentitySha256,
    [property: JsonPropertyName("retained_quantity")] int RetainedQuantity);

public sealed record MoneyRevenueCropRetentionSegment(
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("owned_harvest")] bool OwnedHarvest,
    [property: JsonPropertyName("observed_boundary_count")] int ObservedBoundaryCount,
    [property: JsonPropertyName("inventory_increase")] int InventoryIncrease,
    [property: JsonPropertyName("retained_quantity_after")] int RetainedQuantityAfter);

public sealed record MoneyRevenueCropProducerRetention(
    [property: JsonPropertyName("retained_quantity")] int RetainedQuantity,
    [property: JsonPropertyName("variants")] MoneyRevenueCropRetainedVariant[] Variants,
    [property: JsonPropertyName("segments")] MoneyRevenueCropRetentionSegment[] Segments);

// Fungible stock accounting, not a second native simulator or authority to consume reservations.
public sealed class MoneyRevenueCropRetentionAccount
{
    private static readonly Dictionary<string, int> EmptyStorage = new(StringComparer.Ordinal);
    private readonly string itemId;
    private Dictionary<string, int> previous;
    private Dictionary<string, int> stored = EmptyStorage;
    private readonly Dictionary<string, int> credit = new(StringComparer.Ordinal);
    private string stateHash;
    private readonly string saveId, playerId, gameVersion;
    private long tick;

    internal MoneyRevenueCropRetentionAccount(SnapshotEnvelope before, string itemId)
    {
        this.itemId = itemId;
        previous = ReadInventory(before, itemId);
        stateHash = before.StateHash; tick = before.GameTick;
        saveId = before.SaveId.Value!; playerId = before.PlayerId.Value!; gameVersion = before.GameVersion;
    }

    internal void Observe(SnapshotEnvelope before, SnapshotEnvelope after, bool ownedHarvest)
        => ObserveCore(before, after, ownedHarvest, null);

    internal void ObserveReservedForeign(SnapshotEnvelope before, SnapshotEnvelope after,
        MoneyRevenueCropRetainedVariant[] preserved) => ObserveCore(before, after, false, preserved);

    internal void ObserveTransferredStock(SnapshotEnvelope before, SnapshotEnvelope after, bool ownedHarvest,
        CropTeacherSaleStockObservation stockBefore, CropTeacherSaleStockObservation stockAfter) =>
        ObserveCore(before, after, ownedHarvest, ownedHarvest ? null : stockAfter.Preserved,
            stockBefore.Stored, stockAfter.Stored);

    private void ObserveCore(SnapshotEnvelope before, SnapshotEnvelope after, bool ownedHarvest,
        MoneyRevenueCropRetainedVariant[]? preserved, MoneyRevenueCropRetainedVariant[]? storedBefore = null,
        MoneyRevenueCropRetainedVariant[]? storedAfter = null)
    {
        Require(before.StateHash == stateHash && before.GameTick == tick &&
            before.StateHash == SnapshotHash.ComputeStateHash(before.State) &&
            after.StateHash == SnapshotHash.ComputeStateHash(after.State) && after.GameTick > tick &&
            new[] { before, after }.All(row => row.SaveId.Value == saveId && row.PlayerId.Value == playerId && row.GameVersion == gameVersion),
            "source identity/state/tick is discontinuous.");
        var current = ReadInventory(before, itemId);
        Require(current.Count == previous.Count && current.All(pair => previous.GetValueOrDefault(pair.Key) == pair.Value),
            "source inventory is discontinuous.");
        var next = ReadInventory(after, itemId);
        var beforeStorage = ReadStored(storedBefore); var nextStorage = ReadStored(storedAfter);
        Require(beforeStorage.Count == stored.Count && beforeStorage.All(row => stored.GetValueOrDefault(row.Key) == row.Value),
            "stored stock is discontinuous; an unobserved transfer cannot create owned credit.");
        var currentStock = Combine(current, beforeStorage); var nextStock = Combine(next, nextStorage);
        Require(preserved is null || preserved.All(row => row is not null && !string.IsNullOrWhiteSpace(row.InventoryIdentitySha256)) &&
            preserved.Select(row => row.InventoryIdentitySha256).Distinct(StringComparer.Ordinal).Count() == preserved.Length,
            "reserved stock floors are missing or duplicated.");
        var floors = preserved?.ToDictionary(row => row.InventoryIdentitySha256, row => row.RetainedQuantity, StringComparer.Ordinal);
        Require(floors is null || floors.All(row => row.Value > 0 && row.Value <= credit.GetValueOrDefault(row.Key) &&
            row.Value <= currentStock.GetValueOrDefault(row.Key) && row.Value <= nextStock.GetValueOrDefault(row.Key)),
            "reserved stock cannot create credit or replace missing owned variants.");
        Require(nextStorage.All(row => row.Value <= credit.GetValueOrDefault(row.Key)),
            "stored stock cannot introduce an uncredited owned variant.");
        foreach (var key in currentStock.Keys.Concat(nextStock.Keys).Distinct(StringComparer.Ordinal))
        {
            var delta = checked(nextStock.GetValueOrDefault(key) - currentStock.GetValueOrDefault(key));
            if (ownedHarvest)
            {
                Require(delta >= 0, "owned harvest removed or replaced existing item variants.");
                credit[key] = checked(credit.GetValueOrDefault(key) + delta);
            }
            else if (delta < 0) credit[key] = Math.Max(floors?.GetValueOrDefault(key) ?? 0,
                Math.Max(0, checked(credit.GetValueOrDefault(key) + delta)));
        }
        previous = next; stored = nextStorage; stateHash = after.StateHash; tick = after.GameTick;
    }

    private static Dictionary<string, int> ReadStored(MoneyRevenueCropRetainedVariant[]? rows)
    {
        Require(rows is null || rows.All(row => row is not null && row.RetainedQuantity > 0 &&
            !string.IsNullOrWhiteSpace(row.InventoryIdentitySha256)) &&
            rows.Select(row => row.InventoryIdentitySha256).Distinct(StringComparer.Ordinal).Count() == rows.Length,
            "stored stock variants are missing or duplicated.");
        return rows is { Length: > 0 }
            ? rows.ToDictionary(row => row.InventoryIdentitySha256, row => row.RetainedQuantity, StringComparer.Ordinal)
            : EmptyStorage;
    }

    private static Dictionary<string, int> Combine(Dictionary<string, int> inventory, Dictionary<string, int> storage)
    {
        if (storage.Count == 0) return inventory;
        var result = new Dictionary<string, int>(inventory, StringComparer.Ordinal);
        foreach (var row in storage) result[row.Key] = checked(result.GetValueOrDefault(row.Key) + row.Value);
        return result;
    }

    internal int Quantity => checked(credit.Values.Sum());
    internal MoneyRevenueCropRetainedVariant[] Variants => credit.Where(row => row.Value > 0)
        .OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => new MoneyRevenueCropRetainedVariant(row.Key, row.Value)).ToArray();

    // PlayerReadAdapter projects price and sell protection from the current player/location.
    // Those remain terminal gates, not item identity. Unknown item metadata stays in the identity.
    public static string InventoryIdentity(JsonElement row)
    {
        var identity = JsonNode.Parse(row.GetRawText())!.AsObject();
        foreach (var field in new[] { "slot_index", "stack", "is_empty", "sell_to_store_price", "sale_price",
            "protected_from_auto_sell", "auto_sell_protection_reasons" }) identity.Remove(field);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            SnapshotHash.CanonicalizeSemantic(JsonSerializer.SerializeToElement(identity))))).ToLowerInvariant();
    }

    private static Dictionary<string, int> ReadInventory(SnapshotEnvelope snapshot, string itemId)
    {
        Require(ExactInventoryReceiptVerifier.InventoryCount(snapshot, itemId, 0) is not null,
            "inventory count is unavailable.");
        var rows = snapshot.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray().ToArray();
        var slots = new HashSet<int>(); var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            Require(row.ValueKind == JsonValueKind.Object && row.TryGetProperty("slot_index", out var slot) &&
                slot.TryGetInt32(out var index) && index >= 0 && slots.Add(index), "inventory slots are invalid or duplicated.");
            if (!row.TryGetProperty("qualified_item_id", out var id)) continue;
            if (id.ValueKind == JsonValueKind.Null)
            {
                Require(row.TryGetProperty("is_empty", out var marker) && marker.ValueKind == JsonValueKind.True &&
                    (!row.TryGetProperty("stack", out var count) || count.ValueKind == JsonValueKind.Null ||
                     count.TryGetInt32(out var number) && number == 0), "empty inventory slot is malformed.");
                continue;
            }
            Require(id.ValueKind == JsonValueKind.String, "inventory item identity is malformed.");
            if (id.GetString() != itemId) continue;
            Require(!row.TryGetProperty("is_empty", out var empty) || empty.ValueKind == JsonValueKind.False,
                "matching inventory slot is empty or malformed.");
            var key = InventoryIdentity(row);
            result[key] = checked(result.GetValueOrDefault(key) + row.GetProperty("stack").GetInt32());
        }
        return result;
    }

    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidDataException("Crop producer retention: " + reason); }
}
