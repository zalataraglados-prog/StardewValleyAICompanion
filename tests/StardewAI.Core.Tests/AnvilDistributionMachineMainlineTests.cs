using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class
    CandidateOptionAvailabilityEvaluatorTests
{
    [Fact]
    public void AnvilCompleteDistributionIsAExecutableCandidate()
    {
        var snapshot = Snapshot(
            AnvilDistributionTestFixture.StateJson);

        var option =
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.process_machines" },
                    includeExecutorCalibrationOptions:
                        true)
                .Options[0];

        var candidate = Assert.Single(
            option.EventCandidates.Where(row =>
                row.Kind ==
                "load_machine_input_tile"));
        Assert.True(candidate.Available);
        Assert.Empty(candidate.BlockReasons);
        Assert.Contains(
            "machine_output_prediction_status=machine_distribution_probe_available",
            candidate.ExpectedEffect);
        Assert.Contains(
            "machine_prediction_training_kind=complete_distribution",
            candidate.ExpectedEffect);
        Assert.Contains(
            "machine_special_prediction_model_id=anvil_trinket_reforge_distribution.v1",
            candidate.ExpectedEffect);
        Assert.Contains(
            "machine_output_distribution_outcome_kind=iridium_spur",
            candidate.ExpectedEffect);
        Assert.Contains(
            "machine_additional_consumed_items=(O)337:3",
            candidate.ExpectedEffect);
        Assert.Contains(
            "machine_additional_consumed_available=(O)337:3",
            candidate.ExpectedEffect);
        Assert.Contains(
            "machine_additional_consumed_total_value=3000",
            candidate.ExpectedEffect);
        Assert.Contains(
            "predicted_output_net_value=-3000",
            candidate.ExpectedEffect);
        Assert.Contains(
            "anvil_reforge_utility_metric=critical_hit_speed_buff_duration",
            candidate.ExpectedEffect);
        Assert.Contains(
            "anvil_reforge_current_utility=0",
            candidate.ExpectedEffect);
        Assert.Contains(
            "anvil_reforge_expected_utility=0.5",
            candidate.ExpectedEffect);
        Assert.Contains(
            "anvil_reforge_improvement_probability=0.83333333",
            candidate.ExpectedEffect);
        Assert.Contains(
            "anvil_reforge_decision_class=expected_improvement",
            candidate.ExpectedEffect);
        Assert.Contains(
            "anvil_reforge_capability_class=critical_hit_mobility",
            candidate.ExpectedEffect);
        Assert.Contains(
            "anvil_reforge_loadout_relation=empty_unlocked_slot_available",
            candidate.ExpectedEffect);

        var fingerprint = ReadExpectedEffectValue(
            candidate.ExpectedEffect,
            "machine_prediction_contract_fingerprint");
        Assert.Equal(64, fingerprint.Length);
        Assert.All(
            fingerprint,
            character => Assert.True(
                Uri.IsHexDigit(character)));
    }

    [Fact]
    public void UnvettedDistributionModelRemainsBlocked()
    {
        var snapshot = Snapshot(
            AnvilDistributionTestFixture.StateJson
                .Replace(
                    MachinePredictionTrainingPolicy
                        .AnvilModelId,
                    "unvetted.random.machine.v1",
                    StringComparison.Ordinal));

        var candidate =
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.process_machines" },
                    includeExecutorCalibrationOptions:
                        true)
                .Options[0]
                .EventCandidates
                .Single(row =>
                    row.Kind ==
                    "load_machine_input_tile");

        Assert.False(candidate.Available);
        Assert.Contains(
            "machine_output_not_trainable",
            candidate.BlockReasons);
    }

    [Fact]
    public void DistributionWithoutCurrentOutcomeRemainsBlocked()
    {
        var snapshot = Snapshot(
            AnvilDistributionTestFixture.StateJson
                .Replace(
                    """
                    "input":{"current_outcome":{"stat":"GeneralStat","value":5}},
                    """,
                    """
                    "input":{},
                    """,
                    StringComparison.Ordinal));

        var candidate =
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.process_machines" },
                    includeExecutorCalibrationOptions:
                        true)
                .Options[0]
                .EventCandidates
                .Single(row =>
                    row.Kind ==
                    "load_machine_input_tile");

        Assert.False(candidate.Available);
        Assert.Contains(
            "machine_output_not_trainable",
            candidate.BlockReasons);
    }

    [Fact]
    public void DistributionWithoutExactLoadoutContextRemainsBlocked()
    {
        var snapshot = Snapshot(
            AnvilDistributionTestFixture.StateJson
                .Replace(
                    """
                    "trinket_loadout": {"value":{"schema_version":"trinket_loadout_context.v1","status":"available_exact_live_slots","unlocked_slot_count":1,"occupied_slot_count":0,"empty_unlocked_slot_count":1,"slots":[{"slot_index":0,"unlocked":true,"occupied":false,"item_id":"","qualified_item_id":"","runtime_type":"","special_state":null}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                    """,
                    string.Empty,
                    StringComparison.Ordinal));

        var candidate =
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    snapshot,
                    new[] { "farm.process_machines" },
                    includeExecutorCalibrationOptions:
                        true)
                .Options[0]
                .EventCandidates
                .Single(row =>
                    row.Kind ==
                    "load_machine_input_tile");

        Assert.False(candidate.Available);
        Assert.Contains(
            "anvil_reforge_loadout_context_unavailable",
            candidate.BlockReasons);
    }

    private static string ReadExpectedEffectValue(
        string expectedEffect,
        string key)
    {
        return expectedEffect
            .Split(';')
            .Select(segment =>
                segment.Split(
                    new[] { '=' },
                    2))
            .Where(parts =>
                parts.Length == 2 &&
                parts[0] == key)
            .Select(parts => parts[1])
            .FirstOrDefault() ??
            string.Empty;
    }
}

