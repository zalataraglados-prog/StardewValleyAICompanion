using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class JointDailyCalendarIntegrationTests
{
    [Fact]
    public async Task PrimaryGroupUsesTheOriginalEndpointAndSingleSharedBudget()
    {
        await using var app = new WebApplicationFactory<global::Program>();
        using var http = app.CreateClient();
        app.Services.GetRequiredService<StateStore>().Snapshots["joint.day"] = Snapshot(new { time = new { value = 900, status = "available" } });
        using var response = await http.PostAsJsonAsync("/api/v1/planner/daily-plan/compile", new DailyPlanCompileRequest
        {
            StateHash = "joint.day", UseJointCalendar = true, PrimaryCandidateIds = ["primary"],
            RankedEventCandidates = [Work("optional", 1, 210, 905), Work("primary", 2, 420), Work("fits", 3, 42)]
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plan = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["plan"]!.Deserialize<SmallModelPlanEnvelope>(Json)!;
        Assert.Equal(["primary", "fits"], plan.CandidateAudit.Where(row => row.Decision == "accepted").Select(row => row.CandidateId));
        Assert.Equal(["primary"], plan.PrimaryCandidateIds);
        Assert.Contains("aggregate_calendar_window_missed", plan.CandidateAudit.Single(row => row.CandidateId == "optional").Reasons);
    }

    [Theory]
    [InlineData("wrong_mode")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public async Task PrimaryBindingsCannotUseWrongModeOrAnotherSource(string fault)
    {
        await using var app = new WebApplicationFactory<global::Program>();
        using var http = app.CreateClient();
        app.Services.GetRequiredService<StateStore>().Snapshots["joint.day"] = Snapshot(new { time = new { value = 900, status = "available" } });
        using var response = await http.PostAsJsonAsync("/api/v1/planner/daily-plan/compile", new DailyPlanCompileRequest
        {
            StateHash = "joint.day", UseJointCalendar = fault != "wrong_mode", CompileActionQueue = true,
            PrimaryCandidateIds = fault switch { "missing" => ["other"], "duplicate" => ["primary", "primary"], _ => ["primary"] },
            RankedEventCandidates = [Work("primary", 1, 42)]
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(app.Services.GetRequiredService<StateStore>().ActionQueues);
    }
}
