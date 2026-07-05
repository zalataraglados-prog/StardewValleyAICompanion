using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.WorldModel;

namespace StardewAI.Core.Tests;

public sealed class WorldModelProjectorTests
{
    [Fact]
    public void ProjectBuildsTypedWorldModelFromReadableSnapshot()
    {
        var snapshot = Snapshot("""
        {
          "identity": {
            "save_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"Game1.player.farmName"},"adapter":"test","read_at_tick":10,"confidence":1},
            "player_id": {"value":"123","status":"available","source":{"kind":"game_object","path":"Game1.player.UniqueMultiplayerID"},"adapter":"test","read_at_tick":10,"confidence":1}
          },
          "time": {
            "season": {"value":"fall","status":"available","source":{"kind":"game_object","path":"Game1.currentSeason"},"adapter":"test","read_at_tick":10,"confidence":1},
            "day": {"value":26,"status":"available","source":{"kind":"game_object","path":"Game1.dayOfMonth"},"adapter":"test","read_at_tick":10,"confidence":1},
            "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"Game1.timeOfDay"},"adapter":"test","read_at_tick":10,"confidence":1},
            "weather": {"value":"sun","status":"available","source":{"kind":"game_object","path":"Game1.weather"},"adapter":"test","read_at_tick":10,"confidence":1}
          },
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"Game1.player.currentLocation"},"adapter":"test","read_at_tick":10,"confidence":1},
            "money": {"value":144749,"status":"available","source":{"kind":"game_object","path":"Game1.player.Money"},"adapter":"test","read_at_tick":10,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"Game1.player.Stamina"},"adapter":"test","read_at_tick":10,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"Game1.player.Items"},"adapter":"test","read_at_tick":10,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"farm.crops"},"adapter":"test","read_at_tick":10,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"Game1.activeClickableMenu"},"adapter":"test","read_at_tick":10,"confidence":1}
          },
          "transport": {
            "event_stream_websocket": {"value":{"endpoint":"ws://127.0.0.1:8766/api/v1/events/ws"},"status":"available","source":{"kind":"game_object","path":"ws"},"adapter":"test","read_at_tick":10,"confidence":1}
          },
          "npcs": {},
          "quests": {},
          "world_progress": {},
          "current_location": {},
          "mods": {},
          "modded_state": {}
        }
        """);

        var model = new WorldModelProjector().Project(snapshot, "water crops", "efficiency");

        Assert.Equal("world_model.v1", model.SchemaVersion);
        Assert.Equal("water crops", model.UserGoal);
        Assert.Equal("efficiency", model.Mode);
        Assert.True(model.Completeness.AllRequiredFactsReadable);
        Assert.False(model.PlannerInputs.Blocked);
        Assert.Equal("FarmHouse", model.PlayerString("location_id"));
        Assert.Equal(600, model.GameInt("time"));
        Assert.Equal("Farm", model.GameString("save_id"));
    }

    [Fact]
    public void ProjectBlocksWhenRequiredFactsAreUnreadable()
    {
        var snapshot = Snapshot("""
        {
          "identity": {
            "save_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"Game1.player.farmName"},"adapter":"test","read_at_tick":10,"confidence":1}
          },
          "time": {},
          "player": {},
          "farm": {},
          "menus": {},
          "transport": {}
        }
        """, unavailableFields: new[] { "player.location_id" });

        var model = new WorldModelProjector().Project(snapshot, "water crops", "relaxed");

        Assert.False(model.Completeness.AllRequiredFactsReadable);
        Assert.True(model.PlannerInputs.Blocked);
        Assert.Contains("snapshot_has_unavailable_fields", model.PlannerInputs.BlockReasons);
        Assert.Contains(model.PlannerInputs.RequiredFacts, fact => fact.Path == "player.location_id" && fact.Status == "missing");
    }

    [Fact]
    public void ProjectDoesNotCopyUnreadableEnvelopeValuesIntoFacts()
    {
        var snapshot = Snapshot("""
        {
          "identity": {
            "save_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"Game1.player.farmName"},"adapter":"test","read_at_tick":10,"confidence":1},
            "player_id": {"value":"123","status":"available","source":{"kind":"game_object","path":"Game1.player.UniqueMultiplayerID"},"adapter":"test","read_at_tick":10,"confidence":1}
          },
          "time": {
            "season": {"value":"fall","status":"available","source":{"kind":"game_object","path":"Game1.currentSeason"},"adapter":"test","read_at_tick":10,"confidence":1},
            "day": {"value":26,"status":"available","source":{"kind":"game_object","path":"Game1.dayOfMonth"},"adapter":"test","read_at_tick":10,"confidence":1},
            "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"Game1.timeOfDay"},"adapter":"test","read_at_tick":10,"confidence":1},
            "weather": {"value":"sun","status":"available","source":{"kind":"game_object","path":"Game1.weather"},"adapter":"test","read_at_tick":10,"confidence":1}
          },
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"Game1.player.currentLocation"},"adapter":"test","read_at_tick":10,"confidence":1},
            "money": {"value":144749,"status":"available","source":{"kind":"game_object","path":"Game1.player.Money"},"adapter":"test","read_at_tick":10,"confidence":1},
            "energy": {"value":270,"status":"unavailable","source":{"kind":"unavailable","path":"Game1.player.Stamina"},"adapter":"test","read_at_tick":10,"confidence":0,"reason":"test_unavailable"},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"Game1.player.Items"},"adapter":"test","read_at_tick":10,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"farm.crops"},"adapter":"test","read_at_tick":10,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"Game1.activeClickableMenu"},"adapter":"test","read_at_tick":10,"confidence":1}
          },
          "transport": {
            "event_stream_websocket": {"value":{"endpoint":"ws://127.0.0.1:8766/api/v1/events/ws"},"status":"available","source":{"kind":"game_object","path":"ws"},"adapter":"test","read_at_tick":10,"confidence":1}
          }
        }
        """, unavailableFields: new[] { "player.energy" });

        var model = new WorldModelProjector().Project(snapshot, "water crops", "relaxed");

        Assert.False(model.Facts.Player.ContainsKey("energy"));
        Assert.True(model.PlannerInputs.Blocked);
        Assert.Contains(model.PlannerInputs.RequiredFacts, fact => fact.Path == "player.energy" && fact.Status == "unavailable");
    }

    private static SnapshotEnvelope Snapshot(string stateJson, string[]? unavailableFields = null)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 10,
            RealTimestamp = "2026-07-05T00:00:00Z",
            Completeness = "complete",
            UnavailableFields = unavailableFields ?? Array.Empty<string>(),
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

internal static class WorldModelTestExtensions
{
    public static string? PlayerString(this StardewAI.Contracts.WorldModel.WorldModelEnvelope model, string key)
    {
        return model.Facts.Player[key].GetString();
    }

    public static string? GameString(this StardewAI.Contracts.WorldModel.WorldModelEnvelope model, string key)
    {
        return model.Facts.Game[key].GetString();
    }

    public static int GameInt(this StardewAI.Contracts.WorldModel.WorldModelEnvelope model, string key)
    {
        return model.Facts.Game[key].GetInt32();
    }
}
