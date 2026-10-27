using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class PlanningHandoffCaptureTests
{
    [Fact]
    public async Task CropTeacherHandoffCapturesCompleteBoundedFarmLayout()
    {
        const int mapWidth = 33;
        const int mapHeight = 34;
        var requestedBounds = new List<(int MinX, int MinY, int MaxX, int MaxY)>();
        using var http = new HttpClient(new DelegateHandler(request =>
        {
            var values = ParseQuery(request.RequestUri!);
            Assert.Equal("farm_layout", values["profile"]);
            Assert.Equal("true", values["fresh"]);
            Assert.DoesNotContain("expected_state_hash", values.Keys);
            Assert.DoesNotContain("expected_game_tick", values.Keys);
            var bounds = (
                int.Parse(values["min_x"]),
                int.Parse(values["min_y"]),
                int.Parse(values["max_x"]),
                int.Parse(values["max_y"]));
            requestedBounds.Add(bounds);
            return Snapshot(
                "layout-" + requestedBounds.Count,
                mapWidth,
                mapHeight,
                bounds.Item1,
                bounds.Item2,
                bounds.Item3,
                bounds.Item4);
        }));
        var output = Path.Combine(
            Path.GetTempPath(),
            "stardewai-planning-handoff-tests",
            Guid.NewGuid().ToString("N"));
        var handoff = new JsonObject
        {
            ["handoff_kind"] = "crop_production_teacher",
            ["required_snapshot_profiles"] = new JsonArray("farm_layout"),
            ["automatic_dispatch_required"] = true
        };

        var result = await PlanningHandoffCapture.CaptureAsync(
            http,
            "http://127.0.0.1:8765/api/v1/snapshot?profile=clock&expected_state_hash=old&expected_game_tick=4",
            output,
            Snapshot("source", mapWidth, mapHeight, 0, 0, 0, 0),
            handoff);

        Assert.Equal(4, result.LayoutSnapshotCount);
        Assert.Equal(mapWidth, result.MapWidth);
        Assert.Equal(mapHeight, result.MapHeight);
        Assert.Equal(
            new[]
            {
                (0, 0, 0, 0),
                (0, 0, 31, 31),
                (32, 0, 32, 31),
                (0, 32, 31, 33),
                (32, 32, 32, 33)
            },
            requestedBounds);
        var manifest = JsonNode.Parse(
            await File.ReadAllTextAsync(result.ManifestPath))!.AsObject();
        Assert.Equal(
            "farm_layout_capture_complete",
            manifest["status"]!.GetValue<string>());
        Assert.Equal("source", manifest["source_state_hash"]!.GetValue<string>());
        Assert.Equal(4, manifest["layout_snapshot_count"]!.GetValue<int>());
        Assert.Equal(4, manifest["layout_snapshots"]!.AsArray().Count);
    }

    [Fact]
    public void HandoffDetectionRequiresAutomaticFarmLayoutContract()
    {
        var ranking = new JsonObject
        {
            ["goal_resolution"] = new JsonObject
            {
                ["status"] = "planning_handoff_required",
                ["planning_handoff"] = new JsonObject
                {
                    ["handoff_kind"] = "crop_production_teacher",
                    ["required_snapshot_profiles"] =
                        new JsonArray("farm_layout"),
                    ["automatic_dispatch_required"] = true
                }
            }
        };

        Assert.True(
            PlanningHandoffCapture.TryReadCropProductionTeacherHandoff(
                ranking,
                out var handoff));
        Assert.Equal(
            "crop_production_teacher",
            handoff["handoff_kind"]!.GetValue<string>());

        ranking["goal_resolution"]!["planning_handoff"]![
            "automatic_dispatch_required"] = false;
        Assert.False(
            PlanningHandoffCapture.TryReadCropProductionTeacherHandoff(
                ranking,
                out _));
    }

    private static string Snapshot(
        string stateHash,
        int mapWidth,
        int mapHeight,
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
        return new JsonObject
        {
            ["schema_version"] = "snapshot.v1",
            ["state_hash"] = stateHash,
            ["state"] = new JsonObject
            {
                ["identity"] = new JsonObject
                {
                    ["save_id"] = Envelope("save-a"),
                    ["player_id"] = Envelope("player-a")
                },
                ["time"] = new JsonObject
                {
                    ["total_days"] = Envelope(0),
                    ["time"] = Envelope(600)
                },
                ["farm"] = new JsonObject
                {
                    ["site_preparation_context"] = Envelope(new JsonObject
                    {
                        ["schema_version"] =
                            "farm_site_preparation_context.v2",
                        ["projection_status"] =
                            "complete_bounded_current_farm_region",
                        ["location_id"] = "Farm",
                        ["is_farm"] = true,
                        ["map_width"] = mapWidth,
                        ["map_height"] = mapHeight,
                        ["bounds"] = new JsonObject
                        {
                            ["min_x"] = minX,
                            ["min_y"] = minY,
                            ["max_x"] = maxX,
                            ["max_y"] = maxY
                        },
                        ["tile_count"] =
                            (maxX - minX + 1) * (maxY - minY + 1),
                        ["tiles"] = new JsonArray()
                    })
                }
            }
        }.ToJsonString();
    }

    private static JsonObject Envelope(JsonNode? value) => new()
    {
        ["value"] = value?.DeepClone(),
        ["status"] = "available"
    };

    private static Dictionary<string, string> ParseQuery(Uri uri) =>
        uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length == 2
                    ? Uri.UnescapeDataString(parts[1])
                    : string.Empty,
                StringComparer.OrdinalIgnoreCase);

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, string> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseFactory(request),
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
