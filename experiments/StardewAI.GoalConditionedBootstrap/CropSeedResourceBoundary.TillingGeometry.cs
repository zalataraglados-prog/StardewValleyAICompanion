using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static SnapshotEnvelope ProjectVerifiedTillingTerrainAndStatistic(SnapshotEnvelope before, SnapshotEnvelope after,
        string location, MoneyRevenueCropPlantingTile tile)
    {
        var actor = NativeExecutionSourceSupport.Identity(before.PlayerId);
        Require(before.GameVersion == "1.6.15" && after.GameVersion == before.GameVersion &&
                NativeExecutionSourceSupport.Identity(after.PlayerId) == actor &&
                NativeExecutionSourceSupport.Identity(before.SaveId) == NativeExecutionSourceSupport.Identity(after.SaveId) &&
                location == "Farm" && tile.TileX >= 0 && tile.TileY >= 0 &&
                Value(Field(before, "player", "location_id")!).GetValue<string>() == location &&
                Value(Field(after, "player", "location_id")!).GetValue<string>() == location,
            "Crop till geometry changed its actor/save/Farm identity.");
        var first = Field(before, "farm", "terrain_features")!;
        var next = Field(after, "farm", "terrain_features")!;
        var prior = Array(Value(first), "till original terrain");
        var remaining = Array(Value(next), "till resulting terrain");
        foreach (var rows in new[] { prior, remaining })
            Require(rows.All(row => row is JsonObject) && rows.Select(row => (Int(row!, "tile_x"), Int(row!, "tile_y"))).Distinct().Count() == rows.Count,
                "Crop till terrain coordinates are duplicated or unreadable.");
        bool Target(JsonNode? row) => Int(row!, "tile_x") == tile.TileX && Int(row!, "tile_y") == tile.TileY;
        Require(!prior.Any(Target), "Crop till original target is already occupied.");
        var expectedAddition = new JsonObject { ["tile_x"] = tile.TileX, ["tile_y"] = tile.TileY,
            ["type"] = "StardewValley.TerrainFeatures.HoeDirt" };
        var addition = remaining.SingleOrDefault(Target);
        Require(addition is not null && JsonNode.DeepEquals(expectedAddition, addition),
            "Crop till target is not exactly one native HoeDirt terrain row.");
        remaining.Remove(addition);
        EqualExpectedResourceField("farm", "terrain_features", first, next, "Crop till");

        const string statField = "game_state_query_unlock_state";
        var expectedStats = Field(before, "world_progress", statField)!;
        var observedStats = Field(after, "world_progress", statField)!;
        var context = Value(expectedStats);
        Require(context["current_player_id"]?.GetValue<string>() == actor, "Crop till statistic actor context differs.");
        var players = Array(context["players"], "till statistic players");
        Require(players.All(row => row is JsonObject) && players.OfType<JsonObject>().Count(row =>
            row["is_current"] is JsonValue flag && flag.TryGetValue<bool>(out var current) && current) == 1,
            "Crop till statistic current actor is ambiguous.");
        var player = Unique(players, "player_id", actor);
        Require(player["is_current"]?.GetValue<bool>() == true && player["stats"] is JsonObject,
            "Crop till native actor statistic is unavailable.");
        var stats = player["stats"]!.AsObject();
        uint count = 0;
        Require(!stats.TryGetPropertyValue("dirtHoed", out var raw) || raw is JsonValue number && number.TryGetValue(out count),
            "Crop till original dirtHoed statistic has an invalid type.");
        Require(count < uint.MaxValue, "Crop till statistic overflow requires its exact native Set boundary.");
        stats["dirtHoed"] = count + 1u;
        EqualExpectedResourceField("world_progress", statField, expectedStats, observedStats, "Crop till");

        // Comparison-only view: energy, tools, inventory, debris and all other effects stay unnormalized.
        var state = new Dictionary<string, JsonElement>(after.State, StringComparer.Ordinal);
        foreach (var (domain, field) in new[] { ("farm", "terrain_features"), ("world_progress", statField) })
        {
            var fields = JsonNode.Parse(after.State[domain].GetRawText())!.AsObject();
            fields[field]!["value"] = Value(Field(before, domain, field)!).DeepClone();
            state[domain] = JsonSerializer.SerializeToElement(fields, JsonDefaults.Options);
        }
        return new SnapshotEnvelope { SaveId = after.SaveId, PlayerId = after.PlayerId, GameVersion = after.GameVersion, State = state };
    }
}
