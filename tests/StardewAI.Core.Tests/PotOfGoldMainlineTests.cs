using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class PotOfGoldMainlineTests
{
    [Fact]
    public void PotOfGoldClaimClosesFiveEvidenceGatesAndIsTrainingEligibleTests()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("rewards.claim_pot_of_gold");

        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-268" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-268" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-268" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-268" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-268" }, declaration.OutputEvidenceIds);
        Assert.Contains(declaration.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(
            ImplementationEngineIds.InventoryTransfer,
            OptionImplementationCatalog.GetRequired(declaration.OptionId).PrimaryEngineId);
    }

    [Fact]
    public void ExactSpringSeventeenPotCompilesFromNoParameterIntent()
    {
        var snapshot = Snapshot("ready", true);
        var request = new SmallModelActionEnvelope
        {
            ModelOutputId = "pot.intent",
            SourceModel = "test",
            StateHash = snapshot.StateHash,
            GoalId = "goal.autonomous.singleplayer",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "claim.pot",
                    OptionId = "rewards.claim_pot_of_gold",
                    Rationale = "claim expiring native reward"
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("claim_pot_of_gold", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "quantity" && parameter.Value == "9");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "stand_tile_x" && parameter.Value == "52");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "stand_tile_y" && parameter.Value == "97");
    }

    [Fact]
    public void TransparentCandidateFlowsThroughDailyPlanWithoutInventingPickupReceipt()
    {
        var snapshot = Snapshot("ready", true);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "rewards.claim_pot_of_gold" },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("claim_pot_of_gold", candidate.Kind);
        Assert.Contains("fresh_snapshot_pickup_handoff=true", candidate.ExpectedEffect);

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("claim_pot_of_gold", step.Kind);
        Assert.Contains(step.SafetyConstraints, value => value.Contains("shared_pickup_executor", StringComparison.Ordinal));
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("rewards.claim_pot_of_gold", Assert.Single(queue.Items).OptionId);
        Assert.DoesNotContain(queue.Items.Single().NormalizedCommand.Steps, compiled => compiled.StepType == "pickup_debris");
    }

    [Fact]
    public void WrongDateOrMissingPotFailsClosedUpstreamAndAtCompiler()
    {
        var snapshot = Snapshot("not_spring_17", false);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "rewards.claim_pot_of_gold" },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains(candidate.BlockReasons, reason => reason.StartsWith("pot_of_gold_not_ready", StringComparison.Ordinal));

        var request = new SmallModelActionEnvelope
        {
            ModelOutputId = "pot.bad-date",
            SourceModel = "test",
            StateHash = snapshot.StateHash,
            GoalId = "goal.autonomous.singleplayer",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
            Actions = new[] { new SmallModelAction { ActionId = "claim.pot", OptionId = "rewards.claim_pot_of_gold" } }
        };
        var queue = new ActionQueueCompiler().Compile(request, snapshot);
        Assert.Equal("blocked", queue.Status);
        Assert.Contains(queue.Items.Single().BlockingReasons, reason => reason.StartsWith("pot_of_gold_not_ready_by_transparent_state", StringComparison.Ordinal));
    }

    private static SnapshotEnvelope Snapshot(string status, bool present)
    {
        const string outputJson = "[{\"qualified_item_id\":\"(O)GoldCoin\",\"quantity\":9,\"quality\":0,\"delivery\":\"individual_item_debris\"},{\"qualified_item_id\":\"(H)LeprechuanHat\",\"quantity\":1,\"quality\":0,\"delivery\":\"item_debris\"}]";
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"Forest","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":50,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":97,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "current_location": {
            "debris":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "pot_of_gold_reward":{"value":{
              "status":"{{{status}}}","location_id":"Forest","current_season":"spring","current_day":17,"current_year":2,
              "target_tile_x":52,"target_tile_y":98,"exact_object_present":{{{present.ToString().ToLowerInvariant()}}},
              "qualified_item_id":"(O)PotOfGold","target_runtime_type":"StardewValley.Object","object_type":"interactive","object_stack":1,
              "stand_tiles":[{"tile_x":52,"tile_y":97,"on_map":true,"collision_blocked":false,"available":true},{"tile_x":51,"tile_y":98,"on_map":true,"collision_blocked":false,"available":true}],
              "reward_branch":"spring_17_forest_pot_of_gold","expected_coin_quantity":9,"expected_hat_quantity":1,
              "expected_output_items_json":{{{JsonSerializer.Serialize(outputJson)}}},
              "interaction_kind":"location_object","expected_action_type":"PotOfGold",
              "native_contract":"Forest.DayUpdate_spring_17_tile_52_98->Object.checkForAction_PotOfGold->removeObject_and_createMultipleItemDebris"
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-27T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
