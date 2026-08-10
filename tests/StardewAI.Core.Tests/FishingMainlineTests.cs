using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FishingMainlineTests
{
    [Fact]
    public void FishingHasOnlyCandidateDailyPlanCompilationPath()
    {
        Assert.True(DailyPlanCompiler.HasOptionCompiler("fishing.catch_fish"));
        Assert.False(ActionQueueCompiler.HasStepCompiler("fishing.catch_fish"));
        Assert.True(ActionQueueCompiler.HasStepCompiler("executor.catch_fish"));
    }

    [Fact]
    public void TransparentFishingRuleFlowsThroughCandidatePlanAndQueue()
    {
        var snapshot = Snapshot(BaseState());
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.catch_fish" });

        var option = Assert.Single(availability.Options);
        Assert.False(option.Available);
        Assert.True(option.ReadEligible);
        Assert.Equal("unbound", option.BindingStatus);
        Assert.Equal("not_evaluated", option.CompileStatus);
        var sourceCandidate = Assert.Single(option.EventCandidates);
        Assert.True(sourceCandidate.Available);
        Assert.Equal("catch_fish", sourceCandidate.Kind);
        Assert.Equal((1, 5), (sourceCandidate.TileX, sourceCandidate.TileY));
        Assert.Equal(string.Empty, sourceCandidate.QualifiedItemId);
        Assert.StartsWith("fishing:attempt:", sourceCandidate.CandidateId, StringComparison.Ordinal);
        AssertParameter(sourceCandidate.Parameters, "bobber_tile_x", "5");
        AssertParameter(sourceCandidate.Parameters, "bobber_tile_y", "5");
        AssertParameter(sourceCandidate.Parameters, "rod_slot_index", "0");
        AssertParameter(sourceCandidate.Parameters, "cast_direction", "1");
        AssertParameter(sourceCandidate.Parameters, "target_casting_power", "1");
        AssertParameter(sourceCandidate.Parameters, "max_cast_requested", "True");
        AssertParameter(sourceCandidate.Parameters, "outcome_distribution_complete", "True");
        AssertParameter(sourceCandidate.Parameters, "expected_qualified_item_id", string.Empty);
        Assert.Equal(new[] { "(O)145", "(O)168" }, Outcomes(sourceCandidate).Select(OutcomeItemId).OrderBy(value => value, StringComparer.Ordinal));

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var candidate = Assert.Single(ranked);
        Assert.StartsWith("distribution:", ParameterValue(candidate.Parameters, "rule_key"), StringComparison.Ordinal);

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, snapshot.StateHash);
        Assert.Equal(new[] { "move_to_tile", "catch_fish" }, plan.Steps.Select(step => step.Kind).ToArray());
        AssertParameter(plan.Steps[0].Parameters, "max_movement_tiles", "1");
        AssertParameter(plan.Steps[1].Parameters, "bobber_tile_x", "5");
        Assert.Contains("no_forced_catch_result", plan.Steps[1].SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        AssertParameter(queue.Items[0].NormalizedCommand.Parameters, "max_movement_tiles", "1");
        var catchItem = Assert.Single(queue.Items.Where(item => item.OptionId == "executor.catch_fish"));
        Assert.Empty(catchItem.BlockingReasons);
        AssertParameter(catchItem.NormalizedCommand.Parameters, "expected_qualified_item_id", string.Empty);
        AssertParameter(catchItem.NormalizedCommand.Parameters, "target_casting_power", "1");
        AssertParameter(catchItem.NormalizedCommand.Parameters, "max_cast_requested", "True");
        AssertParameter(catchItem.NormalizedCommand.Parameters, "outcome_distribution_complete", "True");
        var catchStep = Assert.Single(catchItem.NormalizedCommand.Steps);
        Assert.Equal("catch_fish", catchStep.StepType);
        Assert.Contains("Beach:stand(1,5):bobber(5,5):rod_slot=0", catchStep.Target);
    }

    [Fact]
    public void FractionalPlayerEnergyKeepsFishingCompilable()
    {
        var snapshot = Snapshot(BaseState().Replace(
            "\"energy\": {\"value\":250,",
            "\"energy\": {\"value\":330.1,"));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.catch_fish" });
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);

        var queue = new ActionQueueCompiler().Compile(
            new DailyPlanCompiler().Compile(ranked, snapshot.StateHash),
            snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Empty(queue.Items.Single(item => item.OptionId == "executor.catch_fish").BlockingReasons);
    }

    [Fact]
    public void LowPlayerEnergyBlocksFishingConservatively()
    {
        var snapshot = Snapshot(BaseState().Replace(
            "\"energy\": {\"value\":250,",
            "\"energy\": {\"value\":1,"));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.catch_fish" });
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);

        var queue = new ActionQueueCompiler().Compile(
            new DailyPlanCompiler().Compile(ranked, snapshot.StateHash),
            snapshot);

        var item = Assert.Single(queue.Items.Where(queueItem => queueItem.OptionId == "executor.catch_fish"));
        Assert.Equal("blocked", item.Status);
        Assert.Contains("fishing_energy_too_low", item.BlockingReasons);
    }

    [Fact]
    public void MissingPlayerEnergyBlocksFishingConservatively()
    {
        var baseSnapshot = Snapshot(BaseState());
        var baseAvailability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(baseSnapshot, new[] { "fishing.catch_fish" });
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), baseAvailability);
        var plan = new DailyPlanCompiler().Compile(ranked, baseSnapshot.StateHash);
        var snapshot = Snapshot(BaseState().Replace(
            "\"energy\": {\"value\":250,\"status\":\"available\",\"source\":{\"kind\":\"game_object\",\"path\":\"test\"},\"adapter\":\"test\",\"read_at_tick\":1,\"confidence\":1},",
            string.Empty));
        plan.StateHash = snapshot.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        var item = Assert.Single(queue.Items.Where(queueItem => queueItem.OptionId == "executor.catch_fish"));
        Assert.Equal("blocked", item.Status);
        Assert.Contains("fishing_energy_too_low", item.BlockingReasons);
    }

    [Fact]
    public void IncompleteRodSpecificOverrideContextBlocksCandidateUpstream()
    {
        var snapshot = Snapshot(BaseState()
            .Replace("\"complete\":true,\"failure\":null", "\"complete\":false,\"failure\":\"unknown_mod_override\"")
            .Replace("\"special_catch_sources_complete\":true", "\"special_catch_sources_complete\":false"));

        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.catch_fish" })
            .Options);

        Assert.False(option.Available);
        var blocked = Assert.Single(option.EventCandidates);
        Assert.False(blocked.Available);
        Assert.Contains("fishing_rod_context_incomplete", blocked.BlockReasons);
        Assert.Contains("no_available_fishing_candidates", option.BlockingReasons);
    }

    [Fact]
    public void QueueCompilerRejectsForgedNonFishableBobberTile()
    {
        var snapshot = Snapshot(BaseState());
        var action = new SmallModelActionEnvelope
        {
            StateHash = snapshot.StateHash,
            ModelOutputId = "forged-fishing",
            GoalId = "test",
            Actor = new StardewAI.Contracts.Execution.ActionActorRef
            {
                ActorId = "training-player",
                ActorType = "training_player",
                ControlSurface = "singleplayer_training_actor"
            },
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "catch",
                    OptionId = "executor.catch_fish",
                    Parameters = new[]
                    {
                        Parameter("location_id", "Beach"),
                        Parameter("stand_tile_x", "3"),
                        Parameter("stand_tile_y", "5"),
                        Parameter("bobber_tile_x", "6"),
                        Parameter("bobber_tile_y", "5"),
                        Parameter("rod_slot_index", "0"),
                        Parameter("rule_key", "Data/Locations:Beach#0:test_fish"),
                        Parameter("expected_qualified_item_id", "(O)145"),
                        Parameter("cast_direction", "1")
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(action, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("blocked", item.Status);
        Assert.Contains("fishing_distribution_key_required", item.BlockingReasons);
        Assert.Contains("fishing_outcome_distribution_incomplete", item.BlockingReasons);
        Assert.Contains("fishing_expected_item_must_be_unconstrained", item.BlockingReasons);
    }

    [Fact]
    public void QueueCompilerRejectsTruncatedOutcomeDistribution()
    {
        var snapshot = Snapshot(BaseState());
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.catch_fish" });
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var catchStep = Assert.Single(plan.Steps.Where(step => step.Kind == "catch_fish"));
        Assert.Single(catchStep.Parameters.Where(parameter => parameter.Name == "outcome_distribution_json")).Value = "[]";

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items.Where(queueItem => queueItem.OptionId == "executor.catch_fish"));
        Assert.Equal("blocked", item.Status);
        Assert.Contains("fishing_rule_context_no_longer_allows_candidate", item.BlockingReasons);
    }

    [Fact]
    public void DeterministicFishFrenzySupersedesNormalRuleAtSameBobberTile()
    {
        var snapshot = Snapshot(BaseState().Replace(
            "\"fish_frenzy\":{\"active\":false,\"qualified_item_id\":null,\"eligible_fishable_tile_indices\":[]}",
            "\"fish_frenzy\":{\"active\":true,\"qualified_item_id\":\"(O)128\",\"eligible_fishable_tile_indices\":[0]}"));

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.catch_fish" });
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.StartsWith("fishing:attempt:", candidate.CandidateId, StringComparison.Ordinal);
        Assert.Equal(string.Empty, candidate.QualifiedItemId);
        var outcome = Assert.Single(Outcomes(candidate));
        Assert.Equal("(O)128", OutcomeItemId(outcome));
        Assert.Equal("fish_frenzy", outcome.GetProperty("source_key").GetString());
        AssertParameter(candidate.Parameters, "result_is_stochastic", "False");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(queue.Items.Single(item => item.OptionId == "executor.catch_fish").BlockingReasons);
    }

    [Fact]
    public void MineAreaEightyExpandsSpecialFishJellyAndTrashWithoutNormalFallback()
    {
        var snapshot = Snapshot(MineState(mineArea: 80, specialFishId: "(O)162", specialChance: 0.2, caveJellyChance: 0.25));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.catch_fish" });
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        var outcomes = Outcomes(candidate);

        Assert.Equal(8, outcomes.Length);
        Assert.DoesNotContain(outcomes, outcome => OutcomeItemId(outcome) == "(O)145");
        Assert.Equal(0.2, Outcome(outcomes, "(O)162").GetProperty("chance_preview").GetDouble());
        Assert.Equal(0.2, Outcome(outcomes, "(O)CaveJelly").GetProperty("chance_preview").GetDouble());
        Assert.All(outcomes.Where(outcome => OutcomeItemId(outcome) is not "(O)162" and not "(O)CaveJelly"),
            outcome => Assert.Equal(0.1, outcome.GetProperty("chance_preview").GetDouble()));

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(queue.Items.Single(item => item.OptionId == "executor.catch_fish").BlockingReasons);
    }

    [Fact]
    public void MineNormalFallbackChanceExcludesSpecialFishBranchWeight()
    {
        var snapshot = Snapshot(MineState(mineArea: 40, specialFishId: "(O)161", specialChance: 0.2, caveJellyChance: 0));
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.catch_fish" })
            .Options);

        var outcomes = Outcomes(Assert.Single(option.EventCandidates));
        Assert.Equal(0.2, Outcome(outcomes, "(O)161").GetProperty("chance_preview").GetDouble());
        Assert.Equal(0.16, Outcome(outcomes, "(O)145").GetProperty("chance_preview").GetDouble());
        Assert.Contains(outcomes, outcome => OutcomeItemId(outcome) == "(O)168");
    }

    [Fact]
    public void BaseTrashFallbackIsExplicitAndCompilesThroughSpecialContextGate()
    {
        var snapshot = Snapshot(BaseState());
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.catch_fish" });
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        var fallback = Outcome(Outcomes(candidate), "(O)168");

        Assert.Equal("base_fallback", fallback.GetProperty("source_key").GetString());
        Assert.Equal("unresolved_composed_fallthrough", fallback.GetProperty("probability_status").GetString());
        AssertParameter(candidate.Parameters, "outcome_probability_status", "partial_unknown_fallthrough");
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var queue = new ActionQueueCompiler().Compile(
            new DailyPlanCompiler().Compile(ranked, snapshot.StateHash),
            snapshot);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(queue.Items.Single(item => item.OptionId == "executor.catch_fish").BlockingReasons);
    }

    [Fact]
    public void CollectedIslandPoolReservesPoolTileWithoutInventingNormalCatch()
    {
        const string handler = "\"location_get_fish_override\":{\"present\":true,\"transparent_handler_available\":true,\"handlers\":[{\"handler\":\"island_southeast_stardrop_pool_walnut\",\"fishable_tile_indices\":[0],\"eligible_before_catch\":false,\"qualified_item_id\":\"(O)73\",\"matched_pool_without_reward_returns_null\":true}]}";
        var snapshot = Snapshot(BaseState().Replace(
            "\"location_get_fish_override\":{\"present\":false,\"transparent_handler_available\":true,\"handlers\":[]}",
            handler));

        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.catch_fish" })
            .Options);
        Assert.False(option.Available);
        var blocked = Assert.Single(option.EventCandidates);
        Assert.False(blocked.Available);
        Assert.Contains("no_reachable_eligible_fishing_output", blocked.BlockReasons);
    }

    [Fact]
    public void RailroadNecklaceUsesTheExistingSpecialCatchCompiler()
    {
        const string handler = "\"location_get_fish_override\":{\"present\":true,\"transparent_handler_available\":true,\"handlers\":[{\"handler\":\"railroad_carolines_necklace\",\"eligible_before_catch\":true,\"qualified_item_id\":\"(O)191\",\"required_secret_note_index\":25,\"necklace_mail_already_received_or_pending\":false,\"catch_side_effects\":[\"add_carolines_necklace_mail_for_tomorrow\",\"add_quest_128\",\"add_quest_129\"]}]}";
        var state = BaseState()
            .Replace("Beach", "Railroad")
            .Replace(
                "\"location_get_fish_override\":{\"present\":false,\"transparent_handler_available\":true,\"handlers\":[]}",
                handler);
        var snapshot = Snapshot(state);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.catch_fish" });

        var option = Assert.Single(availability.Options);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(",", candidate.BlockReasons));
        Assert.Equal("catch_fish", candidate.Kind);
        Assert.Equal(string.Empty, candidate.QualifiedItemId);
        Assert.Contains(
            "railroad_carolines_necklace",
            ParameterValue(candidate.Parameters, "outcome_distribution_json"),
            StringComparison.Ordinal);
        AssertParameter(candidate.Parameters, "possible_qualified_item_ids_json", "[\"(O)191\"]");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var catchItem = Assert.Single(queue.Items.Where(item => item.OptionId == "executor.catch_fish"));
        Assert.Empty(catchItem.BlockingReasons);
        AssertParameter(catchItem.NormalizedCommand.Parameters, "expected_qualified_item_id", string.Empty);
        AssertParameter(catchItem.NormalizedCommand.Parameters, "possible_qualified_item_ids_json", "[\"(O)191\"]");
    }

    [Fact]
    public void FullInventoryBlocksFishingBeforeAOneShotSpecialCatchCanOpenItemGrabMenu()
    {
        var state = BaseState().Replace(
            "\"occupied_item_stacks\":1,\"empty_slots\":11,\"has_empty_slot\":true",
            "\"occupied_item_stacks\":12,\"empty_slots\":0,\"has_empty_slot\":false");
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(Snapshot(state), new[] { "fishing.catch_fish" })
            .Options);

        Assert.False(option.Available);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("fishing_inventory_full_requires_storage_transfer", candidate.BlockReasons);
    }

    [Fact]
    public void VanillaSecretNoteResolverChanceParticipatesInCandidateProbability()
    {
        var state = BaseState()
            .Replace("\"resolution_status\":\"direct_item\"", "\"resolution_status\":\"vanilla_secret_note_or_item\",\"output_local_chance_preview\":0.5")
            .Replace("\"data_fish_chance_by_water_depth\":[{\"water_depth\":3,\"chance_preview\":0.4}]", "\"data_fish_chance_by_water_depth\":[]");
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(Snapshot(state), new[] { "fishing.catch_fish" })
            .Options);

        var note = Outcome(Outcomes(Assert.Single(option.EventCandidates)), "(O)145");
        Assert.Equal(0.25, note.GetProperty("chance_preview").GetDouble());
    }

    [Fact]
    public void ResourceCollectionQuestBindsFishingTrashDistributionAsSourceAttempt()
    {
        var snapshot = Snapshot(WithQuestState(
            MineState(80, "(O)162", 0.2, 0.05),
            """
            [{
              "id":"96","quest_type":10,"runtime_type":"ResourceCollectionQuest",
              "accepted":true,"completed":false,
              "per_type_fields":{
                "available":true,"item_id":"(O)168","target_npc":"Robin",
                "number_collected":2,"number_required":10,
                "target_count":10,"current_count":2
              }
            }]
            """,
            "[]"));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("catch_fish", candidate.Kind);
        AssertParameter(candidate.Parameters, "quest_acquisition_source_step", "true");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var catchItem = Assert.Single(queue.Items.Where(item =>
            item.OptionId == "executor.catch_fish"));

        Assert.Equal("pending", queue.Status);
        Assert.Empty(catchItem.BlockingReasons);
        AssertParameter(
            catchItem.NormalizedCommand.Parameters,
            "quest_acquisition_source_step",
            "true");
    }

    [Fact]
    public void SpecialOrderCollectBindsFishingTrashByNativeContextTags()
    {
        var snapshot = Snapshot(WithQuestState(
            MineState(80, "(O)162", 0.2, 0.05),
            "[]",
            """
            [{
              "quest_key":"TrashOrder","quest_name":"Trash Order","quest_state":"InProgress",
              "objectives":[{
                "description":"Collect trash","current_count":0,"max_count":5,
                "runtime_type":"CollectObjective","fail_on_completion":false,"complete":false,
                "per_type_fields":{"available":true,"acceptable_context_tag_sets":["trash_item"]}
              }],
              "rewards":[]
            }]
            """));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("catch_fish", candidate.Kind);
        AssertParameter(candidate.Parameters, "quest_acquisition_source_step", "true");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var catchItem = Assert.Single(queue.Items.Where(item =>
            item.OptionId == "executor.catch_fish"));

        Assert.Equal("pending", queue.Status);
        Assert.Empty(catchItem.BlockingReasons);
    }

    [Fact]
    public void FishingQuestMatchesAggregatedOutcomeDistribution()
    {
        var snapshot = Snapshot(WithQuestState(
            BaseState(),
            """
            [{
              "id":"7","quest_type":7,"runtime_type":"FishingQuest",
              "accepted":true,"completed":false,
              "per_type_fields":{
                "available":true,"item_id":"(O)145",
                "number_fished":0,"number_to_fish":3,
                "target_count":3,"current_count":0
              }
            }]
            """,
            "[]"));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "quest.advance" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("catch_fish", candidate.Kind);
    }

    internal static string BaseState()
    {
        return """
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Beach","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":5,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":250,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"max_items":12,"occupied_item_stacks":1,"empty_slots":11,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"qualified_item_id":"(T)BambooPole"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "map": {"value":{"location_id":"Beach","width":12,"height":12},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Beach","width":12,"height":12,"notable_tiles":[{"tile_x":5,"tile_y":5,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "fishing": {
            "location_context": {"value":{"location_id":"Beach","can_fish_here":true,"map_width":12,"map_height":12,"fishing_level":0},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "fishable_tiles": {"value":[{"tile_x":5,"tile_y":5,"water_depth":3,"fish_area_id":"ocean"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "rod_inventory": {"value":[{"slot_index":0,"selected":false,"qualified_item_id":"(T)BambooPole","upgrade_level":0,"in_use":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_cast_state": {"value":{"rod_selected":false,"in_use":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "rod_contexts": {"value":[{
              "rod_slot_index":0,
              "rod_qualified_item_id":"(T)BambooPole",
              "rod_upgrade_level":0,
              "selected":false,
              "complete":true,"failure":null,
              "special_catch_sources_complete":true,
              "special_catch_sources":{"location_get_fish_override":{"present":false,"transparent_handler_available":true,"handlers":[]},"fish_ponds":[],"fish_frenzy":{"active":false,"qualified_item_id":null,"eligible_fishable_tile_indices":[]},"fallbacks":{"tutorial_location_data_fallback_qualified_item_id":"(O)145","no_location_data_match_qualified_item_id":"(O)168"}},
              "spawn_rules":{"item_query_resolution_complete":true,"unresolved_rule_keys":[],"evaluation_context":{"is_tutorial_catch":false},"rules":[{
                "rule_key":"Data/Locations:Beach#0:test_fish",
                "source":"Data/Locations:Beach",
                "source_index":0,
                "id":"test_fish",
                "condition_met":true,
                "player_position":null,
                "effective_spawn_chance_preview":0.5,
                "eligible_before_random_rolls":true,
                "blocking_reasons":[],
                "eligible_fishable_tile_indices":[0],
                "outputs":[{
                  "output_index":0,
                  "resolution_complete":true,
                  "resolution_status":"direct_item",
                  "item_id":"145",
                  "qualified_item_id":"(O)145",
                  "output_eligible_before_random_rolls":true,
                  "output_blocking_reasons":[],
                  "data_fish_chance_by_water_depth":[{"water_depth":3,"chance_preview":0.4}]
                }]
              }]}
            }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
    }

    private static string MineState(int mineArea, string specialFishId, double specialChance, double caveJellyChance)
    {
        var handler = $$"""
        "location_get_fish_override":{"present":true,"transparent_handler_available":true,"handlers":[{"handler":"mine_shaft_fishing","mine_area":{{mineArea}},"uses_training_rod":false,"has_curiosity_lure":false,"special_fish_qualified_item_id":"{{specialFishId}}","special_fish_output":{"qualified_item_id":"{{specialFishId}}","context_tags":["category_fish"],"context_tags_projection_status":"exact_item_get_context_tags"},"special_fish_chance_by_water_depth":[{"water_depth":3,"special_fish_chance":{{specialChance.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}],"lava_area_cave_jelly_chance":{{caveJellyChance.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"cave_jelly_output":{"qualified_item_id":"(O)CaveJelly","context_tags":["category_fish"],"context_tags_projection_status":"exact_item_get_context_tags"},"mine_trash_item_id_range_inclusive":[167,172],"mine_trash_outputs":[{"qualified_item_id":"(O)167","context_tags":["trash_item"],"context_tags_projection_status":"exact_item_get_context_tags"},{"qualified_item_id":"(O)168","context_tags":["trash_item"],"context_tags_projection_status":"exact_item_get_context_tags"},{"qualified_item_id":"(O)169","context_tags":["trash_item"],"context_tags_projection_status":"exact_item_get_context_tags"},{"qualified_item_id":"(O)170","context_tags":["trash_item"],"context_tags_projection_status":"exact_item_get_context_tags"},{"qualified_item_id":"(O)171","context_tags":["trash_item"],"context_tags_projection_status":"exact_item_get_context_tags"},{"qualified_item_id":"(O)172","context_tags":["trash_item"],"context_tags_projection_status":"exact_item_get_context_tags"}]}]}
        """;
        return BaseState().Replace(
            "\"location_get_fish_override\":{\"present\":false,\"transparent_handler_available\":true,\"handlers\":[]}",
            handler);
    }

    private static string WithQuestState(
        string state,
        string activeQuestsJson,
        string specialOrdersJson)
    {
        var questState = $$"""
          "quests": {
            "active_quests":{"value":{{activeQuestsJson}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "special_orders":{"value":{{specialOrdersJson}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "completed_special_orders":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "accepted_special_order_types":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "mail_received":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "community_center":{"value":{},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "achievements":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "fishing": {
        """;
        return state.Replace(
            "\"fishing\": {",
            questState,
            StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(string stateJson)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-13T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SmallModelActionParameter Parameter(string name, string value) => new() { Name = name, Value = value };

    private static JsonElement[] Outcomes(EventCandidate candidate)
    {
        using var document = JsonDocument.Parse(ParameterValue(candidate.Parameters, "outcome_distribution_json"));
        return document.RootElement.EnumerateArray().Select(outcome => outcome.Clone()).ToArray();
    }

    private static JsonElement Outcome(IEnumerable<JsonElement> outcomes, string qualifiedItemId)
    {
        return Assert.Single(outcomes.Where(outcome => OutcomeItemId(outcome) == qualifiedItemId));
    }

    private static string OutcomeItemId(JsonElement outcome)
    {
        return outcome.GetProperty("qualified_item_id").GetString() ?? string.Empty;
    }

    private static string ParameterValue(IEnumerable<SmallModelActionParameter> parameters, string name)
    {
        return Assert.Single(parameters.Where(parameter => parameter.Name == name)).Value;
    }

    private static void AssertParameter(IEnumerable<SmallModelActionParameter> parameters, string name, string value)
    {
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
