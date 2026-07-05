using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StardewAI.Contracts.State;
using Xunit;

namespace StardewAI.Backend.Tests
{
    public sealed class BackendEndpointTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> factory;

        public BackendEndpointTests(WebApplicationFactory<Program> factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task SnapshotAndCompilerEndpointsReturnTypedPreview()
        {
            using var client = factory.CreateClient();

            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

            var previewResponse = await client.PostAsJsonAsync("/api/v1/action-compiler/compile", new
            {
                goal = "water crops today",
                mode = "efficiency"
            });
            Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);

            using var previewJson = JsonDocument.Parse(await previewResponse.Content.ReadAsStringAsync());
            var root = previewJson.RootElement;
            Assert.Equal("feasible", root.GetProperty("feasibility").GetString());
            Assert.True(root.GetProperty("preview_only").GetBoolean());
            Assert.Equal("disabled", root.GetProperty("execution_permission").GetString());
            Assert.True(root.GetProperty("would_be_executable").GetBoolean());
            Assert.Equal("farm.maintain_crops", root.GetProperty("selected_option").GetProperty("option_id").GetString());
        }

        [Fact]
        public async Task ActionCompilerCheckDistinguishesFeasibilityFromExecutionPermission()
        {
            using var client = factory.CreateClient();
            await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());

