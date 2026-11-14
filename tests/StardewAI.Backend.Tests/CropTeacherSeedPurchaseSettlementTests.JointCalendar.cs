using StardewAI.Contracts.Training;
using System.Text.Json;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void JointCalendarPlanReusesTheOriginalWateringActionQueueCompilerWithoutNewNativeRequirements()
    {
        using var f = new WateringFixture();
        var time = SnapshotGameClockBudgetReader.CurrentTime(f.Before)!.Value;
        var candidates = f.Before.State["farm"].GetProperty("crops").GetProperty("value").EnumerateArray()
            .Select((crop, index) => new PolicyEventCandidatePrediction
            {
                CandidateId = "joint-water-" + index, Kind = "water_crop_tile", Rank = index + 1,
                TimelineStatus = "ready_now", LocationId = "Farm", TileX = crop.GetProperty("tile_x").GetInt32(),
                TileY = crop.GetProperty("tile_y").GetInt32(), EstimatedTicks = 60, EnergyCost = 2,
                ClosesAt = GameClockBudgetPolicy.AddClockMinutes(time, 60)
            }).ToArray();
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(candidates, f.Before.StateHash, time);
        var queue = new ActionQueueCompiler().Compile(plan, f.Before);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(queue.CompilerDiagnostics);
        Assert.Equal(candidates.Length, queue.Items.Length);
        Assert.All(queue.Items, item =>
        {
            Assert.Equal("executor.water_crop", item.OptionId);
            Assert.Equal("pending", item.Status);
            Assert.Empty(item.BlockingReasons);
        });
    }

    [Fact]
    public void OriginalWateringObligationsBindToMaterializedCandidatesBeforeOtherGoalsWithoutOwnerOrLedgerMutation()
    {
        using var f = new WateringFixture();
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(JsonSerializer.Serialize(f.Shared.Source.Ledger, JsonOptions), JsonOptions)!;
        ledger.SourceStateHash = f.Before.StateHash;
        var candidates = WateringCompositionCandidates(f).Prepend(new PolicyEventCandidatePrediction
        {
            CandidateId = "other-goal", Kind = "pet_daily_interaction", OptionId = "farm.care_for_pets", Rank = 1,
            LocationId = "Farm", TileX = 99, TileY = 99, TimelineStatus = "ready_now", EstimatedTicks = 42
        }).ToArray();
        var before = JsonSerializer.Serialize(ledger, JsonOptions);
        var binding = CropTeacherRunPlanner.BindDailyComposition(f.Shared.Inputs.Plan, f.Shared.Inputs.Request, f.Before, ledger,
            CompositionRequest(f.Before, candidates), completedDayBoundaryKeys: CompositionPriorNights(f));
        Assert.Equal("ready", binding.Status);
        Assert.Equal(f.Step.ActionBatch!.Tiles.Length, binding.RequiredCandidateIds.Length);
        var request = Assert.IsType<DailyPlanCompileRequest>(binding.CompileRequest);
        Assert.False(request.CompileActionQueue);
        Assert.True(request.UseJointCalendar);
        var plan = new DailyPlanCompiler().CompileWithJointCalendar(request.RankedEventCandidates, request.StateHash,
            SnapshotGameClockBudgetReader.CurrentTime(f.Before)!.Value, request.RequiredCandidateIds!);
        Assert.Equal("accepted", Assert.Single(plan.CandidateAudit.Where(row => row.CandidateId == "other-goal")).Decision);
        Assert.True(Array.FindIndex(plan.CandidateAudit, row => row.CandidateId == "other-goal") >= binding.RequiredCandidateIds.Length);
        Assert.All(plan.CandidateAudit.Take(binding.RequiredCandidateIds.Length), row => Assert.Equal("accepted", row.Decision));
        Assert.False(binding.NativeOwnerInterleavingAuthorized);
        Assert.False(binding.FormalProductTrainingAuthorized);
        Assert.Equal(before, JsonSerializer.Serialize(ledger, JsonOptions));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("ambiguous")]
    [InlineData("blocked")]
    public void OriginalRequiredWateringIsNotSilentlyDroppedWhenCandidateSurfaceIsIncomplete(string fault)
    {
        using var f = new WateringFixture();
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(JsonSerializer.Serialize(f.Shared.Source.Ledger, JsonOptions), JsonOptions)!;
        ledger.SourceStateHash = f.Before.StateHash;
        var candidates = WateringCompositionCandidates(f).ToArray();
        if (fault == "missing") candidates = candidates.Skip(1).ToArray();
        if (fault == "ambiguous") candidates = candidates.Append(candidates[0]).ToArray();
        if (fault == "blocked") candidates[0].TimelineStatus = "blocked";
        var binding = CropTeacherRunPlanner.BindDailyComposition(f.Shared.Inputs.Plan, f.Shared.Inputs.Request, f.Before, ledger,
            CompositionRequest(f.Before, candidates), completedDayBoundaryKeys: CompositionPriorNights(f));
        Assert.Equal("blocked", binding.Status);
        Assert.Contains("crop_teacher_required_candidate_missing_or_ambiguous", binding.BlockingReasons);
        Assert.Null(binding.CompileRequest);
    }

    [Fact]
    public void CompositionRejectsForeignLedgerRatherThanRebasingItToTheNewPlanner()
    {
        using var f = new WateringFixture();
        var ledger = new StrategyCommitmentLedger { SourceStateHash = "foreign" };
        Assert.Throws<InvalidDataException>(() => CropTeacherRunPlanner.BindDailyComposition(f.Shared.Inputs.Plan,
            f.Shared.Inputs.Request, f.Before, ledger, CompositionRequest(f.Before, WateringCompositionCandidates(f))));
    }

    [Fact]
    public void RainSatisfiedMaintenanceLeavesOtherGoalsAvailableWithoutInventingRequiredWork()
    {
        using var f = new WateringFixture();
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(f.Before, JsonOptions), JsonOptions)!;
        ChangeState(snapshot, "farm", node =>
        {
            foreach (var crop in node["crops"]!["value"]!.AsArray())
            {
                crop!["watered"] = true;
                crop["needs_watering"] = false;
            }
        });
        ChangeState(snapshot, "current_location", node =>
        {
            node["crops"] = System.Text.Json.Nodes.JsonNode.Parse(snapshot.State["farm"].GetRawText())!["crops"]!.DeepClone();
            node["home_context"]!["value"]!["current_location_is_home"] = false;
        });
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(JsonSerializer.Serialize(f.Shared.Source.Ledger, JsonOptions), JsonOptions)!;
        ledger.SourceStateHash = snapshot.StateHash;
        var binding = CropTeacherRunPlanner.BindDailyComposition(f.Shared.Inputs.Plan, f.Shared.Inputs.Request, snapshot, ledger,
            CompositionRequest(snapshot, []), completedDayBoundaryKeys: CompositionPriorNights(f));
        Assert.Equal("ready", binding.Status);
        Assert.Empty(binding.RequiredCandidateIds);
        Assert.NotNull(binding.CompileRequest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingOriginalPlantingPrefixIsNotReinterpretedAsMissingLaterWatering(bool sitePrepared)
    {
        using var f = new OwnedRouteFixture(prepareSource: source =>
        {
            source.OtherRequest.MaterialClaims.Single().Quantity = 38;
            source.OwnRequest.MaterialClaims.Single().Quantity = 2;
            ChangeState(source.Before, "time", node =>
            {
                node["total_days"]!["value"] = source.Ledger.CropPlantingCommitments.Single().PlantingTotalDay;
                node["year"]!["value"] = 2;
                node["season"]!["value"] = "spring";
                node["day"]!["value"] = 1;
            });
            ChangeState(source.Before, "farm", node => node["crops"] =
                JsonSerializer.SerializeToNode(new { status = "available", value = Array.Empty<object>() }));
        });
        var prepared = sitePrepared ? f.Inputs.Plan.CoveredCommitmentIds.ToHashSet(StringComparer.Ordinal) : null;
        var binding = CropTeacherRunPlanner.BindDailyComposition(f.Inputs.Plan, f.Inputs.Request, f.Source.Before, f.Source.Ledger,
            CompositionRequest(f.Source.Before, []),
            preparedCommitmentIds: prepared);
        Assert.Equal("original_prefix_required", binding.Status);
        Assert.NotEqual(CropTeacherRunStageKinds.ReplanRequired, binding.OriginalNextStep.Stage);
        Assert.DoesNotContain("crop_teacher_water_target_crop_missing", binding.BlockingReasons);
        if (sitePrepared)
        {
            Assert.Equal(CropTeacherRunStageKinds.ExecuteCropBatch, binding.OriginalNextStep.Stage);
            Assert.Contains("crop_teacher_pending_planting_prefix", binding.BlockingReasons);
        }
        Assert.Null(binding.CompileRequest);
    }

    [Fact]
    public void CompositionPreservesOtherGoalRequirementsAndRejectsStaleCandidateSource()
    {
        using var f = new WateringFixture();
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(JsonSerializer.Serialize(f.Shared.Source.Ledger, JsonOptions), JsonOptions)!;
        ledger.SourceStateHash = f.Before.StateHash;
        var request = CompositionRequest(f.Before, WateringCompositionCandidates(f));
        request.RequiredCandidateIds = request.RankedEventCandidates.Select(row => row.CandidateId).ToArray();
        var originalIds = request.RequiredCandidateIds.ToArray();
        var binding = CropTeacherRunPlanner.BindDailyComposition(f.Shared.Inputs.Plan, f.Shared.Inputs.Request, f.Before, ledger,
            request, completedDayBoundaryKeys: CompositionPriorNights(f));
        Assert.Equal("ready", binding.Status);
        Assert.Equal(originalIds, binding.RequiredCandidateIds);
        Assert.Equal(originalIds, request.RequiredCandidateIds);
        request.StateHash = "stale";
        Assert.Throws<InvalidDataException>(() => CropTeacherRunPlanner.BindDailyComposition(f.Shared.Inputs.Plan,
            f.Shared.Inputs.Request, f.Before, ledger, request, completedDayBoundaryKeys: CompositionPriorNights(f)));
    }

    private static DailyPlanCompileRequest CompositionRequest(SnapshotEnvelope snapshot, IEnumerable<PolicyEventCandidatePrediction> rows) =>
        new() { StateHash = snapshot.StateHash, RankedEventCandidates = rows.ToArray() };

    private static HashSet<string> CompositionPriorNights(WateringFixture f) => f.Shared.Inputs.Plan.Days
        .Where(row => row.TotalDay < f.Step.TotalDay && row.NativeSleepRequired)
        .Select(row => CropTeacherTransitionLedger.DayBoundaryKey(row.TotalDay)).ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<PolicyEventCandidatePrediction> WateringCompositionCandidates(WateringFixture f) =>
        f.Before.State["farm"].GetProperty("crops").GetProperty("value").EnumerateArray().Select((crop, index) =>
            new PolicyEventCandidatePrediction
            {
                CandidateId = "required-water-" + index, Kind = "water_crop_tile", Rank = index + 2,
                TimelineStatus = "ready_now", LocationId = "Farm", TileX = crop.GetProperty("tile_x").GetInt32(),
                TileY = crop.GetProperty("tile_y").GetInt32(), EstimatedTicks = 60, EnergyCost = 2
            });
}
