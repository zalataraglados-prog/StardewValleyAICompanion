using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class BackendEndpointTests
{
    [Fact]
    public async Task MuseumStructuredPolicyAcceptsTheShared95ObservationShapeWithoutCalibration()
    {
        using var client = factory.CreateClient();
        using var content = SampleSnapshotContent();
        var root = JsonNode.Parse(await content.ReadAsStringAsync())!;
        var museum = JsonSerializer.Serialize(new
        {
            total_donatable_items = 95, donated_count = 94, missing_item_count = 1,
            collection_complete = false, complete_collection_achievement_received = false,
            donatable_items = Enumerable.Range(1, 95).Select(id => new { qualified_item_id = "(O)" + id, donated = id < 95 })
        });
        root["state"]!["world_progress"]!["museum"] = JsonNode.Parse(FieldJson(museum, raw: true));
        root["state_hash"] = SnapshotHash.ComputeStateHash(root["state"]!.Deserialize<Dictionary<string, JsonElement>>()!);
        var response = await client.PostAsync("/api/v1/snapshots", new StringContent(root.ToJsonString(), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var snapshot = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var path = Path.Combine(Path.GetTempPath(), "stardewai-museum-unit-" + Guid.NewGuid().ToString("N") + ".json");
        WriteMinimalStructuredPolicyCheckpoint(path);
        var ranking = await client.PostAsJsonAsync("/api/v1/planner/baseline/rank-options", new
        {
            state_hash = snapshot.RootElement.GetProperty("state_hash").GetString(), goal_id = MuseumPolicyInputProjector.GoalId,
            policy_checkpoint_path = path, require_structured_policy = true
        });
        Assert.Equal(HttpStatusCode.OK, ranking.StatusCode);
    }

    [Fact]
    public async Task MuseumStructuredPolicyRejectsMissingCurrentMuseumFacts()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var snapshot = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var path = Path.Combine(Path.GetTempPath(), "stardewai-museum-unit-" + Guid.NewGuid().ToString("N") + ".json");
        WriteMinimalStructuredPolicyCheckpoint(path);
        var ranking = await client.PostAsJsonAsync("/api/v1/planner/baseline/rank-options", new
        {
            state_hash = snapshot.RootElement.GetProperty("state_hash").GetString(), goal_id = MuseumPolicyInputProjector.GoalId,
            policy_checkpoint_path = path, require_structured_policy = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, ranking.StatusCode);
        Assert.Contains("Museum", await ranking.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
