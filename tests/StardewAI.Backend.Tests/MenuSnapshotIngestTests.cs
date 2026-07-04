using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StardewAI.Contracts.State;
using Xunit;

namespace StardewAI.Backend.Tests
{
    public sealed class MenuSnapshotIngestTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> factory;

        public MenuSnapshotIngestTests(WebApplicationFactory<Program> factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task SnapshotIngestAcceptsMenuReadSlice()
        {
            using var client = factory.CreateClient();

            var response = await client.PostAsync("/api/v1/snapshots", SnapshotWithMenus());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SnapshotIngestRejectsUnavailableMenuSpecificDefault()
        {
            using var client = factory.CreateClient();

            var response = await client.PostAsync("/api/v1/snapshots", SnapshotWithMenus(unavailableMenuSpecificCarriesDefault: true));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("state.menus.menu_specific_state non-readable status must not carry a default value", await response.Content.ReadAsStringAsync());
        }

        private static StringContent SnapshotWithMenus(bool unavailableMenuSpecificCarriesDefault = false)
        {
            var menuSpecific = unavailableMenuSpecificCarriesDefault
                ? FieldJson("{\"selected_index\":0}", status: "unavailable", raw: true)
                : UnavailableFieldJson("menu_specific_fields_not_verified_in_this_slice");
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
                "active_menu": {{FieldJson("StardewValley.Menus.ShopMenu")}},
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
                "identity": {{FieldJson("{\"name\":\"Farm\",\"name_or_unique_name\":\"Farm\",\"type\":\"StardewValley.Farm\"}", raw: true)}}
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
                "active_menu": {{FieldJson("{\"is_open\":true,\"type\":\"ShopMenu\",\"full_type\":\"StardewValley.Menus.ShopMenu\"}", raw: true)}},
                "identity": {{FieldJson("{\"type\":\"ShopMenu\",\"full_type\":\"StardewValley.Menus.ShopMenu\",\"assembly\":\"Stardew Valley\",\"is_iclickable_menu\":true}", raw: true)}},
                "screen_bounds": {{FieldJson("{\"x\":100,\"y\":80,\"width\":800,\"height\":600}", raw: true)}},
                "public_state": {{FieldJson("{\"destroy\":false,\"invisible\":false,\"game_window_size_changed\":false,\"upper_right_close_button_present\":true,\"currently_snapped_component_present\":false}", raw: true)}},
                "menu_specific_state": {{menuSpecific}}
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
              "unavailable_fields": ["menus.menu_specific_state"],
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
