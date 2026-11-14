using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DailyDispatchUsesOnlyAcceptedOriginalBatchTilesAndPreservesInputs(int capacity)
    {
        using var f = new WateringFixture();
        var binding = WateringDispatchBinding(f);
        var before = JsonSerializer.Serialize(binding, JsonOptions);
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(binding.CompileRequest!.RankedEventCandidates,
            f.Before.StateHash, SnapshotGameClockBudgetReader.CurrentTime(f.Before)!.Value, binding.RequiredCandidateIds,
            energyBudget: capacity * 2);
        var selected = CropTeacherRunPlanner.SelectOriginalDailyBatch(binding, plan);
        if (capacity == 0) Assert.Null(selected);
        else
        {
            Assert.Equal(capacity, selected!.RemainingQuantity);
            Assert.Equal(f.Step.ActionBatch!.Tiles.Take(capacity), selected.ActionBatch!.Tiles);
            Assert.Equal(f.Step.ActionBatch with { Tiles = selected.ActionBatch.Tiles }, selected.ActionBatch);
        }
        Assert.Equal(before, JsonSerializer.Serialize(binding, JsonOptions));
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("duplicate")]
    [InlineData("foreign_first")]
    [InlineData("deferred")]
    public void DailyDispatchCannotBorrowAStalePlanOrJumpPastOtherWork(string fault)
    {
        using var f = new WateringFixture();
        var binding = WateringDispatchBinding(f);
        var input = binding.CompileRequest!;
        if (fault == "deferred")
        {
            input.RankedEventCandidates[0].TimelineStatus = "deferred";
            input.RankedEventCandidates[0].ScheduledWaitCost = 600;
        }
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(input.RankedEventCandidates, input.StateHash,
            SnapshotGameClockBudgetReader.CurrentTime(f.Before)!.Value, binding.RequiredCandidateIds);
        if (fault == "stale") plan.StateHash = "stale";
        if (fault == "duplicate") plan.CandidateAudit = [..plan.CandidateAudit, plan.CandidateAudit[0]];
        if (fault == "foreign_first") plan.CandidateAudit = [new() { CandidateId = "other-owner", Decision = "accepted" }, ..plan.CandidateAudit];
        if (fault is "stale" or "duplicate")
            Assert.Throws<InvalidDataException>(() => CropTeacherRunPlanner.SelectOriginalDailyBatch(binding, plan));
        else Assert.Null(CropTeacherRunPlanner.SelectOriginalDailyBatch(binding, plan));
    }

    [Fact]
    public async Task BudgetedBatchUsesOriginalOwnerAndReturnsFreshRemainingMaintenanceWithoutDispatchingOptionalWork()
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        var binding = WateringDispatchBinding(f.Basis);
        var input = binding.CompileRequest!;
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(input.RankedEventCandidates, input.StateHash,
            SnapshotGameClockBudgetReader.CurrentTime(f.Basis.Before)!.Value, binding.RequiredCandidateIds, maxCandidates: 1);
        var selected = CropTeacherRunPlanner.SelectOriginalDailyBatch(binding, plan)!;
        var operation = await InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedWateringAsync",
            f.Shared.Runtime.Client, f.Shared.Runtime.LiveOptions, 1, selected, f.Shared.Inputs.Plan, f.Shared.Inputs.Request,
            owner, f.Shared.Files.Native);
        await owner.ExecuteOriginalWateringAsync(operation, f.Shared.Runtime.Client, () => f.EmitNative(applied: 1));
        await f.Rebind(owner, operation);
        Assert.Null(owner.Pending);
        Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
        var remaining = CropTeacherRunPlanner.Evaluate(f.Shared.Inputs.Plan, f.Shared.Inputs.Request, f.Basis.After,
            completedDayBoundaryKeys: CompositionPriorNights(f.Basis));
        Assert.Equal(CropTeacherRunStageKinds.ExecuteCropBatch, remaining.Stage);
        Assert.Single(remaining.ActionBatch!.Tiles);
        Assert.DoesNotContain(selected.ActionBatch!.Tiles[0], remaining.ActionBatch.Tiles);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiveCompositionUsesOriginalRankingEndpointAndCompilerButNeverCompilesAMixedOwnerQueue(bool recovery)
    {
        using var f = new OwnedWateringFixture();
        await using var owner = await f.Shared.Files.Open();
        var options = f.Shared.Runtime.LiveOptions;
        options.UseDailyPlan = true;
        options.UseJointDailyCalendar = true;
        options.DailyPlanMaxCandidates = 1;
        var candidates = recovery ? new[] { new PolicyEventCandidatePrediction
        {
            CandidateId = "recovery", Kind = "recovery_close_menu", OptionId = "recovery.stabilize_day", Rank = 1,
            TimelineStatus = "ready_now"
        } } : WateringCompositionCandidates(f.Basis).ToArray();
        using var handler = new DailyCompositionRankingHandler(f.Shared.Runtime.Handler, candidates);
        using var http = new HttpClient(handler, disposeHandler: false);
        var task = InvokeLive<Task>("ComposeCropTeacherDailyDispatchAsync", http, options, 1, f.Shared.Inputs.Plan,
            f.Shared.Inputs.Request, owner, new HashSet<string>(StringComparer.Ordinal), f.Basis.Before, null!);
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        Assert.Equal(recovery, (bool)result.GetType().GetProperty("ControlPreempted")!.GetValue(result)!);
        var step = (CropTeacherRunStep?)result.GetType().GetProperty("Step")!.GetValue(result);
        if (recovery) Assert.Null(step);
        else Assert.Single(step!.ActionBatch!.Tiles);
        Assert.Equal(1, handler.Ranks);
        Assert.Equal(!recovery, handler.PlanOnly);
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
        Assert.Null(owner.Pending);
    }

    [Fact]
    public async Task LegacyDailyCallerStillUsesExactGoalBindingWithoutTheCompositionHook()
    {
        using var f = new OwnedWateringFixture();
        var options = f.Shared.Runtime.LiveOptions;
        options.UseDailyPlan = true;
        using var handler = new DailyCompositionRankingHandler(f.Shared.Runtime.Handler, WateringCompositionCandidates(f.Basis).ToArray());
        using var http = new HttpClient(handler, disposeHandler: false);
        var task = InvokeLive<Task>("BuildQueueFromDailyPlanAsync", http, options, f.Basis.Before.StateHash,
            Array.Empty<JsonObject>(), null!, null!, null!, null!, null!, null!, false, null!, null!);
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var plan = (JsonObject)result.GetType().GetField("Item2")!.GetValue(result)!;
        Assert.Empty(plan["steps"]!.AsArray());
        Assert.False(handler.PlanOnly);
        Assert.Equal(1, handler.Ranks);
    }

    [Fact]
    public async Task ActiveStoryControlPreemptsCompositionBeforeRankingOrCompiling()
    {
        using var f = new OwnedWateringFixture(prepareSource: source => ChangeState(source.Before, "player", node =>
            node["story_event"] = JsonSerializer.SerializeToNode(new { status = "available", value = new
            {
                active = true, event_up = true, is_festival = false, skipped = false,
                player_control_sequence = false, active_minigame_type = "", event_id = "native-story"
            } })));
        await using var owner = await f.Shared.Files.Open();
        var options = f.Shared.Runtime.LiveOptions;
        options.UseDailyPlan = true;
        options.UseJointDailyCalendar = true;
        using var handler = new DailyCompositionRankingHandler(f.Shared.Runtime.Handler, WateringCompositionCandidates(f.Basis).ToArray());
        using var http = new HttpClient(handler, disposeHandler: false);
        var task = InvokeLive<Task>("ComposeCropTeacherDailyDispatchAsync", http, options, 1, f.Shared.Inputs.Plan,
            f.Shared.Inputs.Request, owner, new HashSet<string>(StringComparer.Ordinal), f.Basis.Before, null!);
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        Assert.True((bool)result.GetType().GetProperty("ControlPreempted")!.GetValue(result)!);
        Assert.Equal(0, handler.Ranks);
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
        Assert.Null(owner.Pending);
    }

    private static CropTeacherDailyCompositionBinding WateringDispatchBinding(WateringFixture f)
    {
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(JsonSerializer.Serialize(f.Shared.Source.Ledger, JsonOptions), JsonOptions)!;
        ledger.SourceStateHash = f.Before.StateHash;
        return CropTeacherRunPlanner.BindDailyComposition(f.Shared.Inputs.Plan, f.Shared.Inputs.Request, f.Before, ledger,
            CompositionRequest(f.Before, WateringCompositionCandidates(f)), completedDayBoundaryKeys: CompositionPriorNights(f));
    }

    private sealed class DailyCompositionRankingHandler(HttpMessageHandler inner, PolicyEventCandidatePrediction[] candidates) : DelegatingHandler(inner)
    {
        internal int Ranks { get; private set; }
        internal int AvailabilityReads { get; private set; }
        internal bool CorruptAvailabilitySourceHash { get; set; }
        internal bool PlanOnly { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/baseline/rank-options", StringComparison.Ordinal))
            {
                Ranks++;
                return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
                {
                    ranked_event_candidates = candidates,
                    goal_resolution = new { status = "resolved", effective_goal_id = "earn_money", bound_candidate_ids = new[] { "absent-other-root" } }
                }, JsonOptions), Encoding.UTF8, "application/json") };
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/daily-plan/compile", StringComparison.Ordinal))
                PlanOnly = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!["compile_action_queue"]!.GetValue<bool>() == false;
            if (request.RequestUri.AbsolutePath.EndsWith("/options/availability", StringComparison.Ordinal)) AvailabilityReads++;
            var response = await base.SendAsync(request, token);
            if (CorruptAvailabilitySourceHash && request.RequestUri.AbsolutePath.EndsWith("/options/availability", StringComparison.Ordinal))
            {
                var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                body["state_hash"] = "another-source";
                response.Dispose();
                return new(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(JsonOptions), Encoding.UTF8, "application/json") };
            }
            return response;
        }
    }
}
