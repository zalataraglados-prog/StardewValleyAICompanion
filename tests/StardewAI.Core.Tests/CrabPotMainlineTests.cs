using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CrabPotMainlineTests
{
    private const string OutputHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void ReadyCrabPotFlowsFromTransparentCandidateThroughQueue()
    {
        var snapshot = Snapshot(StateJson(outputQuantity: 2));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "fishing.collect_crab_pots" }, includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("collect_crab_pot", candidate.Kind);
        Assert.Equal("(O)372", candidate.QualifiedItemId);
        Assert.Equal(2, candidate.Quantity);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_skill_id" && parameter.Value == "fishing");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_skill_experience_delta" && parameter.Value == "5");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_fish_caught_count_after" && parameter.Value == "4");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("collect_crab_pot", planStep.Kind);
        Assert.Contains(planStep.SafetyConstraints, value => value == "native_checkAction_only");

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.collect_crab_pot", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("collect_crab_pot", step.StepType);
        Assert.Contains("player.skills.fishing.experience_increases", step.ExpectedEffect);
    }

    [Fact]
    public void CompilerRejectsCrabPotOutputDriftAfterPlanning()
    {
        var initial = Snapshot(StateJson(outputQuantity: 2));
        var candidate = Assert.Single(new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(
                initial,
                new[] { "fishing.collect_crab_pots" },
                includeExecutorCalibrationOptions: true)));
        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, initial.StateHash);
        var drifted = Snapshot(StateJson(outputQuantity: 1));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(queue.Items.Single().BlockingReasons, reason => reason == "collect_crab_pot_output_projection_drifted");
    }

    private static string StateJson(int outputQuantity)
    {
        var outputItems = JsonSerializer.Serialize(new[]
        {
            new
            {
                RuntimeType = "StardewValley.Object",
                QualifiedItemId = "(O)372",
                Quality = 0,
                UnitStateSha256 = OutputHash,
                Quantity = outputQuantity
            }
        });
        var outputItemsLiteral = JsonSerializer.Serialize(outputItems, JsonOptions);
        return """
        {
          "player": {
            "location_id": {"value":"Beach","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":20,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[{
              "tile_x":22,"tile_y":10,"item_id":"710","qualified_item_id":"(O)710","type":"StardewValley.Objects.CrabPot",
              "crab_pot_collect_status":"ready","crab_pot_tile_index":714,"crab_pot_ready_for_harvest":true,
              "crab_pot_bait_qualified_item_id":"(O)685","crab_pot_output_runtime_type":"StardewValley.Object",
              "crab_pot_output_qualified_item_id":"(O)372","crab_pot_output_quality":0,
              "crab_pot_output_unit_state_sha256":"OUTPUT_HASH","crab_pot_expected_output_items_json":OUTPUT_ITEMS,
              "crab_pot_output_state_context":"post_inventory_receive",
              "crab_pot_output_stack_before":1,"crab_pot_output_stack_on_collect":OUTPUT_QUANTITY,
              "crab_pot_book_double_roll_succeeded":true,"crab_pot_book_crabbing_owned":true,"crab_pot_book_double_applied":true,
              "crab_pot_fishing_experience_on_success_min":5,"crab_pot_fishing_experience_on_success_max":5,
              "crab_pot_experience_projection_status":"exact","crab_pot_fish_collection_eligible":true,
              "crab_pot_fish_caught_count_before":2,"crab_pot_fish_caught_count_after":4,
              "crab_pot_fish_caught_max_size_before":9,"crab_pot_catch_size_min":1,"crab_pot_catch_size_max":10,
              "crab_pot_catch_size_projection_status":"runtime_rng_observed"
            }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"location_id":"Beach","width":100,"height":100},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Beach","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("OUTPUT_HASH", OutputHash)
        .Replace("OUTPUT_ITEMS", outputItemsLiteral)
        .Replace("OUTPUT_QUANTITY", outputQuantity.ToString());
    }

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-18T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
