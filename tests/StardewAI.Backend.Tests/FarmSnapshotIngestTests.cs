using System.Text.Json;
using StardewAI.Contracts.State;
using Xunit;

namespace StardewAI.Backend.Tests
{
    public sealed class FarmSnapshotIngestTests
    {
        [Fact]
        public void SnapshotValidatorAcceptsFarmReadSliceEnvelopeFields()
        {
            var raw = SnapshotJson(FarmJson());

            var errors = SnapshotValidator.ValidateRaw(raw, out var snapshot);

            Assert.Empty(errors);
            Assert.NotNull(snapshot);
            Assert.True(snapshot!.State.ContainsKey("farm"));
        }

        [Fact]
        public void SnapshotValidatorRejectsUnavailableFarmFieldWithDefaultArray()
        {
            var farm = $$"""
            {
              "farm_type": {{UnavailableField("world_not_ready")}},
              "buildings": {{UnavailableField("world_not_ready", "[]")}}
            }
            """;

            var raw = SnapshotJson(farm);

            var errors = SnapshotValidator.ValidateRaw(raw, out _);

            Assert.Contains(errors, error => error.Contains("state.farm.buildings non-readable status must not carry a default value"));
        }

        private static string FarmJson()
        {
            return $$"""
            {
              "farm_type": {{Field(0)}},
              "farm_identity": {{Field("{\"location_name\":\"Farm\",\"location_id\":\"Farm\",\"is_farm\":true,\"greenhouse_unlocked\":false}", raw: true)}},
              "buildings": {{Field("[{\"type\":\"Farmhouse\",\"tile_x\":64,\"tile_y\":15,\"tiles_wide\":5,\"tiles_high\":9,\"days_of_construction_left\":0,\"is_under_construction\":false}]", raw: true)}},
              "crops": {{Field("[{\"tile_x\":10,\"tile_y\":11,\"harvest_item_id\":\"24\",\"current_phase\":1,\"phase_count\":5,\"day_of_current_phase\":0,\"dead\":false,\"forage_crop\":false,\"forage_crop_id\":\"\",\"fully_grown\":false,\"ready_for_harvest\":false,\"watered\":true,\"needs_watering\":false}]", raw: true)}},
              "terrain_features": {{Field("[{\"tile_x\":10,\"tile_y\":11,\"type\":\"StardewValley.TerrainFeatures.HoeDirt\"}]", raw: true)}},
              "objects": {{Field("[{\"tile_x\":12,\"tile_y\":11,\"item_id\":\"590\",\"qualified_item_id\":\"(O)590\",\"name\":\"Artifact Spot\",\"display_name\":\"Artifact Spot\",\"stack\":1,\"quality\":0,\"big_craftable\":false,\"ready_for_harvest\":false,\"minutes_until_ready\":0,\"held_item\":null}]", raw: true)}},
              "machines": {{Field("[]", raw: true)}},
              "chests": {{Field("[]", raw: true)}},
              "animals": {{Field("[]", raw: true)}},
              "resource_clumps": {{Field("[]", raw: true)}},
              "debris": {{Field("[]", raw: true)}},
              "warps": {{Field("[]", raw: true)}}
            }
            """;
        }

        private static string SnapshotJson(string farmJson)
        {
            var stateJson = $$"""
            {
              "environment": {
                "game_version": {{Field("1.6.15")}},
                "smapi_version": {{Field("4.5.2")}},
                "bridge_version": {{Field("test")}},
                "installed_mods": {{Field("[]", raw: true)}}
              },
              "identity": {
                "save_id": {{Field("Farm")}},
                "player_id": {{Field("123")}}
              },
              "time": {
                "season": {{Field("spring")}},
                "day": {{Field(1)}},
                "time": {{Field(610)}},
                "weather": {{Field("sun")}}
              },
              "player": {
                "location_id": {{Field("Farm")}},
                "tile_x": {{Field(64)}},
                "tile_y": {{Field(15)}},
                "facing_direction": {{Field(2)}},
                "money": {{Field(500)}},
                "health": {{Field(100)}},
                "max_health": {{Field(100)}},
                "energy": {{Field(270)}},
                "max_energy": {{Field(270)}},
                "current_tool": {{Field("(T)Axe")}},
                "active_menu": {{Field("none")}},
                "inventory": {{Field("[]", raw: true)}}
              },
              "mods": {
                "installed_count": {{Field(0)}},
                "installed_mods": {{Field("[]", raw: true)}}
              },
              "farm": {{farmJson}},
              "current_location": {
                "identity": {{Field("{\"name\":\"Farm\",\"name_or_unique_name\":\"Farm\",\"type\":\"StardewValley.Farm\"}", raw: true)}}
              },
              "npcs": {
                "positions": {{Field("[]", raw: true)}},
                "schedules": {{UnavailableField("npc_schedules_unavailable_without_complete_read_only_decompile_proof")}}
              },
              "quests": {
                "active_quests": {{Field("[]", raw: true)}},
                "completed_quests": {{UnavailableField("no_verified_global_completed_quest_collection_found")}}
              },
              "world_progress": {
                "community_center": {{Field("{\"bundles\":{},\"bundle_rewards\":{},\"completed_area_mail_flags\":[]}", raw: true)}},
                "perfection": {{UnavailableField("perfection_fields_not_verified_in_this_slice")}},
                "golden_walnuts": {{UnavailableField("golden_walnut_progress_not_verified_in_this_slice")}}
              }
            }
            """;

            var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson)!;
            var hash = SnapshotHash.ComputeStateHash(state);
            return $$"""
            {
              "schema_version": "snapshot.v1",
              "bridge_version": "test",
              "game_version": "1.6.15",
              "smapi_version": "4.5.2",
              "installed_mods": [],
              "save_id": {{Field("Farm")}},
              "player_id": {{Field("123")}},
              "game_tick": 100,
              "in_game_time": {{Field(610)}},
              "real_timestamp": "2026-07-04T00:00:00Z",
              "state_hash": "{{hash}}",
              "completeness": "partial",
              "unavailable_fields": [],
              "state": {{stateJson}}
            }
            """;
        }

        private static string Field(object? value, string status = "available", bool raw = false)
        {
            var valueJson = value is null
                ? "null"
                : raw
                    ? value.ToString()
                    : JsonSerializer.Serialize(value);
            var reason = status == "available" || status == "derived" ? string.Empty : @",""reason"":""value_unavailable""";
            return $$"""
            {
              "value": {{valueJson}},
              "status": "{{status}}",
              "source": { "kind": "{{(status == "available" ? "game_object" : "unavailable")}}", "path": "test" },
              "adapter": "test",
              "read_at_tick": 100,
              "confidence": {{(status == "available" ? "1.0" : "0.0")}}{{reason}}
            }
            """;
        }

        private static string Field(int value)
        {
            return Field((object)value);
        }

        private static string Field(string? value)
        {
            return Field((object?)value);
        }

        private static string UnavailableField(string reason, string valueJson = "null")
        {
            return $$"""
            {
              "value": {{valueJson}},
              "status": "unavailable",
              "source": { "kind": "unavailable", "path": "test" },
              "adapter": "test",
              "read_at_tick": 100,
              "confidence": 0.0,
              "reason": "{{reason}}"
            }
            """;
        }
    }
}
