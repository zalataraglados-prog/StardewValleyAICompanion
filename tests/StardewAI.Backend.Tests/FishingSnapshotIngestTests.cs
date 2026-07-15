using System.Text.Json;
using StardewAI.Contracts.State;
using Xunit;

namespace StardewAI.Backend.Tests;

public sealed class FishingSnapshotIngestTests
{
    [Fact]
    public void SnapshotValidatorAcceptsFishingReadinessSlice()
    {
        var raw = SnapshotJson(unavailableSpawnRulesCarryDefault: false);

        var errors = SnapshotValidator.ValidateRaw(raw, out var snapshot);

        Assert.Empty(errors);
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.State.ContainsKey("fishing"));
        var fishing = snapshot.State["fishing"];
        Assert.Equal(2, fishing.GetProperty("fishable_tiles").GetProperty("value").GetArrayLength());
        Assert.Equal("available", fishing.GetProperty("spawn_rules").GetProperty("status").GetString());
        Assert.Equal(2, fishing.GetProperty("spawn_rules").GetProperty("value").GetProperty("combined_rule_count").GetInt32());
    }

    [Fact]
    public void SnapshotValidatorRejectsUnavailableFishingRulesWithDefaultArray()
    {
        var raw = SnapshotJson(unavailableSpawnRulesCarryDefault: true);

        var errors = SnapshotValidator.ValidateRaw(raw, out _);

        Assert.Contains(errors, error => error.Contains("state.fishing.spawn_rules non-readable status must not carry a default value"));
    }

    private static string SnapshotJson(bool unavailableSpawnRulesCarryDefault)
    {
        var stateObjects = RequiredDomains.ToDictionary(
            domain => domain,
            _ => (object)new Dictionary<string, object> { ["marker"] = Field("available") },
            StringComparer.Ordinal);
        stateObjects["fishing"] = new Dictionary<string, object>
        {
            ["location_context"] = Field(new
            {
                location_id = "Beach",
                can_fish_here = true,
                map_width = 80,
                map_height = 50,
                fishable_tile_count = 2,
                fishing_level = 3,
                scan_policy = "complete_current_map_no_cap"
            }),
            ["fishable_tiles"] = Field(new[]
            {
                new { tile_x = 10, tile_y = 20, water_depth = 2, fish_area_id = "ocean" },
                new { tile_x = 11, tile_y = 20, water_depth = 3, fish_area_id = "ocean" }
            }),
            ["rod_inventory"] = Field(new[]
            {
                new { slot_index = 0, qualified_item_id = "(T)FiberglassRod", can_use_bait = true, can_use_tackle = false }
            }),
            ["active_cast_state"] = Field(new { rod_selected = true, is_fishing = false }),
            ["spawn_rules"] = unavailableSpawnRulesCarryDefault
                ? Unavailable("test_unavailable", Array.Empty<object>())
                : Field(new
                {
                    location_id = "Beach",
                    combined_rule_count = 2,
                    inventory_complete = true,
                    random_policy = new
                    {
                        consumes_game_rng = false,
                        chance_rolls_executed = false
                    },
                    item_query_resolution_complete = true,
                    unresolved_rule_keys = Array.Empty<string>(),
                    rules = new[]
                    {
                        new { rule_key = "Default#0", eligible_before_random_rolls = true },
                        new { rule_key = "Beach#0", eligible_before_random_rolls = false }
                    }
                })
        };

        var state = stateObjects.ToDictionary(
            pair => pair.Key,
            pair => JsonSerializer.SerializeToElement(pair.Value, JsonOptions),
            StringComparer.Ordinal);
        var snapshot = new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            BridgeVersion = "test",
            GameVersion = "1.6.15",
            SmapiVersion = "4.5.2",
            GameTick = 100,
            RealTimestamp = "2026-07-13T00:00:00Z",
            Completeness = "partial",
            UnavailableFields = unavailableSpawnRulesCarryDefault
                ? new[] { "fishing.spawn_rules" }
                : Array.Empty<string>(),
            State = state
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    private static object Field(object value)
    {
        return new
        {
            value,
            status = "available",
            source = new { kind = "game_object", path = "test" },
            adapter = "test",
            read_at_tick = 100,
            confidence = 1.0
        };
    }

    private static object Unavailable(string reason, object? value)
    {
        return new
        {
            value,
            status = "unavailable",
            source = new { kind = "unavailable", path = "test" },
            adapter = "test",
            read_at_tick = 100,
            confidence = 0.0,
            reason
        };
    }

    private static readonly string[] RequiredDomains =
    {
        "environment",
        "identity",
        "time",
        "player",
        "mods",
        "farm",
        "current_location",
        "npcs",
        "quests",
        "world_progress",
        "menus",
        "modded_state"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
