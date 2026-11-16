using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TillingGeometryNormalizesOnlyVerifiedTerrainAndDirtHoedWithoutChangingNativeInputs(bool existingCounter)
    {
        var (before, after) = TillingGeometryFixture(existingCounter);
        var first = JsonSerializer.Serialize(before, JsonOptions); var next = JsonSerializer.Serialize(after, JsonOptions);
        ChangeState(after, "player", player => player["money"]!["value"] = 123);
        next = JsonSerializer.Serialize(after, JsonOptions);
        var view = ProjectTillingGeometry(before, after);
        Assert.Equal(first, JsonSerializer.Serialize(before, JsonOptions)); Assert.Equal(next, JsonSerializer.Serialize(after, JsonOptions));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(before.State["farm"].GetProperty("terrain_features").GetProperty("value").GetRawText()),
            JsonNode.Parse(view.State["farm"].GetProperty("terrain_features").GetProperty("value").GetRawText())));
        Assert.Equal(123, view.State["player"].GetProperty("money").GetProperty("value").GetInt32());
        Assert.True(string.IsNullOrEmpty(view.StateHash));
    }

    [Theory]
    [InlineData("before_target")]
    [InlineData("missing_target")]
    [InlineData("wrong_type")]
    [InlineData("unknown_target_field")]
    [InlineData("duplicate_before")]
    [InlineData("duplicate_after")]
    [InlineData("other_terrain")]
    [InlineData("actor")]
    [InlineData("location")]
    [InlineData("actor_context")]
    [InlineData("duplicate_current")]
    [InlineData("missing_stat")]
    [InlineData("extra_stat")]
    [InlineData("foreign_stat")]
    [InlineData("stat_type")]
    [InlineData("original_stat_type")]
    [InlineData("overflow")]
    public void TillingGeometryRejectsUnknownTerrainOrUnprovenStatisticEffects(string change)
    {
        var (before, after) = TillingGeometryFixture();
        void Terrain(SnapshotEnvelope snapshot, Action<JsonArray> mutate) => ChangeState(snapshot, "farm", farm => mutate(farm["terrain_features"]!["value"]!.AsArray()));
        void Stats(SnapshotEnvelope snapshot, Action<JsonNode> mutate) => ChangeState(snapshot, "world_progress", progress => mutate(progress["game_state_query_unlock_state"]!["value"]!));
        switch (change)
        {
            case "before_target": Terrain(before, rows => rows.Add(new JsonObject { ["tile_x"] = 1, ["tile_y"] = 1, ["type"] = "other" })); break;
            case "missing_target": Terrain(after, rows => rows.RemoveAt(1)); break;
            case "wrong_type": Terrain(after, rows => rows[1]!["type"] = "StardewValley.TerrainFeatures.Tree"); break;
            case "unknown_target_field": Terrain(after, rows => rows[1]!["unknown_persistent"] = 1); break;
            case "duplicate_before": Terrain(before, rows => rows.Add(rows[0]!.DeepClone())); break;
            case "duplicate_after": Terrain(after, rows => rows.Add(rows[1]!.DeepClone())); break;
            case "other_terrain": Terrain(after, rows => rows[0]!["type"] = "changed"); break;
            case "actor": after.PlayerId.Value += ".foreign"; break;
            case "location": ChangeState(after, "player", player => player["location_id"]!["value"] = "Town"); break;
            case "actor_context": Stats(before, value => value["current_player_id"] = "foreign"); break;
            case "duplicate_current": Stats(before, value => value["players"]![1]!["is_current"] = true); break;
            case "missing_stat": Stats(after, value => value["players"]![0]!["stats"]!.AsObject().Remove("dirtHoed")); break;
            case "extra_stat": Stats(after, value => value["players"]![0]!["stats"]!["dirtHoed"] = 2); break;
            case "foreign_stat": Stats(after, value => value["players"]![1]!["stats"]!["dirtHoed"] = 2); break;
            case "stat_type": Stats(after, value => value["players"]![0]!["stats"]!["dirtHoed"] = "1"); break;
            case "original_stat_type": Stats(before, value => value["players"]![0]!["stats"]!["dirtHoed"] = -1); break;
            case "overflow": Stats(before, value => value["players"]![0]!["stats"]!["dirtHoed"] = uint.MaxValue); break;
        }
        Assert.Throws<InvalidDataException>(() => ProjectTillingGeometry(before, after));
    }

    private static SnapshotEnvelope ProjectTillingGeometry(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        var method = typeof(CropTeacherSupportingRoute).Assembly.GetType("StardewAI.GoalConditionedBootstrap.CropSeedResourceBoundary")!
            .GetMethod("ProjectVerifiedTillingTerrainAndStatistic", BindingFlags.NonPublic | BindingFlags.Static)!;
        try { return (SnapshotEnvelope)method.Invoke(null, new object[] { before, after, "Farm", new MoneyRevenueCropPlantingTile(1, 1) })!; }
        catch (TargetInvocationException error) when (error.InnerException is InvalidDataException invalid) { throw invalid; }
    }

    private static (SnapshotEnvelope Before, SnapshotEnvelope After) TillingGeometryFixture(bool existingCounter = false)
    {
        var before = new SnapshotEnvelope { GameVersion = "1.6.15", SaveId = new() { Status = "available", Value = "save.till" },
            PlayerId = new() { Status = "available", Value = "actor.till" } };
        before.State["player"] = JsonSerializer.SerializeToElement(new { location_id = new { status = "available", value = "Farm" },
            money = new { status = "available", value = 500 } });
        before.State["farm"] = JsonSerializer.SerializeToElement(new { terrain_features = new { status = "available", value = new[]
            { new { tile_x = 4, tile_y = 4, type = "StardewValley.TerrainFeatures.Tree" } } } });
        var stats = new JsonObject { ["stepsTaken"] = 5 }; if (existingCounter) stats["dirtHoed"] = 7;
        before.State["world_progress"] = JsonSerializer.SerializeToElement(new { game_state_query_unlock_state = new
        {
            status = "available", value = new { current_player_id = before.PlayerId.Value, players = new[]
            { new { player_id = before.PlayerId.Value, is_current = true, stats },
                new { player_id = "foreign", is_current = false, stats = new JsonObject { ["dirtHoed"] = 1 } } } }
        } });
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
        ChangeState(after, "farm", farm => farm["terrain_features"]!["value"]!.AsArray().Add(new JsonObject
            { ["tile_x"] = 1, ["tile_y"] = 1, ["type"] = "StardewValley.TerrainFeatures.HoeDirt" }));
        ChangeState(after, "world_progress", progress =>
        {
            var actor = progress["game_state_query_unlock_state"]!["value"]!["players"]![0]!;
            actor["stats"]!["dirtHoed"] = existingCounter ? 8 : 1; actor["stats"]!["stepsTaken"] = 8;
        });
        return (before, after);
    }
}