            var response = await client.GetAsync("/api/v1/action-compiler/check");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
            Assert.Equal("disabled", json.RootElement.GetProperty("execution_permission").GetString());
            Assert.True(json.RootElement.GetProperty("preview_only").GetBoolean());
        }

        [Fact]
        public async Task StardewInputLatestReturnsTypedWorldModel()
        {
            using var client = factory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

            var response = await client.GetAsync("/api/v1/stardew/input/latest?goal=water%20crops&mode=efficiency");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            Assert.Equal("world_model.v1", root.GetProperty("schema_version").GetString());
            Assert.Equal("efficiency", root.GetProperty("mode").GetString());
            Assert.True(root.GetProperty("completeness").GetProperty("all_required_facts_readable").GetBoolean());
            Assert.False(root.GetProperty("planner_inputs").GetProperty("blocked").GetBoolean());
            Assert.Equal("Farm", root.GetProperty("facts").GetProperty("player").GetProperty("location_id").GetString());
            Assert.Equal(610, root.GetProperty("facts").GetProperty("game").GetProperty("time").GetInt32());
        }

        [Fact]
        public async Task GrandpaEvaluationEndpointReturnsFourCandleGoalReport()
        {
            using var client = factory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

            var response = await client.GetAsync("/api/v1/goals/grandpa-evaluation/latest");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            Assert.Equal("grandpa_evaluation_goal.v1", root.GetProperty("schema_version").GetString());
            Assert.Equal(12, root.GetProperty("target_score").GetInt32());
            Assert.Equal(4, root.GetProperty("current_candles").GetInt32());
            Assert.True(root.GetProperty("target_met").GetBoolean());
            Assert.Empty(root.GetProperty("missing_fact_paths").EnumerateArray());
        }

        [Fact]
        public async Task GrandpaTrainingEndpointReturnsDeterministicSample()
        {
            using var client = factory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

            var response = await client.GetAsync("/api/v1/training/grandpa-evaluation/latest");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            Assert.Equal("training_sample.v1", root.GetProperty("schema_version").GetString());
            Assert.Equal("grandpa_score", root.GetProperty("target").GetProperty("metric").GetString());
            Assert.True(root.GetProperty("target").GetProperty("complete").GetBoolean());
            Assert.False(root.GetProperty("feedback").GetProperty("executor_required").GetBoolean());
            Assert.False(root.GetProperty("feedback").GetProperty("available_now").GetBoolean());
            Assert.Empty(root.GetProperty("candidate_directions").EnumerateArray());
        }

        [Fact]
        public async Task SmallModelActionQueueCompilesAndDryRunExecutes()
        {
            using var client = factory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
            using var snapshotJson = JsonDocument.Parse(await snapshotResponse.Content.ReadAsStringAsync());
            var stateHash = snapshotJson.RootElement.GetProperty("state_hash").GetString();

            var compileResponse = await client.PostAsJsonAsync("/api/v1/small-model/action-queue/compile", new
            {
                schema_version = "small_model_action.v1",
                model_output_id = "model-output.test",
                source_model = "small-model.test",
                state_hash = stateHash,
                goal_id = "goal.test",
                actor = new
                {
                    actor_id = "ai_companion.main",
                    actor_type = "ai_companion",
                    control_surface = "companion_actor"
                },
                actions = new[]
                {
                    new
                    {
                        action_id = "action.test",
                        option_id = "farm.maintain_crops",
                        rationale = "maintain crops"
                    }
                }
            });

            Assert.Equal(HttpStatusCode.OK, compileResponse.StatusCode);
            using var queueJson = JsonDocument.Parse(await compileResponse.Content.ReadAsStringAsync());
            var queueRoot = queueJson.RootElement;
            Assert.Equal("action_queue.v1", queueRoot.GetProperty("schema_version").GetString());
            Assert.Equal("pending", queueRoot.GetProperty("status").GetString());
            Assert.Equal("ai_companion.main", queueRoot.GetProperty("actor").GetProperty("actor_id").GetString());
            var queueId = queueRoot.GetProperty("queue_id").GetString();

            var executeResponse = await client.PostAsync($"/api/v1/action-queues/{queueId}/execute", null);

            Assert.Equal(HttpStatusCode.OK, executeResponse.StatusCode);
            using var resultJson = JsonDocument.Parse(await executeResponse.Content.ReadAsStringAsync());
            var resultRoot = resultJson.RootElement;
            Assert.Equal("execution_batch_result.v1", resultRoot.GetProperty("schema_version").GetString());
            Assert.Equal("dry_run", resultRoot.GetProperty("executor_mode").GetString());
            Assert.Equal("dry_run_ready", resultRoot.GetProperty("status").GetString());
            Assert.Equal("ai_companion.main", resultRoot.GetProperty("actor").GetProperty("actor_id").GetString());
        }

        [Fact]
        public async Task SnapshotIngestRejectsMismatchedHash()
        {
            using var client = factory.CreateClient();

            var response = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent("bad-hash"));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("state_hash mismatch", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task SnapshotIngestRejectsUnavailableDefaultValue()
        {
            using var client = factory.CreateClient();

            var response = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent(unavailableCarriesDefault: true));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("non-readable status must not carry a default value", await response.Content.ReadAsStringAsync());
        }

        private static StringContent SampleSnapshotContent(string? forcedHash = null, bool unavailableCarriesDefault = false)
        {
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
                "year": {{FieldJson(3)}},
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
                "total_money_earned": {{FieldJson(1000000)}},
                "health": {{FieldJson(100)}},
                "max_health": {{FieldJson(100)}},
                "energy": {{FieldJson(270)}},
                "stamina": {{FieldJson(270)}},
                "max_energy": {{FieldJson(270)}},
                "level": {{FieldJson(25)}},
                "has_skull_key": {{FieldJson(true)}},
                "has_rusty_key": {{FieldJson(true)}},
                "married_or_roommate": {{FieldJson(false)}},
                "farmhouse_upgrade_level": {{FieldJson(1)}},
                "current_tool": {{FieldJson("(T)Axe")}},
                "current_item_qualified_id": {{FieldJson("(O)72")}},
                "active_object_qualified_id": {{FieldJson("(O)72")}},
                "active_menu": {{FieldJson("none", status: unavailableCarriesDefault ? "unavailable" : "available")}},
                "inventory": {{FieldJson("[{\"slot_index\":0,\"item_id\":\"Axe\",\"qualified_item_id\":\"(T)Axe\",\"display_name\":\"Axe\",\"stack\":1,\"quality\":0,\"is_empty\":false}]", raw: true)}}
              },
              "mods": {
                "installed_count": {{FieldJson(0)}},
                "installed_mods": {{FieldJson("[]", raw: true)}}
              },
              "game": {
                "current_location": {{FieldJson("Farm")}},
                "time_of_day": {{FieldJson(610)}}
              },
              "farm": {
                "grandpa_score": {{FieldJson(3)}},
                "crops": {{FieldJson("[]", raw: true)}}
              },
              "current_location": {
                "identity": {{FieldJson("{\"name\":\"Farm\",\"name_or_unique_name\":\"Farm\",\"type\":\"StardewValley.Farm\"}", raw: true)}}
              },
              "npcs": {
                "positions": {{FieldJson("[]", raw: true)}},
                "friendships": {{FieldJson("[{\"npc_name\":\"A\",\"points\":2000},{\"npc_name\":\"B\",\"points\":2000},{\"npc_name\":\"C\",\"points\":2000},{\"npc_name\":\"D\",\"points\":2000},{\"npc_name\":\"E\",\"points\":2000}]", raw: true)}},
                "schedules": {{UnavailableFieldJson("npc_schedules_unavailable_without_complete_read_only_decompile_proof")}}
              },
              "quests": {
                "active_quests": {{FieldJson("[]", raw: true)}},
                "mail_received": {{FieldJson("[\"petLoveMessage\"]", raw: true)}},
                "completed_quests": {{UnavailableFieldJson("no_verified_global_completed_quest_collection_found")}}
              },
              "world_progress": {
                "community_center": {{FieldJson("{\"location_accessible\":true,\"completed\":true,\"bundles\":{},\"bundle_rewards\":{},\"completed_area_mail_flags\":[]}", raw: true)}},
                "achievements": {{FieldJson("[5,26,34]", raw: true)}},
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
              },
              "transport": {
                "event_stream_websocket": {{FieldJson("{\"endpoint\":\"ws://127.0.0.1:8766/api/v1/events/ws\"}", raw: true)}}
              }
            }
            """;

            var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson)!;
            var hash = forcedHash ?? SnapshotHash.ComputeStateHash(state);
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
            var reason = status == "available" || status == "derived" ? "" : @",""reason"":""value_unavailable""";
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

        private static string FieldJson(int value)
        {
            return FieldJson((object)value);
        }

        private static string FieldJson(string? value)
        {
            return FieldJson((object?)value);
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
