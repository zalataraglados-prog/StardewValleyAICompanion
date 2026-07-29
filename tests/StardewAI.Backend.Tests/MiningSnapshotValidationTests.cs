using System.Text.Json;
using StardewAI.Contracts.State;
using Xunit;

namespace StardewAI.Backend.Tests;

public sealed class MiningSnapshotValidationTests
{
    [Fact]
    public void SnapshotValidatorAcceptsMiningSectionWithReadableOuterEnvelopesAndNestedGaps()
    {
        var raw = MiningSnapshotJson();

        var errors = SnapshotValidator.ValidateRaw(raw, out var snapshot);

        Assert.Empty(errors);
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.State.ContainsKey("mining"));
        var mining = snapshot.State["mining"];
        Assert.Equal("available", mining.GetProperty("tiles").GetProperty("status").GetString());
        Assert.Equal("available", mining.GetProperty("objects").GetProperty("status").GetString());
        Assert.Equal("available", mining.GetProperty("floor_objectives").GetProperty("status").GetString());
        var tilesValue = mining.GetProperty("tiles").GetProperty("value");
        Assert.Equal("unavailable", tilesValue.GetProperty("collision_context").GetProperty("status").GetString());
        var objectsValue = mining.GetProperty("objects").GetProperty("value");
        Assert.Equal("unavailable", objectsValue[0].GetProperty("is_ore_or_resource_node").GetProperty("status").GetString());
        var floorValue = mining.GetProperty("floor_objectives").GetProperty("value");
        Assert.Equal("unavailable", floorValue.GetProperty("ladder_creation_rule").GetProperty("status").GetString());

