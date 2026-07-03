using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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

            var snapshotResponse = await client.PostAsJsonAsync("/api/v1/snapshots", SampleSnapshot());
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
            await client.PostAsJsonAsync("/api/v1/snapshots", SampleSnapshot());

            var response = await client.GetAsync("/api/v1/action-compiler/check");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
            Assert.Equal("disabled", json.RootElement.GetProperty("execution_permission").GetString());
            Assert.True(json.RootElement.GetProperty("preview_only").GetBoolean());
        }

        private static object SampleSnapshot()
        {
            return new
            {
                schema_version = "snapshot.v1",
                bridge_version = "test",
                game_tick = 100,
                real_timestamp = "2026-07-04T00:00:00Z",
                state_hash = "hash-100",
                completeness = "partial",
                unavailable_fields = Array.Empty<string>(),
                state = new
                {
                    game = new
                    {
                        current_location = Field("Farm"),
                        time_of_day = Field(610)
                    },
                    player = new
                    {
                        stamina = Field(270),
                        money = Field(500),
                        inventory = Field(Array.Empty<object>())
                    },
                    farm = new
                    {
                        crops = Field(Array.Empty<object>())
                    },
                    locations = new { },
                    npcs = new { },
                    quests = new { },
                    world_progress = new { },
                    menus = new
                    {
                        active_menu = Field("none")
                    },
                    mods = new { },
                    modded_state = new { }
                }
            };
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
    }
}
