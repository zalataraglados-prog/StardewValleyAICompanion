using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static class CropTeacherDebrisProtection
{
    public const string Reason = "protect_established_crop_layout_from_debris";
    public const string RequestSchema = "money_revenue_crop_site_preparation_plan_request.v3";

    public static bool HasLiveLayout(SnapshotEnvelope snapshot, IReadOnlyCollection<MoneyRevenueCropPlantingTile> layout, string seedId)
    {
        if (layout.Count == 0 || string.IsNullOrWhiteSpace(seedId)) return false;
        var live = Array(snapshot, "farm", "crops").EnumerateArray().Where(row =>
            Text(row, "location_id") == "Farm" &&
            row.TryGetProperty("dead", out var dead) && dead.ValueKind == JsonValueKind.False &&
            (Text(row, "harvest_source_seed_id") == seedId || "(O)" + Text(row, "harvest_source_seed_id") == seedId))
            .Select(Position).ToHashSet();
        return layout.All(live.Contains);
    }

    public static IReadOnlyDictionary<MoneyRevenueCropPlantingTile, string> ReadFrontier(
        SnapshotEnvelope snapshot, IReadOnlyCollection<MoneyRevenueCropPlantingTile> layout, bool boundedFarmView = false)
    {
        if (snapshot.GameVersion != "1.6.15")
            throw new InvalidDataException("Crop debris protection requires its original game version.");
        var footprint = layout.ToHashSet();
        var neighbours = new HashSet<MoneyRevenueCropPlantingTile>();
        foreach (var tile in footprint)
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                    neighbours.Add(new(tile.TileX + dx, tile.TileY + dy));
        neighbours.ExceptWith(footprint);

        var targets = new Dictionary<MoneyRevenueCropPlantingTile, string>();
        foreach (var row in Array(snapshot, boundedFarmView ? "current_location" : "farm", "objects").EnumerateArray())
        {
            var tile = Position(row);
            if (!neighbours.Contains(tile)) continue;
            var id = Text(row, "qualified_item_id");
            // Farm's vanilla spawned debris sources; crops and player-placed objects aren't clearance targets.
            if (id is "(O)25" or "(O)294" or "(O)295" or "(O)343" or "(O)450" or
                "(O)674" or "(O)675" or "(O)676" or "(O)677" or "(O)678" or "(O)679" or
                "(O)784" or "(O)785" or "(O)786" or "(O)792" or "(O)793" or "(O)794" or "(O)882" or "(O)883" or "(O)884")
            {
                if (!targets.TryAdd(tile, id))
                    throw new InvalidDataException("Crop debris protection found duplicate farm debris coordinates.");
            }
        }
        return targets;
    }

    private static MoneyRevenueCropPlantingTile Position(JsonElement row) =>
        new(row.GetProperty("tile_x").GetInt32(), row.GetProperty("tile_y").GetInt32());

    private static string? Text(JsonElement row, string key) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static JsonElement Array(SnapshotEnvelope snapshot, string domain, string member)
    {
        var value = NativeExecutionSourceSupport.Value(snapshot, domain, member);
        return value.ValueKind == JsonValueKind.Array ? value :
            throw new InvalidDataException($"Crop debris protection needs readable {domain}.{member}.");
    }
}