public sealed partial class ActionQueueCompilerTests
{
    [Fact]
    public void CompileAcceptsBoundAnvilDistributionContract()
    {
        var snapshot = Snapshot(
            AnvilDistributionTestFixture.StateJson);
        var request = AnvilRequest(
            snapshot,
            AnvilDistributionTestFixture
                .ReadContract(snapshot)
                .Fingerprint);

        var queue = new ActionQueueCompiler()
            .Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(
            item.NormalizedCommand.Steps);
        Assert.Contains(
            "machine_prediction_training_kind=complete_distribution",
            step.ExpectedEffect);
        Assert.Contains(
            "machine_output_distribution_outcome_kind=iridium_spur",
            step.ExpectedEffect);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tampered")]
    public void CompileBlocksUnboundAnvilDistributionContract(
        string fingerprint)
    {
        var snapshot = Snapshot(
            AnvilDistributionTestFixture.StateJson);
        var request = AnvilRequest(
            snapshot,
            fingerprint);

        var queue = new ActionQueueCompiler()
            .Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "load_machine_input_distribution_contract_mismatch",
            queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksTamperedAnvilUtilityProjection()
    {
        var snapshot = Snapshot(
            AnvilDistributionTestFixture.StateJson);
        var request = AnvilRequest(
            snapshot,
            AnvilDistributionTestFixture
                .ReadContract(snapshot)
                .Fingerprint);
        request.Actions[0]
            .Parameters
            .Single(parameter =>
                parameter.Name ==
                "anvil_reforge_current_utility")
            .Value = "1";

        var queue = new ActionQueueCompiler()
            .Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "load_machine_input_distribution_contract_mismatch",
            queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksTamperedAnvilLoadoutProjection()
    {
        var snapshot = Snapshot(
            AnvilDistributionTestFixture.StateJson);
        var request = AnvilRequest(
            snapshot,
            AnvilDistributionTestFixture
                .ReadContract(snapshot)
                .Fingerprint);
        request.Actions[0]
            .Parameters
            .Single(parameter =>
                parameter.Name ==
                "anvil_reforge_empty_unlocked_slot_count")
            .Value = "0";

        var queue = new ActionQueueCompiler()
            .Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "load_machine_input_distribution_contract_mismatch",
            queue.Items[0].BlockingReasons);
    }

    private static SmallModelActionEnvelope AnvilRequest(
        StardewAI.Contracts.State.SnapshotEnvelope snapshot,
        string fingerprint)
    {
        var prediction =
            AnvilDistributionTestFixture
                .ReadPrediction(snapshot);
        var utility =
            AnvilReforgeUtilityProjection.Read(
                prediction);
        var loadout =
            AnvilReforgeLoadoutProjection.Read(
                snapshot,
                "iridium_spur");
        var request = Request(
            snapshot.StateHash,
            "executor.load_machine_input");
        request.Actions[0].Parameters = new[]
        {
            Parameter("target_tile_x", "64"),
            Parameter("target_tile_y", "15"),
            Parameter("target_location", "Farm"),
            Parameter("machine_location_id", "Farm"),
            Parameter("input_slot_index", "0"),
            Parameter(
                "qualified_item_id",
                "(TR)IridiumSpur"),
            Parameter(
                "machine_prediction_training_kind",
                "complete_distribution"),
            Parameter(
                "machine_special_prediction_model_id",
                MachinePredictionTrainingPolicy
                    .AnvilModelId),
            Parameter(
                "machine_output_distribution_outcome_kind",
                "iridium_spur"),
            Parameter(
                "machine_prediction_contract_fingerprint",
                fingerprint),
            Parameter(
                "anvil_reforge_utility_status",
                utility.Status),
            Parameter(
                "anvil_reforge_utility_metric",
                utility.MetricId),
            Parameter(
                "anvil_reforge_utility_ordering",
                utility.Ordering),
            Parameter(
                "anvil_reforge_current_utility",
                AnvilReforgeUtilityProjection.Format(
                    utility.CurrentUtility)),
            Parameter(
                "anvil_reforge_expected_utility",
                AnvilReforgeUtilityProjection.Format(
                    utility.ExpectedUtility)),
            Parameter(
                "anvil_reforge_expected_utility_delta",
                AnvilReforgeUtilityProjection.Format(
                    utility.ExpectedDelta)),
            Parameter(
                "anvil_reforge_improvement_probability",
                AnvilReforgeUtilityProjection.Format(
                    utility.ImprovementProbability)),
            Parameter(
                "anvil_reforge_equal_probability",
                AnvilReforgeUtilityProjection.Format(
                    utility.EqualProbability)),
            Parameter(
                "anvil_reforge_degradation_probability",
                AnvilReforgeUtilityProjection.Format(
                    utility.DegradationProbability)),
            Parameter(
                "anvil_reforge_decision_class",
                utility.DecisionClass),
            Parameter(
                "anvil_reforge_loadout_status",
                loadout.Status),
            Parameter(
                "anvil_reforge_capability_class",
                loadout.CapabilityClass),
            Parameter(
                "anvil_reforge_kill_credit_policy",
                loadout.KillCreditPolicy),
            Parameter(
                "anvil_reforge_loot_policy",
                loadout.LootPolicy),
            Parameter(
                "anvil_reforge_unlocked_slot_count",
                loadout.UnlockedSlotCount.ToString()),
            Parameter(
                "anvil_reforge_occupied_slot_count",
                loadout.OccupiedSlotCount.ToString()),
            Parameter(
                "anvil_reforge_empty_unlocked_slot_count",
                loadout.EmptyUnlockedSlotCount.ToString()),
            Parameter(
                "anvil_reforge_same_type_equipped_count",
                loadout.SameTypeEquippedCount.ToString()),
            Parameter(
                "anvil_reforge_other_type_equipped_count",
                loadout.OtherTypeEquippedCount.ToString()),
            Parameter(
                "anvil_reforge_loadout_relation",
                loadout.Relation)
        };
        return request;
    }
}

internal static class AnvilDistributionTestFixture
{
    internal const string StateJson =
        """
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":63,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":15,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "trinket_loadout": {"value":{"schema_version":"trinket_loadout_context.v1","status":"available_exact_live_slots","unlocked_slot_count":1,"occupied_slot_count":0,"empty_unlocked_slot_count":1,"slots":[{"slot_index":0,"unlocked":true,"occupied":false,"item_id":"","qualified_item_id":"","runtime_type":"","special_state":null}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":2,"empty_slots":10,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"IridiumSpur","qualified_item_id":"(TR)IridiumSpur","stack":1,"quality":0,"sale_price":0,"maximum_stack_size":1,"is_empty":false},{"slot_index":1,"item_id":"337","qualified_item_id":"(O)337","stack":3,"quality":0,"sale_price":1000,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{
              "location_id":"Farm",
              "tile_x":64,
              "tile_y":15,
              "qualified_item_id":"(BC)Anvil",
              "display_name":"Anvil",
              "ready_for_harvest":false,
              "minutes_until_ready":-1,
              "machine_execution_semantics":{"status":"available","execution_status":"available_data_driven","input_dispatch_kind":"base_object_data_driven","prediction_training_status":"distribution_complete_shared_rng_realized_stats_blocked"},
              "machine_data":{"status":"blocked","reason":"machine_profile_minimal_skips_machine_data","has_output":true,"output_rule_count":1},
              "held_item":null,
              "loadable_inputs":[{
                "slot_index":0,
                "item_id":"IridiumSpur",
                "qualified_item_id":"(TR)IridiumSpur",
                "stack":1,
                "quality":0,
                "sale_price":0,
                "probe_source":"Object.performObjectDropInAction(probe:true)",
                "load_executor_status":"covered_for_runtime_load",
                "predicted_output":{
                  "status":"available",
                  "training_eligibility_status":"distribution_complete_shared_rng_realized_stats_blocked",
                  "source":"decompiled_Object.OutputAnvil_and_vanilla_TrinketEffect_GenerateRandomStats",
                  "special_prediction_model_id":"anvil_trinket_reforge_distribution.v1",
                  "matched_rule_id":"Default",
                  "input":{"current_outcome":{"stat":"GeneralStat","value":5}},
                  "output_identity":{"qualified_item_id":"(TR)IridiumSpur","same_trinket_identity":true,"stack":1,"quality":0,"sale_price":0},
                  "consumed_additional_items":[{"qualified_item_id":"(O)337","required_count":3,"available_count":3,"unit_sale_price":1000,"total_sale_value":3000}],
                  "effective_minutes_until_ready":10,
                  "outcome_kind":"iridium_spur",
                  "outcome_rules":{"stat":"GeneralStat","distribution":"discrete_uniform","minimum_inclusive":5,"maximum_inclusive":10},
                  "distribution_status":"complete_vanilla_generative_rules",
                  "realized_generation_seed_status":"blocked_shared_Game1_random_Next_9999999",
                  "realized_output_stats_status":"blocked_until_native_load_records_held_trinket",
                  "rng_safety_status":"no_callback_no_RerollStats_no_Game1_random_read"
                }
              }]
            }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;

    internal static MachinePredictionTrainingContract
        ReadContract(
            StardewAI.Contracts.State.SnapshotEnvelope
                snapshot)
    {
        var prediction = ReadPrediction(snapshot);
        return MachinePredictionTrainingPolicy
            .ReadContract(
                prediction,
                "(TR)IridiumSpur");
    }

    internal static JsonElement ReadPrediction(
        StardewAI.Contracts.State.SnapshotEnvelope
            snapshot)
    {
        return snapshot.State["farm"]
            .GetProperty("machines")
            .GetProperty("value")[0]
            .GetProperty("loadable_inputs")[0]
            .GetProperty("predicted_output");
    }
}

public sealed class AnvilReforgeStrategicDemandProjectionTests
{
    public static IEnumerable<object[]> CapabilityCases =>
        new[]
        {
            new object[] { "iridium_spur", "critical_hit_mobility" },
            new object[] { "parrot_egg", "kill_triggered_currency" },
            new object[] { "frog_egg", "enemy_removal_no_kill_or_loot_credit" },
            new object[] { "fairy_box", "reactive_healing" },
            new object[] { "ice_rod", "ranged_freeze_control" },
            new object[] { "magic_quiver", "ranged_direct_damage" }
        };

    [Theory]
    [MemberData(nameof(CapabilityCases))]
    public void AllVettedOutcomesExposeExactCapability(
        string outcomeKind,
        string capability)
    {
        Assert.Equal(
            capability,
            AnvilReforgeLoadoutProjection.Capability(
                outcomeKind));
    }

    [Fact]
    public void BroadGrandpaGoalDoesNotInventTrinketPreference()
    {
        var ranked = Rank(
            "goal.grandpa_max_score_year3",
            Candidate(
                "spur",
                "critical_hit_mobility"));

        var candidate = Assert.Single(ranked);
        Assert.Equal(0.035, candidate.Score);
        Assert.Contains(
            "anvil_reforge_goal_demand_status=broad_goal_requires_downstream_subgoal",
            candidate.ExpectedEffect);
        Assert.Contains(
            "anvil_reforge_effective_demand_score=0",
            candidate.ExpectedEffect);
    }

    [Fact]
    public void LootGoalPrefersDamageAndRejectsFrogRemovalSemantics()
    {
        var ranked = Rank(
            "goal.quest_kill_and_preserve_drops",
            Candidate(
                "frog",
                "enemy_removal_no_kill_or_loot_credit"),
            Candidate(
                "quiver",
                "ranged_direct_damage"));

        Assert.Equal("quiver", ranked[0].CandidateId);
        Assert.Equal("frog", ranked[1].CandidateId);
        Assert.Contains(
            "anvil_reforge_effective_demand_score=-0.95",
            ranked[1].ExpectedEffect);
        Assert.Contains(
            "frog_removal_conflicts_with_loot_kill_credit_and_infestation_completion",
            ranked[1].ExpectedEffect);
    }

    [Fact]
    public void EconomyGoalPrefersParrotCurrencyCapability()
    {
        var ranked = Rank(
            "goal.economy.gold",
            Candidate(
                "quiver",
                "ranged_direct_damage"),
            Candidate(
                "parrot",
                "kill_triggered_currency"));

        Assert.Equal("parrot", ranked[0].CandidateId);
        Assert.Contains(
            "anvil_reforge_goal_family=economy",
            ranked[0].ExpectedEffect);
    }

    private static PolicyEventCandidatePrediction[] Rank(
        string goalId,
        params EventCandidate[] candidates)
    {
        return new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new OptionAvailabilityEnvelope
            {
                CurrentTime = 600,
                Options = new[]
                {
                    new OptionAvailability
                    {
                        OptionId = "farm.process_machines",
                        Available = true,
                        EventCandidates = candidates
                    }
                }
            },
            goalId);
    }

    private static EventCandidate Candidate(
        string id,
        string capability)
    {
        return new EventCandidate
        {
            CandidateId = id,
            Kind = "load_machine_input_tile",
            Available = true,
            ExpectedEffect =
                "anvil_reforge_capability_class=" +
                capability +
                ";anvil_reforge_unlocked_slot_count=1" +
                ";anvil_reforge_empty_unlocked_slot_count=1" +
                ";anvil_reforge_same_type_equipped_count=0"
        };
    }
}

public sealed class AnvilReforgeUtilityProjectionTests
{
    public static IEnumerable<object[]> SixOutcomeKinds =>
        new[]
        {
            Case(
                "iridium_spur",
                """{"value":5}""",
                """{"minimum_inclusive":5,"maximum_inclusive":10}""",
                "critical_hit_speed_buff_duration"),
            Case(
                "parrot_egg",
                """{"value":1}""",
                """{"minimum_inclusive":0,"maximum_inclusive":3}""",
                "kill_gold_coin_probability_level"),
            Case(
                "frog_egg",
                """{"value":7}""",
                """{"probabilities":[{"value":7,"probability":1}]}""",
                "frog_variant_mechanically_equivalent"),
            Case(
                "fairy_box",
                """{"value":3,"heal_delay_milliseconds":4100,"power":1}""",
                """{"probabilities":[{"value":1,"probability":0.33657421875},{"value":2,"probability":0.45},{"value":3,"probability":0.1375},{"value":4,"probability":0.0515625},{"value":5,"probability":0.02436328125}]}""",
                "healing_power_and_interval_tier"),
            Case(
                "ice_rod",
                """{"projectile_delay_milliseconds":4000,"freeze_time_milliseconds":3000}""",
                """{"perfect_override":{"probability":0.05,"projectile_delay_milliseconds":3000,"freeze_time_milliseconds":4000}}""",
                "freeze_duration_per_projectile_interval"),
            Case(
                "magic_quiver",
                """{"branch":"ordinary","min_damage":20,"max_damage":25,"projectile_delay_milliseconds":1500}""",
                """{"branches":[{"branch":"perfect","probability":0.04,"min_damage":30,"max_damage":35,"projectile_delay_milliseconds":900},{"branch":"rapid","probability":0.048,"min_damage_minimum_inclusive":8,"min_damage_maximum_inclusive":12,"max_damage_offset":5,"projectile_delay_minimum_inclusive":600,"projectile_delay_maximum_inclusive":700,"projectile_delay_step":10},{"branch":"heavy","probability":0.048,"min_damage_minimum_inclusive":23,"min_damage_maximum_inclusive":38,"max_damage_offset":5,"projectile_delay_minimum_inclusive":1500,"projectile_delay_maximum_inclusive":2000,"projectile_delay_step":100},{"branch":"ordinary","probability":0.864,"min_damage_minimum_inclusive":13,"min_damage_maximum_inclusive":28,"max_damage_offset":5,"projectile_delay_minimum_inclusive":1100,"projectile_delay_maximum_inclusive":2100,"projectile_delay_step":100}]}""",
                "expected_projectile_damage_per_second")
        };

    [Theory]
    [MemberData(nameof(SixOutcomeKinds))]
    public void AllVettedOutcomesProduceCompleteUtility(
        string json,
        string metric)
    {
        using var document = JsonDocument.Parse(json);

        var projection =
            AnvilReforgeUtilityProjection.Read(
                document.RootElement);

        Assert.True(projection.Supported);
        Assert.Equal(
            AnvilReforgeUtilityProjection.Status,
            projection.Status);
        Assert.Equal(metric, projection.MetricId);
        Assert.InRange(
            projection.CurrentUtility,
            0,
            1);
        Assert.InRange(
            projection.ExpectedUtility,
            0,
            1);
        Assert.Equal(
            1,
            projection.ImprovementProbability +
            projection.EqualProbability +
            projection.DegradationProbability,
            6);
    }

    [Fact]
    public void NativeResultFlowsToStrategyTrainingWithoutHidingExecutorFailures()
    {
        var runtime = File.ReadAllText(
            FindRepositoryFile(
                "tools",
                "StardewAI.RuntimeTestHarness",
                "ModEntry.MachinesAndPickup.cs"));
        var feedback = File.ReadAllText(
            FindRepositoryFile(
                "tools",
                "StardewAI.RuntimeTestHarness",
                "ModEntry.AnvilFeedback.cs"));
        var dataset = File.ReadAllText(
            FindRepositoryFile(
                "tools",
                "StardewAI.LiveTrainingLoop",
                "Program.QueueInspection.cs"));
        var bridge = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "StardewAI.TransparentBridge",
                "Adapters",
                "PlayerReadAdapter.Trinkets.cs"));
        var liveLoop = File.ReadAllText(
            FindRepositoryFile(
                "tools",
                "StardewAI.LiveTrainingLoop",
                "Program.QueueBuilding.cs"));
        var backend = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "StardewAI.Backend",
                "Program.cs"));

        Assert.Contains(
            "AnvilReforgeRealizedUtilityDelta",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "machine.anvil.reforge.utility",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "expected_projectile_damage_per_second",
            feedback,
            StringComparison.Ordinal);
        Assert.Contains(
            "(double)ice.FreezeTime",
            feedback,
            StringComparison.Ordinal);
        Assert.Contains(
            "trinket.ItemId != \"IridiumSpur\"",
            feedback,
            StringComparison.Ordinal);
        Assert.Contains(
            "AnvilDistributionRequestIsValid",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "recordedAnvilFeedback",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "TrainingRoles.StrategyValue",
            dataset,
            StringComparison.Ordinal);
        Assert.Contains(
            "excludeFromPolicyTraining",
            dataset,
            StringComparison.Ordinal);
        Assert.Contains(
            "anvil_reforge_realized_utility_delta",
            dataset,
            StringComparison.Ordinal);
        Assert.Contains(
            "trinket_loadout_context.v1",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "Farmer.MaximumTrinkets",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "unlockStatValue != 0",
            bridge,
            StringComparison.Ordinal);
        Assert.Contains(
            "goal_id = options.Goal",
            liveLoop,
            StringComparison.Ordinal);
        Assert.Contains(
            "request.GoalId",
            backend,
            StringComparison.Ordinal);
        Assert.Contains(
            "machine.anvil.reforge.effective_demand_score",
            dataset,
            StringComparison.Ordinal);
    }

    private static object[] Case(
        string kind,
        string current,
        string rules,
        string metric)
    {
        return new object[]
        {
            $$"""
            {
              "outcome_kind":"{{kind}}",
              "input":{"current_outcome":{{current}}},
              "outcome_rules":{{rules}}
            }
            """,
            metric
        };
    }

    private static string FindRepositoryFile(
        params string[] parts)
    {
        var directory = new DirectoryInfo(
            AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
               !File.Exists(
                   Path.Combine(
                       directory.FullName,
                       "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ??
            throw new InvalidOperationException(
                "Cannot find repository root."),
            Path.Combine(parts));
    }
}
