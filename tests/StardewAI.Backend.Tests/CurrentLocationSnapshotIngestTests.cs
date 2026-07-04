using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StardewAI.Contracts.State;
using Xunit;

namespace StardewAI.Backend.Tests
{
    public sealed class CurrentLocationSnapshotIngestTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> factory;

        public CurrentLocationSnapshotIngestTests(WebApplicationFactory<Program> factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task SnapshotIngestAcceptsCurrentLocationReadSlice()
        {
            using var client = factory.CreateClient();

            var response = await client.PostAsync("/api/v1/snapshots", SnapshotWithCurrentLocation());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SnapshotIngestRejectsUnavailableCurrentLocationDefault()
        {
            using var client = factory.CreateClient();

            var response = await client.PostAsync("/api/v1/snapshots", SnapshotWithCurrentLocation(unavailableObjectsCarryDefault: true));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("state.current_location.objects non-readable status must not carry a default value", await response.Content.ReadAsStringAsync());
        }

        private static StringContent SnapshotWithCurrentLocation(bool unavailableObjectsCarryDefault = false)
        {
            var objectStatus = unavailableObjectsCarryDefault ? "unavailable" : "available";
            var stateJson = $$"""
            {
              "environment": {
                "game_version": {{FieldJson("1.6.15")}},
                "smapi_version": {{FieldJson("4.5.2")}},
                "bridge_version": {{FieldJson("test")}},
                "installed_mods": {{FieldJson("[]", raw: true)}}
              },
              "identity": {
                "save_id": {{FieldJson("Farm")}},
                "player_id": {{FieldJson("123")}}
              },
              "time": {
                "season": {{FieldJson("spring")}},
                "day": {{FieldJson(1)}},
                "time": {{FieldJson(610)}},
                "weather": {{FieldJson("sun")}}
              },
              "player": {
                "location_id": {{FieldJson("Farm")}},
                "tile_x": {{FieldJson(64)}},
                "tile_y": {{FieldJson(15)}},
                "facing_direction": {{FieldJson(2)}},
                "money": {{FieldJson(500)}},
                "health": {{FieldJson(100)}},
                "max_health": {{FieldJson(100)}},
                "energy": {{FieldJson(270)}},
                "max_energy": {{FieldJson(270)}},
                "current_tool": {{FieldJson("(T)Axe")}},
                "active_menu": {{FieldJson("none")}},
                "inventory": {{FieldJson("[]", raw: true)}}
              },
              "mods": {
                "installed_count": {{FieldJson(0)}},
                "installed_mods": {{FieldJson("[]", raw: true)}}
              },
              "farm": {
                "crops": {{FieldJson("[]", raw: true)}}
              },
              "current_location": {
                "identity": {{FieldJson("{\"name\":\"Farm\",\"name_or_unique_name\":\"Farm\",\"type\":\"StardewValley.Farm\"}", raw: true)}},
                "display_name": {{FieldJson("Farm")}},
                "flags": {{FieldJson("{\"is_outdoors\":true,\"is_farm\":true}", raw: true)}},
                "objects": {{FieldJson("[{\"tile_x\":64,\"tile_y\":15,\"item_id\":\"Chest\",\"qualified_item_id\":\"(BC)Chest\",\"name\":\"Chest\",\"display_name\":\"Chest\",\"stack\":1,\"quality\":0,\"type\":\"StardewValley.Objects.Chest\"}]", status: objectStatus, raw: true)}},
                "terrain_features": {{FieldJson("[{\"tile_x\":10,\"tile_y\":20,\"type\":\"StardewValley.TerrainFeatures.Tree\"}]", raw: true)}},
                "warps": {{FieldJson("[{\"x\":64,\"y\":15,\"target_name\":\"FarmHouse\",\"target_x\":3,\"target_y\":8,\"flip_farmer\":false,\"npc_only\":false}]", raw: true)}},
                "map": {{FieldJson("{\"id\":\"Farm\",\"width\":120,\"height\":65,\"layer_count\":3,\"layers\":[{\"index\":0,\"id\":\"Back\",\"width\":120,\"height\":65}]}", raw: true)}}
              },
              "npcs": {
                "positions": {{FieldJson("[]", raw: true)}},
                "schedules": {{UnavailableFieldJson("npc_schedules_unavailable_without_complete_read_only_decompile_proof")}}
              },
              "quests": {
                "active_quests": {{FieldJson("[]", raw: true)}},
                "completed_quests": {{UnavailableFieldJson("no_verified_global_completed_quest_collection_found")}}
              },
              "world_progress": {
                "community_center": {{FieldJson("{\"bundles\":{},\"bundle_rewards\":{},\"completed_area_mail_flags\":[]}", raw: true)}},
                "perfection": {{UnavailableFieldJson("perfection_fields_not_verified_in_this_slice")}},
                "golden_walnuts": {{UnavailableFieldJson("golden_walnut_progress_not_verified_in_this_slice")}}
              },
              "menus": {
                "active_menu": {{FieldJson("{\"is_open\":false,\"type\":\"none\",\"full_type\":null}", raw: true)}},
                "menu_specific_state": {{UnavailableFieldJson("no_active_clickable_menu")}}
              },
              "modded_state": {
                "installed_count": {{FieldJson(0)}},
                "installed": {{FieldJson("[]", raw: true)}},
                "content_pack_count": {{FieldJson(0)}},
                "content_packs": {{FieldJson("[]", raw: true)}},
                "private_mod_state": {{UnavailableFieldJson("arbitrary_mod_private_state_unavailable_without_mod_specific_read_only_api")}}
              }
            }
            """;

            var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson)!;
            var hash = SnapshotHash.ComputeStateHash(state);
            var snapshotJson = $$"""
            {
              "schema_version": "snapshot.v1",
              "bridge_version": "test",
              "game_version": "1.6.15",
              "smapi_version": "4.5.2",
              "installed_mods": [],
              "save_id": {{FieldJson("Farm")}},
              "player_id": {{FieldJson("123")}},
              "game_tick": 100,
              "in_game_time": {{FieldJson(610)}},
              "real_timestamp": "2026-07-04T00:00:00Z",
              "state_hash": "{{hash}}",
              "completeness": "partial",
              "unavailable_fields": [],
              "state": {{stateJson}}
            }
            """;

            return new StringContent(snapshotJson, Encoding.UTF8, "application/json");
        }

        private static string FieldJson(object? value, string status = "available", bool raw = false)
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

        private static string UnavailableFieldJson(string reason)
        {
            return $$"""
            {
              "value": null,
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