        Assert.DoesNotContain("mining.tiles", snapshot.UnavailableFields);
        Assert.DoesNotContain("mining.objects", snapshot.UnavailableFields);
        Assert.DoesNotContain("mining.floor_objectives", snapshot.UnavailableFields);
    }

    [Fact]
    public void SnapshotValidatorRejectsMiningSectionWithUnavailableOuterAndNonNullValue()
    {
        var raw = MiningSnapshotJson(badEnvelope: true);

        var errors = SnapshotValidator.ValidateRaw(raw, out _);

        Assert.Contains(errors, error => error.Contains("non-readable status must not carry a default value"));
    }

    [Fact]
    public void SnapshotValidatorAcceptsPurposeLimitedMiningProfile()
    {
        var raw = PurposeLimitedMiningSnapshotJson();

        var errors = SnapshotValidator.ValidateRaw(
            raw,
            out var snapshot,
            "mining");

        Assert.Empty(errors);
        Assert.NotNull(snapshot);
        Assert.Equal(
            MiningProfileDomains.OrderBy(value => value),
            snapshot!.State.Keys.OrderBy(value => value));
    }

    [Fact]
    public void SnapshotValidatorKeepsPurposeLimitedMiningProfileFailClosed()
    {
        var raw = PurposeLimitedMiningSnapshotJson(
            omittedDomain: "mining");

        var errors = SnapshotValidator.ValidateRaw(
            raw,
            out _,
            "mining");

        Assert.Contains("missing state domain: mining", errors);
    }

    [Fact]
    public void SnapshotValidatorRejectsUnknownPurposeProfile()
    {
        var errors = SnapshotValidator.ValidateRaw(
            PurposeLimitedMiningSnapshotJson(),
            out _,
            "anything");

        Assert.Contains("unsupported snapshot profile: anything", errors);
    }

    private static string PurposeLimitedMiningSnapshotJson(
        string? omittedDomain = null)
    {
        var state = MiningProfileDomains
            .Where(domain => domain != omittedDomain)
            .ToDictionary(
                domain => domain,
                _ => JsonSerializer.SerializeToElement(
                    new Dictionary<string, object>
                    {
                        ["marker"] = Field("available")
                    },
                    JsonOptions),
                StringComparer.Ordinal);
        var snapshot = new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            BridgeVersion = "test",
            GameTick = 812,
            RealTimestamp = "2026-07-30T00:00:00Z",
            Completeness = "complete",
            State = state
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    private static string MiningSnapshotJson(bool badEnvelope = false)
    {
        var stateObjects = RequiredDomains.ToDictionary(
            domain => domain,
            _ => (object)new Dictionary<string, object> { ["marker"] = Field("available") },
            StringComparer.Ordinal);
        stateObjects["mining"] = new Dictionary<string, object>
        {
            ["current_mine"] = Field(new
            {
                location_id = "UndergroundMine_100",
                mine_level = 100,
                mine_area = 0,
                mine_kind = "ordinary_mines",
                generated_identity = "UndergroundMine_100:100",
                is_loaded_current_location = true,
                is_skull_cavern = false,
                is_quarry_mine = false,
                is_dangerous = false,
                additional_difficulty = 0
            }),
            ["tiles"] = badEnvelope
                ? Unavailable("known_incomplete_aspect", new
                {
                    player_tile = new { tile_x = 10, tile_y = 20 },
                    collision_context = new { status = "unavailable", reason = "no side-effect-free complete passability projection" }
                })
                : Field(new
                {
                    player_tile = new { tile_x = 10, tile_y = 20 },
                    collision_context = new { status = "unavailable", reason = "no side-effect-free complete passability projection" }
                }),
            ["objects"] = badEnvelope
                ? Unavailable("known_incomplete_aspect", new object[]
                {
                    new
                    {
                        tile_x = 5,
                        tile_y = 10,
                        qualified_item_id = "(O)668",
                        is_ore_or_resource_node = new { status = "unavailable", reason = "no complete decompile-backed mine resource ID table" }
                    }
                })
                : Field(new object[]
                {
                    new
                    {
                        tile_x = 5,
                        tile_y = 10,
                        qualified_item_id = "(O)668",
                        is_ore_or_resource_node = new { status = "unavailable", reason = "no complete decompile-backed mine resource ID table" }
                    }
                }),
            ["monsters"] = Field(new object[]
            {
                new
                {
                    name = "Stone Golem",
                    tile_x = 15,
                    tile_y = 20,
                    health = 100,
                    max_health = 100,
                    is_monster = true
                }
            }),
            ["floor_objectives"] = badEnvelope
                ? Unavailable("known_incomplete_aspect", new
                {
                    must_kill_all_monsters_to_advance = false,
                    enemy_count = 0,
                    ladder_creation_rule = new { status = "unavailable", reason = "future ladder creation is executor/game progression" },
                    water_or_bridge_constraints = new { status = "unavailable", reason = "no non-mutating vanilla aggregate property" },
                    ladder_probability_preview = new { status = "unavailable", reason = "would require RNG/drop progression" }
                })
                : Field(new
                {
                    must_kill_all_monsters_to_advance = false,
                    enemy_count = 0,
                    ladder_creation_rule = new { status = "unavailable", reason = "future ladder creation is executor/game progression" },
                    water_or_bridge_constraints = new { status = "unavailable", reason = "no non-mutating vanilla aggregate property" },
                    ladder_probability_preview = new { status = "unavailable", reason = "would require RNG/drop progression" }
                }),
            ["player_resources"] = Field(new
            {
                health = 100,
                max_health = 100,
                energy = 270,
                max_energy = 270,
                mining_level = 10,
                combat_level = 8
            }),
            ["completeness"] = Field(new
            {
                status = "incomplete",
                source = "live_loaded_mineshaft_only",
                unavailable_reasons = new[] { "map_collision_passability_unavailable", "object_classification_incomplete", "floor_constraints_incomplete" }
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
            GameTick = 811,
            RealTimestamp = "2026-07-13T00:00:00Z",
            Completeness = "partial",
            UnavailableFields = badEnvelope
                ? new[] { "mining.tiles", "mining.objects", "mining.floor_objectives" }
                : new[]
                {
                    "mining.tiles.value.collision_context",
                    "mining.objects.value[*].is_ore_or_resource_node",
                    "mining.objects.value[*].is_container",
                    "mining.objects.value[*].health_or_hits_remaining",
                    "mining.floor_objectives.value.ladder_creation_rule",
                    "mining.floor_objectives.value.water_or_bridge_constraints",
                    "mining.floor_objectives.value.ladder_probability_preview"
                },
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
            read_at_tick = 811,
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
            read_at_tick = 811,
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

    private static readonly string[] MiningProfileDomains =
    {
        "environment",
        "identity",
        "time",
        "player",
        "options",
        "menus",
        "transport",
        "mining"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
