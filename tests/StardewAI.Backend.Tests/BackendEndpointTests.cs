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
                "stamina": {{FieldJson(270)}},
                "max_energy": {{FieldJson(270)}},
                "current_tool": {{FieldJson("(T)Axe")}},
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
                "crops": {{FieldJson("[]", raw: true)}}
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
    }
}
