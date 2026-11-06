using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class BackendEndpointTests
{
    [Theory]
    [InlineData(null, "requires an explicit compatible route timing calibration")]
    [InlineData("{}", "calibration/context is unavailable")]
    public async Task FriendshipStructuredPolicyRejectsMissingOrUnprovedCalibration(string? calibration, string reason)
    {
        using var client = factory.CreateClient();
        var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
        Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
        using var snapshot = JsonDocument.Parse(await snapshotResponse.Content.ReadAsStringAsync());
        var path = Path.Combine(Path.GetTempPath(), "stardewai-friendship-unit-" + Guid.NewGuid().ToString("N") + ".json");
        WriteMinimalStructuredPolicyCheckpoint(path);
        var response = await client.PostAsJsonAsync("/api/v1/planner/baseline/rank-options", new
        {
            state_hash = snapshot.RootElement.GetProperty("state_hash").GetString(),
            goal_id = FriendshipPolicyInputProjector.GoalId,
            policy_checkpoint_path = path, require_structured_policy = true,
            route_timing_calibration_json = calibration
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(reason, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("goal.collection.master_angler")]
    [InlineData("grandpa.maximum_21")]
    public async Task UnrelatedStructuredGoalDoesNotRequireFriendshipCalibration(string goal)
    {
        using var client = factory.CreateClient();
        var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
        Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
        using var snapshot = JsonDocument.Parse(await snapshotResponse.Content.ReadAsStringAsync());
        var path = Path.Combine(Path.GetTempPath(), "stardewai-unrelated-unit-" + Guid.NewGuid().ToString("N") + ".json");
        WriteMinimalStructuredPolicyCheckpoint(path);
        var response = await client.PostAsJsonAsync("/api/v1/planner/baseline/rank-options", new
        {
            state_hash = snapshot.RootElement.GetProperty("state_hash").GetString(), goal_id = goal,
            policy_checkpoint_path = path, require_structured_policy = true,
            route_timing_calibration_json = "not-json"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("policy_features", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
