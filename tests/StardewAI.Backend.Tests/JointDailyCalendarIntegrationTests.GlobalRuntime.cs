using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class JointDailyCalendarIntegrationTests
{
    [Theory]
    [InlineData(true, true, false, 2, true)]
    [InlineData(false, true, false, 1, false)]
    [InlineData(true, false, false, 1, false)]
    [InlineData(true, true, true, 2, false)]
    public async Task OriginalRuntimeKeepsTheFullPoolOnlyForTheExplicitGlobalJointMode(
        bool global, bool joint, bool owned, int pool, bool primary)
    {
        using var handler = new PrimaryRuntimeHandler();
        var result = await BuildPrimaryRuntime(handler, global, joint, owned);
        Assert.Equal(pool, handler.CompileRequest!.RankedEventCandidates.Length);
        Assert.Equal(primary, handler.CompileRequest.PrimaryCandidateIds is not null);
        Assert.Equal(primary, result.Plan["primary_candidate_ids"] is JsonArray);
        Assert.Equal(["/api/v1/planner/baseline/rank-options", "/api/v1/planner/daily-plan/compile"], handler.Paths);
        if (primary) Assert.Equal("primary", result.Plan["candidate_audit"]![0]!["candidate_id"]!.GetValue<string>());
        if (owned) Assert.Equal(["primary"], handler.CompileRequest.RequiredCandidateIds);
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("wrong")]
    [InlineData("duplicate")]
    public async Task OriginalRuntimeRejectsACompilerThatDidNotApplyThePrimaryConstraint(string reply)
    {
        using var handler = new PrimaryRuntimeHandler { Reply = reply };
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => BuildPrimaryRuntime(handler, true, true, false));
        Assert.Contains("daily_plan_primary_goal_constraint_not_applied", error.Message);
        Assert.Equal(2, handler.Paths.Count);
        Assert.DoesNotContain(handler.Paths, path => path.Contains("execute", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("planning_handoff_required", false)]
    [InlineData("resolved", true)]
    public async Task HandoffAndMissingPrimaryBindingsDoNotTurnIntoUnrelatedOptionalActions(string status, bool empty)
    {
        using var handler = new PrimaryRuntimeHandler { ResolutionStatus = status, EmptyBinding = empty };
        var result = await BuildPrimaryRuntime(handler, true, true, false);
        Assert.Empty(result.Plan["steps"]!.AsArray());
        if (empty) Assert.Empty(handler.CompileRequest!.PrimaryCandidateIds!);
        else Assert.Null(handler.CompileRequest!.PrimaryCandidateIds);
    }

    [Fact]
    public void NonGlobalRequestsKeepTheirOriginalBytes()
    {
        var method = typeof(LiveTrainingOptions).Assembly.GetType("Program", true)!
            .GetMethod("WithGlobalDailyPrimaryCandidates", BindingFlags.NonPublic | BindingFlags.Static)!;
        const string request = "{\"state_hash\":\"source\",\"future_field\":123}";
        Assert.Equal(request, method.Invoke(null, [request, null]));
        var enabled = JsonNode.Parse((string)method.Invoke(null, [request, new[] { "primary" }])!)!;
        Assert.Equal(123, enabled["future_field"]!.GetValue<int>());
        Assert.True(enabled["use_joint_calendar"]!.GetValue<bool>());
        Assert.Equal("primary", enabled["primary_candidate_ids"]![0]!.GetValue<string>());
    }

    private static async Task<(JsonObject Response, JsonObject Plan, JsonObject Queue, JsonObject Ranking)> BuildPrimaryRuntime(
        PrimaryRuntimeHandler handler, bool global, bool joint, bool owned)
    {
        using var http = new HttpClient(handler, disposeHandler: false);
        var options = new LiveTrainingOptions
        {
            Root = Path.Combine(Path.GetTempPath(), "global-primary-client"), RunId = "client",
            Goal = "grandpa.maximum_21", BackendUrl = "http://planner.test", UseDailyPlan = true,
            ContinueGlobalDailyPlanAfterCropTeacherRun = global, UseJointDailyCalendar = joint
        };
        Func<DailyPlanCompileRequest, DailyPlanCompileRequest>? hook = owned ? request =>
        {
            request.RequiredCandidateIds = ["primary"];
            return request;
        } : null;
        var method = typeof(LiveTrainingOptions).Assembly.GetType("Program", true)!
            .GetMethod("BuildQueueFromDailyPlanAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(13, method.GetParameters().Length);
        return await (Task<(JsonObject, JsonObject, JsonObject, JsonObject)>)method.Invoke(null,
            [http, options, "source", Array.Empty<JsonObject>(), null, null, null, null, null, null, false, null, hook])!;
    }

    private sealed class PrimaryRuntimeHandler : HttpMessageHandler
    {
        internal string Reply { get; init; } = "current";
        internal string ResolutionStatus { get; init; } = "resolved";
        internal bool EmptyBinding { get; init; }
        internal List<string> Paths { get; } = [];
        internal DailyPlanCompileRequest? CompileRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            object body;
            if (path == "/api/v1/planner/baseline/rank-options")
                body = new
                {
                    ranked_event_candidates = new[] { Work("optional", 1, 42), Work("primary", 2, 420) },
                    goal_resolution = new
                    {
                        status = ResolutionStatus, source_state_hash = "source", requested_goal_id = "grandpa.maximum_21",
                        effective_goal_id = "earn_money", bound_candidate_ids = EmptyBinding ? Array.Empty<string>() : new[] { "primary" }
                    }
                };
            else if (path == "/api/v1/planner/daily-plan/compile")
            {
                CompileRequest = JsonSerializer.Deserialize<DailyPlanCompileRequest>(await request.Content!.ReadAsStringAsync(cancellationToken), Json)!;
                var compiler = new DailyPlanCompiler();
                var plan = CompileRequest.PrimaryCandidateIds is not null && Reply != "legacy"
                    ? compiler.CompileWithPrimaryGoal(CompileRequest.RankedEventCandidates, "source", 900,
                        CompileRequest.RequiredCandidateIds ?? [], CompileRequest.PrimaryCandidateIds, availableMinutes: 60, energyBudget: 100)
                    : CompileRequest.UseJointCalendar
                        ? compiler.CompileWithJointCalendar(CompileRequest.RankedEventCandidates, "source", 900,
                            CompileRequest.RequiredCandidateIds ?? [], availableMinutes: 60, energyBudget: 100)
                        : compiler.Compile(CompileRequest.RankedEventCandidates, "source", availableMinutes: 60, energyBudget: 100);
                if (Reply == "wrong") plan.PrimaryCandidateIds = ["other"];
                if (Reply == "duplicate") plan.PrimaryCandidateIds = ["primary", "primary"];
                body = new { plan, action_queue = new { status = plan.Steps.Length == 0 ? "blocked" : "pending", items = Array.Empty<object>() } };
            }
            else throw new InvalidOperationException("Unexpected native/runtime request: " + path);
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json") };
        }
    }
}
