using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class BackendEndpointTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    public async Task MarriageStructuredInputFailsClosedWithoutCurrentFactsAndCalibration(string? calibration)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var snapshot = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var path = Path.Combine(Path.GetTempPath(), "stardewai-marriage-unit-" + Guid.NewGuid().ToString("N") + ".json");
        WriteMinimalStructuredPolicyCheckpoint(path);
        var ranking = await client.PostAsJsonAsync("/api/v1/planner/baseline/rank-options", new
        {
            state_hash = snapshot.RootElement.GetProperty("state_hash").GetString(), goal_id = MarriagePolicyInputProjector.GoalId,
            policy_checkpoint_path = path, require_structured_policy = true, route_timing_calibration_json = calibration
        });
        Assert.Equal(HttpStatusCode.BadRequest, ranking.StatusCode);
        Assert.Contains("Marriage", await ranking.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
