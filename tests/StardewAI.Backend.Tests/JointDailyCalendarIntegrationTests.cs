using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class JointDailyCalendarIntegrationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public async Task OriginalEndpointOptsIntoSharedDayWithoutChangingTheLegacyRequest(bool joint, int accepted)
    {
        await using var app = new WebApplicationFactory<global::Program>();
        using var http = app.CreateClient();
        app.Services.GetRequiredService<StateStore>().Snapshots["joint.day"] = Snapshot(new
            { time = new { value = 900, status = "available" } });
        using var response = await http.PostAsJsonAsync("/api/v1/planner/daily-plan/compile", new DailyPlanCompileRequest
        {
            StateHash = "joint.day", UseJointCalendar = joint,
            RankedEventCandidates = [Work("crop", 1, 420), Work("shop", 2, 210, 910)]
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var plan = body["plan"]!.Deserialize<SmallModelPlanEnvelope>(Json)!;
        Assert.Equal(accepted, plan.CandidateAudit.Count(row => row.Decision == "accepted"));
        Assert.Null(body["action_queue"]);
        if (joint) Assert.Contains("aggregate_calendar_window_missed", plan.CandidateAudit[1].Reasons);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("conflict")]
    [InlineData("missing")]
    [InlineData("unavailable")]
    public async Task JointEndpointRejectsUnknownSourceAndUnreadableClockRatherThanUsingLatestOrUnlimitedTime(string fault)
    {
        await using var app = new WebApplicationFactory<global::Program>();
        using var http = app.CreateClient();
        var clock = fault switch
        {
            "conflict" => JsonSerializer.SerializeToElement(new { time = new { value = 900, status = "available" }, time_of_day = new { value = 1000, status = "available" } }),
            "missing" => JsonSerializer.SerializeToElement(new { }),
            _ => JsonSerializer.SerializeToElement(new { time = new { value = 900, status = fault == "unavailable" ? "unavailable" : "available" } })
        };
        app.Services.GetRequiredService<StateStore>().Snapshots["joint.day"] = Snapshot(clock);
        using var response = await http.PostAsJsonAsync("/api/v1/planner/daily-plan/compile", new DailyPlanCompileRequest
        {
            StateHash = fault == "unknown" ? "not-ingested" : "joint.day", UseJointCalendar = true,
            RankedEventCandidates = [Work("crop", 1, 42)]
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("matching snapshot with a readable native game clock", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AliasOnlyClockUsesTheOriginalRecoveryCutoffInsteadOfUnlimitedOrdinaryWork()
    {
        await using var app = new WebApplicationFactory<global::Program>();
        using var http = app.CreateClient();
        app.Services.GetRequiredService<StateStore>().Snapshots["joint.day"] = Snapshot(new
            { time_of_day = new { value = 1905, status = "available" } });
        using var response = await http.PostAsJsonAsync("/api/v1/planner/daily-plan/compile", new DailyPlanCompileRequest
        {
            StateHash = "joint.day", UseJointCalendar = true, RankedEventCandidates = [Work("ordinary", 1, 42)]
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var plan = body["plan"]!.Deserialize<SmallModelPlanEnvelope>(Json)!;
        Assert.Empty(plan.Steps);
        Assert.Contains("aggregate_time_budget_exceeded", Assert.Single(plan.CandidateAudit).Reasons);
    }

    [Fact]
    public void RuntimeFlagReachesInitialAndSelectedCandidateRequestHelperWithoutChangingLegacyBytes()
    {
        var method = typeof(LiveTrainingOptions).Assembly.GetType("Program", true)!
            .GetMethod("WithJointDailyCalendar", BindingFlags.NonPublic | BindingFlags.Static)!;
        const string request = "{\"state_hash\":\"source\",\"ranked_event_candidates\":[{\"future_field\":123}]}";
        var defaults = LiveTrainingOptions.Parse(["--no-manifest", "--use-daily-plan"]);
        Assert.False(defaults.UseJointDailyCalendar);
        Assert.Equal(request, (string)method.Invoke(null, [request, defaults])!);
        var enabled = LiveTrainingOptions.Parse(["--no-manifest", "--use-daily-plan", "--joint-daily-calendar"]);
        var output = JsonNode.Parse((string)method.Invoke(null, [request, enabled])!)!;
        Assert.True(output["use_joint_calendar"]!.GetValue<bool>());
        Assert.Equal(123, output["ranked_event_candidates"]![0]!["future_field"]!.GetValue<int>());
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(["--no-manifest", "--joint-daily-calendar"]));
        Assert.DoesNotContain("use_joint_calendar", JsonSerializer.Serialize(new DailyPlanCompileRequest(), Json));
    }

    private static SnapshotEnvelope Snapshot(object clock) => new()
    {
        StateHash = "joint.day", State = new Dictionary<string, JsonElement>
        {
            ["time"] = JsonSerializer.SerializeToElement(clock),
            ["player"] = JsonSerializer.SerializeToElement(new { energy = new { value = 270, status = "available" } })
        }
    };
    private static PolicyEventCandidatePrediction Work(string id, int rank, int ticks, int? closes = null) => new()
    {
        CandidateId = id, Kind = "water_crop_tile", Rank = rank, TimelineStatus = "ready_now",
        LocationId = "Farm", TileX = rank, TileY = rank, EstimatedTicks = ticks, EnergyCost = 2, ClosesAt = closes
    };
}
