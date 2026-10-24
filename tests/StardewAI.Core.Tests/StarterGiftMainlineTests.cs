using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class StarterGiftMainlineTests
{
    [Fact]
    public void StarterGiftIsFiveGateVerifiedAndTrainingEligible()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired("rewards.claim_starter_gift");

        Assert.Equal(CapabilityCompilerStatus.StepAndParameterCompilerDeclared, declaration.CompilerStatus);
        Assert.True(declaration.HarnessDispatchSupported);
        Assert.True(declaration.ProductExecutorSupported);
        Assert.True(declaration.AutonomousCandidateEnabled);
        Assert.True(TrainingEligibilityPolicy.IsEligible(declaration));
        Assert.Equal(new[] { "EVD-342" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-342" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-342" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-342" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-342" }, declaration.OutputEvidenceIds);
        Assert.Empty(declaration.TrainingExclusionReasons);
        Assert.Equal(
            ImplementationEngineIds.InventoryTransfer,
            OptionImplementationCatalog.GetRequired(declaration.OptionId).PrimaryEngineId);
    }

    [Fact]
    public void ReadyStarterGiftCompilesFromParameterlessIntent()
    {
        var snapshot = Snapshot("ready", true);
        var request = new SmallModelActionEnvelope
        {
            ModelOutputId = "starter-gift.intent",
            SourceModel = "test",
            StateHash = snapshot.StateHash,
            GoalId = "goal.grandpa_max_score_year3",
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
                    ActionId = "claim.starter-gift",
                    OptionId = "rewards.claim_starter_gift",
                    Rationale = "unlock the fresh-save crop-production path"
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("claim_starter_gift", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "quest_id" && parameter.Value == "6");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "stand_tile_x" && parameter.Value == "3");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "stand_tile_y" && parameter.Value == "8");
    }

    [Fact]
    public void TransparentStarterGiftCandidateFlowsThroughDailyPlan()
    {
        var snapshot = Snapshot("ready", true);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "rewards.claim_starter_gift" },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.True(candidate.AllowedNow);
        Assert.True(candidate.AllowedToday);
        Assert.Equal("claim_starter_gift", candidate.Kind);
        Assert.Contains("fresh_snapshot_replan_required=true", candidate.ExpectedEffect);

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);

        Assert.Equal("claim_starter_gift", step.Kind);
        Assert.Contains("native_GameLocation.checkAction_only", step.SafetyConstraints);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("rewards.claim_starter_gift", Assert.Single(queue.Items).OptionId);
    }

    [Fact]
    public void MissingOrUnownedStarterGiftFailsClosed()
    {
        var snapshot = Snapshot("blocked_farmhouse_not_owned_by_current_player", false);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "rewards.claim_starter_gift" },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains(candidate.BlockReasons, reason => reason.StartsWith("starter_gift_not_ready", StringComparison.Ordinal));

        var request = new SmallModelActionEnvelope
        {
            ModelOutputId = "starter-gift.blocked",
            SourceModel = "test",
            StateHash = snapshot.StateHash,
            GoalId = "goal.grandpa_max_score_year3",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
            Actions = new[] { new SmallModelAction { ActionId = "claim.starter-gift", OptionId = "rewards.claim_starter_gift" } }
        };
        var queue = new ActionQueueCompiler().Compile(request, snapshot);
        Assert.Equal("blocked", queue.Status);
        Assert.Contains(queue.Items.Single().BlockingReasons, reason => reason.StartsWith("starter_gift_not_ready_by_transparent_state", StringComparison.Ordinal));
    }

    [Fact]
    public void GrandpaMoneyDirectionOwnsStarterGiftAsFreshSaveSupportingTransition()
    {
        var money = GrandpaDirectionCatalog.Entries.Single(entry => entry.DirectionId == "earn_money");
        Assert.Contains("rewards.claim_starter_gift", money.PermittedOptionIds);
        Assert.Contains("claim_starter_gift", money.PermittedCandidateKinds);
    }

    private static SnapshotEnvelope Snapshot(string status, bool present)
    {
        const string outputJson = "[{\"runtimeType\":\"StardewValley.Object\",\"qualifiedItemId\":\"(O)472\",\"quality\":0,\"unitStateSha256\":\"starter-seed-state\",\"quantity\":15}]";
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "current_location": {
            "objects":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "starter_gift_claim":{"value":{
              "status":"{{{status}}}","location_id":"FarmHouse","location_runtime_type":"StardewValley.Locations.FarmHouse",
              "is_farmhouse":true,"farmhouse_owned_by_current_player":{{{present.ToString().ToLowerInvariant()}}},
              "target_tile_x":3,"target_tile_y":7,"target_runtime_type":"StardewValley.Objects.Chest","qualified_item_id":"(BC)130",
              "exact_starter_gift_present":{{{present.ToString().ToLowerInvariant()}}},"content_slot_count":1,
              "expected_output_items_json":{{{JsonSerializer.Serialize(outputJson)}}},"inventory_accepts_all_outputs":true,
              "expected_quest_id":"6","expected_quest_present_before":false,"expected_quest_present_after":true,"farm_type":0,
              "stand_tiles":[{"tile_x":3,"tile_y":8,"on_map":true,"collision_blocked":false,"available":true}],
              "interaction_kind":"location_object","expected_action_type":"StarterGiftChest",
              "native_contract":"FarmHouse_owned_by_current_player->Chest.checkForAction_giftbox_starter->dumpContents_addQuest_and_addItemByMenuIfNecessaryElseHoldUp->remove_empty_giftbox",
              "projection_fingerprint":"starter-gift-test-fingerprint"
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-24T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
