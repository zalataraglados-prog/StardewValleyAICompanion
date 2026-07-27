using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;

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

    private static SmallModelActionEnvelope AnvilRequest(
        StardewAI.Contracts.State.SnapshotEnvelope snapshot,
        string fingerprint)
    {
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
                fingerprint)
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
        var prediction = snapshot.State["farm"]
            .GetProperty("machines")
            .GetProperty("value")[0]
            .GetProperty("loadable_inputs")[0]
            .GetProperty("predicted_output");
        return MachinePredictionTrainingPolicy
            .ReadContract(
                prediction,
                "(TR)IridiumSpur");
    }
}
