using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FishingItemGrabRewardRecoveryTests
{
    [Fact]
    public void ExistingCapacityCompilesOneMandatoryNativeRewardClaim()
    {
        var snapshot = Snapshot(canAcceptReward: true);
        var availability = Availability(snapshot);
        var option = Assert.Single(availability.Options);
        var candidate = Assert.Single(option.EventCandidates, row =>
            row.Kind == "recovery_claim_fishing_item_grab_reward");

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.DoesNotContain(candidate.Parameters, row =>
            row.Name == "fishing_reward_discard_slot_index");

        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability);
        var selected = Assert.Single(ranked);
        Assert.Equal(candidate.CandidateId, selected.CandidateId);

        var plan = new DailyPlanCompiler().Compile(
            ranked,
            snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("claim_fishing_item_grab_reward", planStep.Kind);
        Assert.Contains(
            "one_reward_stack_then_fresh_snapshot",
            planStep.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal(
            "executor.claim_fishing_item_grab_reward",
            item.OptionId);
        Assert.Empty(item.BlockingReasons);
        var actionStep = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal(
            "claim_fishing_item_grab_reward",
            actionStep.StepType);
        Assert.Contains("(O)145", actionStep.Target, StringComparison.Ordinal);
    }

    [Fact]
    public void FullInventorySelectsLowestTotalValueSafeObjectStack()
    {
        var snapshot = Snapshot(canAcceptReward: false);
        var candidate = Assert.Single(
            Assert.Single(Availability(snapshot).Options).EventCandidates,
            row => row.Kind ==
                "recovery_claim_fishing_item_grab_reward");

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, row =>
            row.Name == "fishing_reward_requires_discard" &&
            row.Value == "true");
        Assert.Contains(candidate.Parameters, row =>
            row.Name == "fishing_reward_discard_slot_index" &&
            row.Value == "1");
        Assert.Contains(candidate.Parameters, row =>
            row.Name == "fishing_reward_discard_qualified_item_id" &&
            row.Value == "(O)390");
        Assert.Contains(candidate.Parameters, row =>
            row.Name == "fishing_reward_discard_authorization" &&
            row.Value ==
                "objective_safe_unprotected_lowest_total_store_value");

        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            Availability(snapshot));
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var queueItem = Assert.Single(queue.Items);
        Assert.Empty(queueItem.BlockingReasons);
        Assert.Contains(queueItem.NormalizedCommand.Parameters, row =>
            row.Name == "fishing_reward_discard_slot_index" &&
            row.Value == "1");
        Assert.Contains(queueItem.NormalizedCommand.Parameters, row =>
            row.Name == "fishing_reward_discard_qualified_item_id" &&
            row.Value == "(O)390");
        Assert.Contains(queueItem.NormalizedCommand.Parameters, row =>
            row.Name == "fishing_reward_discard_stack_before" &&
            row.Value == "1");
        Assert.Contains(queueItem.NormalizedCommand.Parameters, row =>
            row.Name == "fishing_reward_discard_quality" &&
            row.Value == "0");
    }

    [Fact]
    public void FullInventoryWithoutSafeObjectFailsClosed()
    {
        var snapshot = Snapshot(
            canAcceptReward: false,
            includeSafeDiscard: false);
        var candidate = Assert.Single(
            Assert.Single(Availability(snapshot).Options).EventCandidates,
            row => row.Kind ==
                "recovery_claim_fishing_item_grab_reward");

        Assert.False(candidate.Available);
        Assert.Contains(
            "fishing_reward_inventory_full_no_objective_safe_discard",
            candidate.BlockReasons);
    }

    [Fact]
    public void CompilerRejectsRewardIdentityDrift()
    {
        var snapshot = Snapshot(canAcceptReward: true);
        var availability = Availability(snapshot);
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability);
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        step.Parameters = step.Parameters
            .Select(row => row.Name == "fishing_reward_stack_before"
                ? new SmallModelActionParameter
                {
                    Name = row.Name,
                    Value = "999"
                }
                : row)
            .ToArray();

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "fishing_reward_menu_binding_drift",
            Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void PrimitiveIsRegisteredAsRuntimeRecoveryNotPolicyOutput()
    {
        const string optionId =
            "executor.claim_fishing_item_grab_reward";
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(optionId));
        Assert.Contains(optionId, ActionQueueCompiler.StepCompilerOptionIds);
        Assert.DoesNotContain(
            optionId,
            OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    private static OptionAvailabilityEnvelope Availability(
        SnapshotEnvelope snapshot) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "recovery.stabilize_day" },
            includeExecutorCalibrationOptions: true);

    private static SnapshotEnvelope Snapshot(
        bool canAcceptReward,
        bool includeSafeDiscard = true)
    {
        var inventory = includeSafeDiscard
            ? """
              [
                {"slot_index":0,"qualified_item_id":"(O)388","stack":5,"quality":0,"is_empty":false,"is_object":true,"can_be_trashed":true,"protected_from_auto_sell":false,"sell_to_store_price":10},
                {"slot_index":1,"qualified_item_id":"(O)390","stack":1,"quality":0,"is_empty":false,"is_object":true,"can_be_trashed":true,"protected_from_auto_sell":false,"sell_to_store_price":30},
                {"slot_index":2,"qualified_item_id":"(T)Axe","stack":1,"quality":0,"is_empty":false,"is_object":false,"can_be_trashed":true,"protected_from_auto_sell":false,"sell_to_store_price":1},
                {"slot_index":3,"qualified_item_id":"(O)472","stack":1,"quality":0,"is_empty":false,"is_object":true,"can_be_trashed":true,"protected_from_auto_sell":true,"sell_to_store_price":1}
              ]
              """
            : """
              [
                {"slot_index":0,"qualified_item_id":"(T)Axe","stack":1,"quality":0,"is_empty":false,"is_object":false,"can_be_trashed":true,"protected_from_auto_sell":false,"sell_to_store_price":1},
                {"slot_index":1,"qualified_item_id":"(O)472","stack":1,"quality":0,"is_empty":false,"is_object":true,"can_be_trashed":true,"protected_from_auto_sell":true,"sell_to_store_price":1}
              ]
              """;
        var stateJson = $$"""
        {
          "time": {
            "time": {"value":1200,"status":"available"}
          },
          "player": {
            "location_id": {"value":"Mountain","status":"available"},
            "tile_x": {"value":10,"status":"available"},
            "tile_y": {"value":10,"status":"available"},
            "energy": {"value":270,"status":"available"},
            "inventory": {"value":{{inventory}},"status":"available"}
          },
          "menus": {
            "active_menu": {
              "value":{"is_open":true,"type":"ItemGrabMenu"},
              "status":"available"
            },
            "menu_specific_state": {
              "value":{
                "kind":"fishing_item_grab",
                "source":0,
                "context_type":"StardewValley.Tools.FishingRod",
                "essential":true,
                "held_item_present":false,
                "pending_item_count":1,
                "pending_items":[{
                  "slot_index":0,
                  "qualified_item_id":"(O)145",
                  "stack":1,
                  "quality":0,
                  "player_can_accept_whole_stack":{{canAcceptReward.ToString().ToLowerInvariant()}}
                }]
              },
              "status":"available"
            },
            "sleep_prompt_context": {
              "value":{"is_sleep_prompt":false},
              "status":"available"
            }
          },
          "current_location": {
            "home_context": {
              "value":{"home_location_id":"FarmHouse","current_location_is_home":false},
              "status":"available"
            }
          },
          "locations": {
            "collision_grid": {
              "value":{"location_id":"Mountain","width":1,"height":1,"notable_tiles":[]},
              "status":"available"
            },
            "route_action_branch_coverage": {
              "value":{"unsupported_for_route_training_count":0,"rows":[]},
              "status":"available"
            }
          }
        }
        """;
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(stateJson)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-29T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }
}
