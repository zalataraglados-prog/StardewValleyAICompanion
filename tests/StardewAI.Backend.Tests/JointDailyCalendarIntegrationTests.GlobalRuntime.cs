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

    [Theory]
    [InlineData("time", true)]
    [InlineData("energy", false)]
    [InlineData("wrong_state", false)]
    [InlineData("absent", false)]
    public async Task OnlyPinnedPrimaryTimeExhaustionInvokesOriginalNativeSaveRoute(string fault, bool expected)
    {
        using var handler = new PrimaryRuntimeHandler { PrimaryFailure = fault };
        var result = await BuildPrimaryRuntime(handler, true, true, false, requireNativeSave: true);
        Assert.Equal(expected, handler.RecoveryRequested);
        Assert.Equal(expected ? 4 : 2, handler.Paths.Count);
        if (!expected)
        {
            Assert.Empty(result.Plan["steps"]!.AsArray());
            Assert.Null(result.Ranking["global_primary_budget_recovery"]);
            return;
        }
        Assert.Equal("pending", result.Queue["status"]!.GetValue<string>());
        Assert.Null(handler.CompileRequest!.PrimaryCandidateIds);
        Assert.Equal("native-recovery", Assert.Single(handler.CompileRequest.RankedEventCandidates).CandidateId);
        Assert.NotNull(result.Ranking["global_primary_budget_recovery"]?["blocked_compile_response"]);
        Assert.Contains("sleep", result.Plan["steps"]!.ToJsonString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("exact", true)]
    [InlineData("partial", false)]
    [InlineData("absent_binding", false)]
    [InlineData("empty_binding", false)]
    [InlineData("stale_resolution", false)]
    [InlineData("stale_queue", false)]
    [InlineData("no_save_owner", false)]
    public async Task OnlyProvenSameDayPrimarySuppressionInvokesOriginalNativeSaveRoute(string fault, bool expected)
    {
        using var handler = new PrimaryRuntimeHandler
        {
            PrimaryNpc = "Marnie",
            BoundIds = fault switch
            {
                "partial" => ["primary", "optional"],
                "absent_binding" => ["primary", "not-ranked"],
                "empty_binding" => [],
                _ => ["primary"]
            },
            ResolutionState = fault == "stale_resolution" ? "different" : "source",
            PrimaryFailure = fault == "stale_queue" ? "wrong_state" : ""
        };
        var suppression = new JsonObject { ["kind"] = "social", ["option_id"] = "social.talk_npc", ["npc_name"] = "Marnie" };
        var result = await BuildPrimaryRuntime(handler, true, true, false,
            requireNativeSave: fault != "no_save_owner", suppressed: [suppression]);
        Assert.Equal(expected, handler.RecoveryRequested);
        Assert.Equal(expected ? 4 : 2, handler.Paths.Count);
        if (!expected)
        {
            Assert.Null(result.Ranking["global_primary_budget_recovery"]);
            return;
        }
        Assert.Equal("all_bound_primary_work_suppressed_until_game_day_changes",
            result.Ranking["global_primary_budget_recovery"]!["reason"]!.GetValue<string>());
        Assert.Equal("native-recovery", Assert.Single(handler.CompileRequest!.RankedEventCandidates).CandidateId);
        Assert.Contains("sleep", result.Plan["steps"]!.ToJsonString(), StringComparison.Ordinal);
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
        PrimaryRuntimeHandler handler, bool global, bool joint, bool owned, bool requireNativeSave = false,
        JsonObject[]? suppressed = null)
    {
        using var http = new HttpClient(handler, disposeHandler: false);
        var options = new LiveTrainingOptions
        {
            Root = Path.Combine(Path.GetTempPath(), "global-primary-client"), RunId = "client",
            Goal = "grandpa.maximum_21", BackendUrl = "http://planner.test", UseDailyPlan = true,
            ContinueGlobalDailyPlanAfterCropTeacherRun = global, UseJointDailyCalendar = joint,
            RequireNativeSaveBoundary = requireNativeSave
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
            [http, options, "source", suppressed ?? Array.Empty<JsonObject>(), null, null, null, null, null, null, false, null, hook])!;
    }

    private sealed class PrimaryRuntimeHandler : HttpMessageHandler
    {
        internal string Reply { get; init; } = "current";
        internal string ResolutionStatus { get; init; } = "resolved";
        internal bool EmptyBinding { get; init; }
        internal string PrimaryFailure { get; init; } = "";
        internal string PrimaryNpc { get; init; } = "";
        internal string[]? BoundIds { get; init; }
        internal string ResolutionState { get; init; } = "source";
        internal bool RecoveryRequested { get; private set; }
        internal List<string> Paths { get; } = [];
        internal DailyPlanCompileRequest? CompileRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            object body;
            if (path == "/api/v1/planner/baseline/rank-options")
            {
                var rankRequest = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
                var candidates = rankRequest["candidates"] as JsonArray;
                RecoveryRequested = candidates is { Count: > 0 } &&
                    candidates[0]?["option_id"]?.GetValue<string>() == "recovery.stabilize_day";
                if (RecoveryRequested)
                {
                    Assert.Contains(candidates![0]!["parameters"]!.AsArray(), parameter =>
                        parameter?["name"]?.GetValue<string>() == "control_plane.native_save_boundary" &&
                        parameter?["value"]?.GetValue<string>() == "true");
                }
                var optional = Work("optional", 1, 42);
                var primary = Work("primary", 2, PrimaryFailure is "time" or "wrong_state" or "absent" ? 4800 : 420);
                if (PrimaryNpc.Length > 0)
                {
                    primary.OptionId = "social.talk_npc";
                    primary.Parameters = [new SmallModelActionParameter { Name = "npc_name", Value = PrimaryNpc }];
                }
                if (PrimaryFailure == "energy") primary.EnergyCost = 200;
                var recovery = Work("native-recovery", 1, 60);
                recovery.OptionId = "recovery.stabilize_day";
                recovery.Kind = "recovery_sleep_immediately";
                recovery.LocationId = "FarmHouse";
                recovery.Parameters = [new SmallModelActionParameter { Name = "execution_option_id", Value = "executor.sleep" }];
                body = new
                {
                    ranked_event_candidates = RecoveryRequested ? new[] { recovery } : new[] { optional, primary },
                    goal_resolution = new
                    {
                        status = ResolutionStatus, source_state_hash = ResolutionState, requested_goal_id = "grandpa.maximum_21",
                        effective_goal_id = "earn_money", bound_candidate_ids = BoundIds ?? (EmptyBinding ? Array.Empty<string>() : new[] { "primary" })
                    }
                };
            }
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
                body = new
                {
                    plan,
                    action_queue = new
                    {
                        state_hash = PrimaryFailure == "wrong_state" ? "different" : "source",
                        status = plan.Steps.Length == 0 ? "blocked" : "pending",
                        compiler_diagnostics = plan.Steps.Length == 0 && PrimaryFailure != "absent"
                            ? new[] { "empty_action_list" } : Array.Empty<string>(),
                        items = Array.Empty<object>()
                    }
                };
            }
            else throw new InvalidOperationException("Unexpected native/runtime request: " + path);
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json") };
        }
    }
}
