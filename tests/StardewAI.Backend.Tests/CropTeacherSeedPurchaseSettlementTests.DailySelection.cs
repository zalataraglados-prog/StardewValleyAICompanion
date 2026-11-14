using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task PlanOnlySelectionMatchesTheOriginalCompiledQueueLeaseExactly()
    {
        using var f = new OwnedWateringFixture();
        var rows = ActualWaterSelectionCandidates(f);
        using var response = await f.Shared.Runtime.Client.PostAsJsonAsync("http://test/api/v1/planner/daily-plan/compile",
            new DailyPlanCompileRequest { StateHash = f.Basis.Before.StateHash, UseJointCalendar = true,
                RankedEventCandidates = rows, MaxCandidates = 2, CompileActionQueue = true }, JsonOptions);
        response.EnsureSuccessStatusCode();
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var ranking = new JsonObject { ["ranked_event_candidates"] = JsonSerializer.SerializeToNode(rows, JsonOptions) };
        var original = SelectedQueueDecisionLease.Create(body["action_queue"]!.AsObject(), ranking);
        var projected = SelectedQueueDecisionLease.CreateFromPlan(body["plan"]!.Deserialize<SmallModelPlanEnvelope>(JsonOptions)!, ranking);
        Assert.Equal(JsonSerializer.Serialize(original.Candidates, JsonOptions), JsonSerializer.Serialize(projected.Candidates, JsonOptions));
    }

    [Theory]
    [InlineData(20, false)]
    [InlineData(1, true)]
    public async Task AcceptedMechanicalPrefixReusesTheSelectedHighLevelDecisionWithoutPolicyReranking(int water, bool refill)
    {
        using var f = new OwnedWateringFixture(water: water, prepareSource: CompleteDailySelectionSurface);
        await using var owner = await f.Shared.Files.Open();
        ConfigureJointSelection(f, 2);
        using var handler = new DailyCompositionRankingHandler(f.Shared.Runtime.Handler, ActualWaterSelectionCandidates(f));
        using var http = new HttpClient(handler, disposeHandler: false);
        var first = await ComposeSelection(f, owner, http, 1, f.Basis.Before);
        var selected = SelectionProperty<CropTeacherRunStep>(first, "Step")!;
        var lease = SelectionProperty<object>(first, "Selection")!;
        Assert.Equal(2, selected.ActionBatch!.Tiles.Length);
        var operation = await InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedWateringAsync",
            f.Shared.Runtime.Client, f.Shared.Runtime.LiveOptions, 1, selected, f.Shared.Inputs.Plan,
            f.Shared.Inputs.Request, owner, f.Shared.Files.Native);
        await owner.ExecuteOriginalWateringAsync(operation, f.Shared.Runtime.Client,
            () => f.EmitNative(applied: 1, failedSuffix: !refill));
        await f.Rebind(owner, operation);
        var second = await ComposeSelection(f, owner, http, 2, f.Basis.After, lease);
        Assert.Equal(1, handler.Ranks);
        Assert.Same(lease, SelectionProperty<object>(second, "Selection"));
        Assert.Equal(refill, SelectionProperty<bool>(second, "ControlPreempted"));
        Assert.Equal(refill ? 0 : 1, handler.AvailabilityReads);
        if (!refill)
        {
            var pending = SelectionProperty<CropTeacherRunStep>(second, "Step")!;
            Assert.Equal(1, pending.RemainingQuantity);
            Assert.Equal(1, Assert.Single(pending.ActionBatch!.Tiles).TileX);
        }
        Assert.Null(owner.Pending);
        Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task EmptyCanYieldsToOriginalRefillApproachBeforeAFirstPolicyDecision()
    {
        using var f = new OwnedWateringFixture(water: 0);
        await using var owner = await f.Shared.Files.Open();
        ConfigureJointSelection(f, 2);
        using var handler = new DailyCompositionRankingHandler(f.Shared.Runtime.Handler, ActualWaterSelectionCandidates(f));
        using var http = new HttpClient(handler, disposeHandler: false);
        var result = await ComposeSelection(f, owner, http, 1, f.Basis.Before);
        Assert.True(SelectionProperty<bool>(result, "ControlPreempted"));
        Assert.Equal(0, handler.Ranks);
        Assert.Equal(0, handler.AvailabilityReads);
        Assert.Null(owner.Pending);
    }

    [Fact]
    public async Task ZeroCreditRefillKeepsBothSelectedCropTargetsUntilTheyAreActuallyWatered()
    {
        using var f = new OwnedWateringFixture(water: 0, atRefillApproach: true, prepareSource: CompleteDailySelectionSurface);
        await using var owner = await f.Shared.Files.Open();
        ConfigureJointSelection(f, 2);
        using var handler = new DailyCompositionRankingHandler(f.Shared.Runtime.Handler, ActualWaterSelectionCandidates(f));
        using var http = new HttpClient(handler, disposeHandler: false);
        var first = await ComposeSelection(f, owner, http, 1, f.Basis.Before);
        var selected = SelectionProperty<CropTeacherRunStep>(first, "Step")!;
        var lease = SelectionProperty<object>(first, "Selection")!;
        var operation = await InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedWateringAsync",
            f.Shared.Runtime.Client, f.Shared.Runtime.LiveOptions, 1, selected, f.Shared.Inputs.Plan,
            f.Shared.Inputs.Request, owner, f.Shared.Files.Native);
        await owner.ExecuteOriginalWateringAsync(operation, f.Shared.Runtime.Client,
            () => f.EmitNative(applied: 0, failedSuffix: true, refillOnlySuffix: true));
        await f.Rebind(owner, operation);
        var second = await ComposeSelection(f, owner, http, 2, f.Basis.After, lease);
        Assert.Same(lease, SelectionProperty<object>(second, "Selection"));
        Assert.Equal(2, SelectionProperty<CropTeacherRunStep>(second, "Step")!.ActionBatch!.Tiles.Length);
        Assert.Equal(1, handler.Ranks);
        Assert.Equal(1, handler.AvailabilityReads);
        Assert.Null(owner.Pending);
    }

    [Fact]
    public async Task ASelectedForeignGoalIsHeldRatherThanDroppedOrDispatchedThroughTheCropOwner()
    {
        using var f = new OwnedWateringFixture(prepareSource: source => ChangeState(source.Before, "current_location", node =>
            node["home_context"]!["value"]!["current_location_is_home"] = false));
        await using var owner = await f.Shared.Files.Open();
        ConfigureJointSelection(f, 3);
        var rows = ActualWaterSelectionCandidates(f).Where(row => row.TileX != 9).Append(new PolicyEventCandidatePrediction
            { CandidateId = "other-goal", Kind = "pet_daily_interaction", OptionId = "farm.care_for_pets", Rank = 9,
                LocationId = "Farm", TileX = 7, TileY = 8, TimelineStatus = "ready_now", EstimatedTicks = 42 }).ToArray();
        using var handler = new DailyCompositionRankingHandler(f.Shared.Runtime.Handler, rows);
        using var http = new HttpClient(handler, disposeHandler: false);
        var first = await ComposeSelection(f, owner, http, 1, f.Basis.Before);
        var selected = SelectionProperty<CropTeacherRunStep>(first, "Step")!;
        var lease = SelectionProperty<object>(first, "Selection")!;
        var operation = await InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedWateringAsync",
            f.Shared.Runtime.Client, f.Shared.Runtime.LiveOptions, 1, selected, f.Shared.Inputs.Plan,
            f.Shared.Inputs.Request, owner, f.Shared.Files.Native);
        await owner.ExecuteOriginalWateringAsync(operation, f.Shared.Runtime.Client, () => f.EmitNative());
        await f.Rebind(owner, operation);
        var second = await ComposeSelection(f, owner, http, 2, f.Basis.After, lease);
        Assert.Null(SelectionProperty<CropTeacherRunStep>(second, "Step"));
        Assert.Equal("crop_teacher_joint_daily_selected_other_owner_required", SelectionProperty<string>(second, "BlockReason"));
        Assert.Same(lease, SelectionProperty<object>(second, "Selection"));
        Assert.Equal(1, handler.Ranks);
        Assert.Null(owner.Pending);
    }

    private static void ConfigureJointSelection(OwnedWateringFixture f, int maxCandidates)
    {
        f.Shared.Runtime.LiveOptions.UseDailyPlan = true;
        f.Shared.Runtime.LiveOptions.UseJointDailyCalendar = true;
        f.Shared.Runtime.LiveOptions.DailyPlanMaxCandidates = maxCandidates;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RequiredForeignGoalSurvivesOwnedCompletionWithoutBeingRetiredByTheCropRefresh(bool sameOption, bool corruptForeign)
    {
        using var f = new OwnedWateringFixture(prepareSource: source =>
        {
            CompleteDailySelectionSurface(source);
            ChangeState(source.Before, "current_location", node => node["home_context"]!["value"]!["current_location_is_home"] = false);
        });
        await using var owner = await f.Shared.Files.Open();
        ConfigureJointSelection(f, 3);
        var foreign = sameOption ? ActualWaterSelectionCandidates(f).Single(row => row.TileX == 9) : new PolicyEventCandidatePrediction
        {
            CandidateId = "required-other-goal", Kind = "pet_daily_interaction", OptionId = "farm.care_for_pets", Rank = 9,
            LocationId = "Farm", TileX = 7, TileY = 8, TimelineStatus = "ready_now", EstimatedTicks = 42
        };
        foreign.Rank = 9;
        var rows = ActualWaterSelectionCandidates(f).Where(row => row.TileX != 9).Append(foreign).ToArray();
        var input = CompositionRequest(f.Basis.Before, rows);
        input.RequiredCandidateIds = [foreign.CandidateId];
        var binding = CropTeacherRunPlanner.BindDailyComposition(f.Shared.Inputs.Plan, f.Shared.Inputs.Request,
            f.Basis.Before, f.Shared.Source.Ledger, input);
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(rows, f.Basis.Before.StateHash,
            SnapshotGameClockBudgetReader.CurrentTime(f.Basis.Before)!.Value, binding.RequiredCandidateIds, goalId: "earn_money");
        var ranking = new JsonObject { ["ranked_event_candidates"] = JsonSerializer.SerializeToNode(rows, JsonOptions) };
        var lease = SelectedQueueDecisionLease.CreateFromPlan(plan, ranking);
        Assert.Contains(lease.Candidates, row => row.CandidateId == foreign.CandidateId);
        var selectionType = typeof(LiveTrainingOptions).Assembly.GetType("Program")!
            .GetNestedType("CropTeacherDailySelection", System.Reflection.BindingFlags.NonPublic)!;
        var selection = Activator.CreateInstance(selectionType, binding.OriginalNextStep.TotalDay, plan.GoalId, lease,
            binding.OwnedCandidateIds.ToHashSet(StringComparer.Ordinal), "original-plan.json", "original-ranking.json")!;
        var selected = CropTeacherRunPlanner.SelectOriginalDailyBatch(binding, plan, f.Shared.Inputs.Plan)!;
        Assert.Equal(2, selected.ActionBatch!.Tiles.Length);
        var operation = await InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedWateringAsync",
            f.Shared.Runtime.Client, f.Shared.Runtime.LiveOptions, 1, selected, f.Shared.Inputs.Plan,
            f.Shared.Inputs.Request, owner, f.Shared.Files.Native);
        await owner.ExecuteOriginalWateringAsync(operation, f.Shared.Runtime.Client, () => f.EmitNative());
        await f.Rebind(owner, operation);
        using var handler = new DailyCompositionRankingHandler(f.Shared.Runtime.Handler, rows);
        if (corruptForeign) handler.CorruptAvailabilityReadNumber = 2;
        using var http = new HttpClient(handler, disposeHandler: false);
        if (corruptForeign)
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => ComposeSelection(f, owner, http, 2, f.Basis.After, selection));
            Assert.Contains("different snapshot", error.Message);
            Assert.Equal(0, handler.Ranks);
            Assert.Equal(2, handler.AvailabilityReads);
            Assert.Null(owner.Pending);
            Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
            return;
        }
        var result = await ComposeSelection(f, owner, http, 2, f.Basis.After, selection);
        Assert.Same(selection, SelectionProperty<object>(result, "Selection"));
        Assert.Null(SelectionProperty<CropTeacherRunStep>(result, "Step"));
        Assert.Equal("crop_teacher_joint_daily_selected_other_owner_required", SelectionProperty<string>(result, "BlockReason"));
        Assert.Equal(0, handler.Ranks);
        Assert.Equal(2, handler.AvailabilityReads);
        var preparation = SelectionProperty<object>(result, "OtherGoalPreparation")!;
        Assert.Equal(foreign.CandidateId, SelectionProperty<SelectedQueueCandidateLock>(preparation, "Candidate")!.CandidateId);
        var evidence = JsonNode.Parse(File.ReadAllText(SelectionProperty<string>(preparation, "EvidencePath")!))!;
        Assert.Equal(f.Basis.After.StateHash, evidence["state_hash"]!.GetValue<string>());
        Assert.False(evidence["policy_model_invoked"]!.GetValue<bool>());
        Assert.False(evidence["native_dispatch_performed"]!.GetValue<bool>());
        Assert.False(evidence["native_owner_interleaving_authorized"]!.GetValue<bool>());
        Assert.True(evidence["shared_source_return_required"]!.GetValue<bool>());
        if (sameOption)
        {
            Assert.Equal(1, evidence["selected_candidate_count"]!.GetValue<int>());
            var queue = JsonNode.Parse(File.ReadAllText(SelectionProperty<string>(preparation, "QueuePath")!))!;
            Assert.Equal(f.Basis.After.StateHash, queue["state_hash"]!.GetValue<string>());
            Assert.NotEmpty(queue["items"]!.AsArray());
        }
        Assert.Null(owner.Pending);
        Assert.Equal(1, f.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task WrongSourceAvailabilityCannotRefreshOrReplaceTheRetainedDecision()
    {
        using var f = new OwnedWateringFixture(prepareSource: CompleteDailySelectionSurface);
        await using var owner = await f.Shared.Files.Open();
        ConfigureJointSelection(f, 2);
        using var handler = new DailyCompositionRankingHandler(f.Shared.Runtime.Handler, ActualWaterSelectionCandidates(f));
        using var http = new HttpClient(handler, disposeHandler: false);
        var first = await ComposeSelection(f, owner, http, 1, f.Basis.Before);
        handler.CorruptAvailabilitySourceHash = true;
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => ComposeSelection(f, owner, http, 2, f.Basis.Before,
            SelectionProperty<object>(first, "Selection")));
        Assert.Contains("different snapshot", error.Message);
        Assert.Equal(1, handler.Ranks);
        Assert.Null(owner.Pending);
        Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task BudgetedLaterOriginalBatchUsesItsOriginalOwnerInsteadOfStallingOnTheFirstBatch()
    {
        using var f = new OwnedWateringFixture(prepareSource: source =>
        {
            CompleteDailySelectionSurface(source);
            var request = JsonSerializer.Deserialize<MoneyRevenueCropNativeProductionRunPlanRequest>(File.ReadAllText(source.RequestPath), JsonOptions)!;
            request.MaxQueueItemCount = 1;
            Fixture.Save(source.RequestPath, request);
        });
        var candidates = ActualWaterSelectionCandidates(f);
        foreach (var row in candidates) row.Rank = row.TileX == 1 ? 1 : row.TileX == 0 ? 2 : 9;
        var binding = CropTeacherRunPlanner.BindDailyComposition(f.Shared.Inputs.Plan, f.Shared.Inputs.Request,
            f.Basis.Before, f.Shared.Source.Ledger, CompositionRequest(f.Basis.Before, candidates));
        var compiled = new DailyPlanCompiler().CompileWithJointCalendar(candidates, f.Basis.Before.StateHash,
            SnapshotGameClockBudgetReader.CurrentTime(f.Basis.Before)!.Value, binding.RequiredCandidateIds);
        var selected = CropTeacherRunPlanner.SelectOriginalDailyBatch(binding, compiled, f.Shared.Inputs.Plan)!;
        Assert.Equal(1, Assert.Single(selected.ActionBatch!.Tiles).TileX);
        Assert.NotEqual(binding.OriginalNextStep.ActionBatch!.BatchIndex, selected.ActionBatch.BatchIndex);
        await using var owner = await f.Shared.Files.Open();
        var operation = await InvokeLive<Task<CropTeacherManagedSeedOperation>>("PrepareCropTeacherOwnedWateringAsync",
            f.Shared.Runtime.Client, f.Shared.Runtime.LiveOptions, 1, selected, f.Shared.Inputs.Plan,
            f.Shared.Inputs.Request, owner, f.Shared.Files.Native);
        Assert.True(operation.IsWateringMaintenance);
        var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(f.QueuePath), JsonOptions)!;
        Assert.Equal("1", Assert.Single(queue.Items).NormalizedCommand.Parameters.Single(row => row.Name == "target_tile_x").Value);
    }

    private static void CompleteDailySelectionSurface(Fixture source)
    {
        ChangeState(source.Before, "current_location", node => node["planting_context"] =
            JsonSerializer.SerializeToNode(new { status = "available", value = new { hoe_dirt_tiles = Array.Empty<object>() } }));
        ChangeState(source.Before, "time", node => node["weather"] =
            JsonSerializer.SerializeToNode(new { status = "available", value = "sunny" }));
    }

    private static PolicyEventCandidatePrediction[] ActualWaterSelectionCandidates(OwnedWateringFixture f) =>
        WateringCompositionCandidates(f.Basis).Select(row =>
        {
            row.CandidateId = $"water:{row.LocationId}:{row.TileX},{row.TileY}";
            row.OptionId = "farm.maintain_crops";
            return row;
        }).ToArray();

    private static async Task<object> ComposeSelection(OwnedWateringFixture f, CropTeacherManagedSeedOwner owner,
        HttpClient http, int iteration, StardewAI.Contracts.State.SnapshotEnvelope source, object? selection = null)
    {
        var task = InvokeLive<Task>("ComposeCropTeacherDailyDispatchAsync", http, f.Shared.Runtime.LiveOptions,
            iteration, f.Shared.Inputs.Plan, f.Shared.Inputs.Request, owner, new HashSet<string>(StringComparer.Ordinal), source, selection!);
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static T? SelectionProperty<T>(object result, string name) =>
        (T?)result.GetType().GetProperty(name)!.GetValue(result);
}
