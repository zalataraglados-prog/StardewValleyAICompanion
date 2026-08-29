using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class GrangeDisplayMainlineTests
{
    [Fact]
    public void StrategyOptionIsTrainableWhileNativeExecutorRemainsCalibrationOnly()
    {
        var strategy = OptionCapabilityRegistrySource.GetRequired("festival.manage_grange_display");
        var executor = OptionCapabilityRegistrySource.GetRequired("executor.manage_grange_display");

        Assert.True(TrainingEligibilityPolicy.IsEligible(strategy));
        Assert.Contains(strategy.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(strategy.AutonomousCandidateEnabled);
        Assert.Equal(new[] { "EVD-292" }, strategy.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-292" }, strategy.OutputEvidenceIds);
        Assert.Contains("live_sell_price_quality_category_optimizer", strategy.TrainingEvidenceScope, StringComparison.Ordinal);

        Assert.False(TrainingEligibilityPolicy.IsEligible(executor));
        Assert.Equal(OptionTrainingEligibility.EvaluationOnly, executor.TrainingEligibility);
        Assert.DoesNotContain(executor.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.ReadTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.CandidateTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.CompilerTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, executor.OutputTrainingGate);
        Assert.Equal(new[] { "EVD-292" }, executor.RuntimeEvidenceIds);
    }

    [Fact]
    public void FreshProjectedPlacementFlowsToOneNativeDisplayMutation()
    {
        var snapshot = Snapshot("fingerprint-a", judged: false, operation: "place");
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "festival.manage_grange_display" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("manage_grange_display", candidate.Kind);
        AssertParameter(candidate.Parameters, "operation", "place");
        AssertParameter(candidate.Parameters, "inventory_stack_before", "2");
        AssertParameter(candidate.Parameters, "inventory_stack_after", "1");
        AssertParameter(candidate.Parameters, "first_place_score", "90");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("manage_grange_display", step.Kind);
        Assert.Contains("one_fresh_snapshot_display_mutation_only", step.SafetyConstraints);
        Assert.Contains("never_start_grange_judging", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.manage_grange_display", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("manage_grange_display", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void PostJudgingRetrievalUsesTheSameActionFamily()
    {
        var snapshot = Snapshot("fingerprint-r", judged: true, operation: "remove");
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "festival.manage_grange_display" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        AssertParameter(candidate.Parameters, "objective", "retrieve_after_judging");
        AssertParameter(candidate.Parameters, "operation", "remove");
        AssertParameter(candidate.Parameters, "sink_inventory_slot_index", "1");
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.manage_grange_display", Assert.Single(queue.Items).OptionId);
    }

    [Fact]
    public void CompilerRejectsFingerprintDriftBeforeRuntime()
    {
        var original = Snapshot("fingerprint-a", judged: false, operation: "place");
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "festival.manage_grange_display" }, true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot("fingerprint-b", judged: false, operation: "place");
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("grange_display_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RuntimeUsesFestivalMenuAndNeverWritesDisplayOrJudgingStateDirectly()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.GrangeDisplay.cs"));

        Assert.Contains("Festival.checkAction", source, StringComparison.Ordinal);
        Assert.Contains("StorageContainer", source, StringComparison.Ordinal);
        Assert.Contains("receiveRightClick", source, StringComparison.Ordinal);
        Assert.Contains("receiveLeftClick", source, StringComparison.Ordinal);
        Assert.Contains("AdvanceNativeObjectInteractionMovement", source, StringComparison.Ordinal);
        Assert.Contains("grangeMutex.IsLockHeld", source, StringComparison.Ordinal);
        Assert.DoesNotContain("grangeDisplay[request.GrangeDisplaySlotIndex!.Value] =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("grangeDisplay.Add", source, StringComparison.Ordinal);
        Assert.DoesNotContain("grangeScore =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("grangeJudged = true", source, StringComparison.Ordinal);
        Assert.DoesNotContain("grangeJudged = false", source, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(string fingerprint, bool judged, string operation)
    {
        var place = operation == "place";
        var objective = judged ? "retrieve_after_judging" : "prepare_best_available_display";
        var scoreBefore = place ? 5 : 20;
        var scoreAfter = place ? 20 : 5;
        var occupiedBefore = place ? 0 : 1;
        var occupiedAfter = place ? 1 : 0;
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":8,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[{"slot_index":0,"qualified_item_id":"(O)613","stack":2}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "grange_display":{"value":{
              "projection_status":"complete_current_festival_fall16_grange_context",
              "projection_fingerprint":"{{{fingerprint}}}",
              "gate_status":"ready",
              "festival_id":"festival_fall16",
              "festival_location_id":"Town",
              "grange_judged":{{{judged.ToString().ToLowerInvariant()}}},
              "mutex_locked_by_other":false,
              "best_available_score":95,
              "first_place_score":90,
              "native_contract":"Event.checkAction(festival_fall16_buildings_349_350_351)->FarmerTeam.grangeMutex->StorageContainer(9x3,Event.onGrangeChange,Utility.highlightSmallObjects)->one_native_remove_or_place_click_pair->okButton->mutex_release",
              "interaction_tiles":[{"tile_x":10,"tile_y":9,"tile_index":349}],
              "next_operation":{
                "status":"ready","objective":"{{{objective}}}","operation":"{{{operation}}}",
                "display_slot_index":0,"inventory_slot_index":{{{(place ? 0 : -1)}}},
                "inventory_stack_before":{{{(place ? 2 : -1)}}},"inventory_stack_after":{{{(place ? 1 : -1)}}},
                "sink_inventory_slot_index":{{{(place ? -1 : 1)}}},
                "qualified_item_id":"(O)613","item_id":"613","runtime_type":"StardewValley.Object",
                "quality":4,"actual_sell_price":500,"item_points":8,"scoring_group":"fruit",
                "score_before":{{{scoreBefore}}},"score_after":{{{scoreAfter}}},
                "occupied_slots_before":{{{occupiedBefore}}},"occupied_slots_after":{{{occupiedAfter}}}
              }
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"Town","width":64,"height":64,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-08-29T00:00:00Z", Completeness = "complete", State = state
        };
    }

    private static void AssertParameter(
        StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters, string name, string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Repository file not found.", Path.Combine(parts));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
