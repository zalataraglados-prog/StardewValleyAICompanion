using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static class CropSeedPlantingGrowthPreflight
{
    internal static void Validate(SnapshotEnvelope snapshot, MoneyRevenueCropNativeProductionActionBatch batch,
        int inventorySlot, string seedId, int baseGrowDays, int? onlyIndex = null)
    {
        Require(snapshot.State.TryGetValue("current_location", out var location) && location.ValueKind == JsonValueKind.Object &&
                location.TryGetProperty("planting_context", out var envelope) && envelope.ValueKind == JsonValueKind.Object &&
                envelope.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String &&
                FieldEnvelopeValidator.IsReadableStatus(status.GetString()) && envelope.TryGetProperty("value", out var context) &&
                context.ValueKind == JsonValueKind.Object && Text(context, "location_id") == batch.LocationId &&
                context.TryGetProperty("hoe_dirt_tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array,
            "Owned planting live growth context is unavailable.");
        var rows = location.GetProperty("planting_context").GetProperty("value").GetProperty("hoe_dirt_tiles");
        foreach (var tile in onlyIndex.HasValue ? new[] { batch.Tiles[onlyIndex.Value] } : batch.Tiles)
        {
            var targets = rows.EnumerateArray().Where(row => Integer(row, "tile_x") == tile.TileX && Integer(row, "tile_y") == tile.TileY).ToArray();
            Require(targets.Length == 1 && targets[0].TryGetProperty("has_crop", out var planted) && planted.ValueKind == JsonValueKind.False &&
                    targets[0].TryGetProperty("seed_results", out var seeds) && seeds.ValueKind == JsonValueKind.Array,
                "Owned planting target is not uniquely unplanted in live context.");
            var candidates = targets[0].GetProperty("seed_results").EnumerateArray().Where(row =>
                Text(row, "seed_id") == seedId && Integer(row, "slot_index") == inventorySlot).ToArray();
            Require(candidates.Length == 1 && candidates[0].TryGetProperty("hard_rule_allows_planting", out var allowed) &&
                    allowed.ValueKind == JsonValueKind.True && Integer(candidates[0], "base_grow_days") == baseGrowDays &&
                    Zero(candidates[0], "speed_boost_without_paddy") && Zero(candidates[0], "speed_boost_with_paddy") &&
                    Integer(candidates[0], "adjusted_grow_days_with_paddy_if_eligible") == baseGrowDays,
                "Owned planting live growth/rules differ from the original unboosted profile.");
        }
    }

    private static string? Text(JsonElement row, string name) => row.ValueKind == JsonValueKind.Object &&
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static int? Integer(JsonElement row, string name) => row.ValueKind == JsonValueKind.Object &&
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
    private static bool Zero(JsonElement row, string name) => row.ValueKind == JsonValueKind.Object &&
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && number == 0d;
}
