using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MachineHarvestExperienceMainlineTests
{
    [Fact]
    public void MultiSkillMachineHarvestExperienceFlowsToCollectExecutor()
    {
        var snapshot = Snapshot(StateJson("Farming 5 Mining 3 Luck 20", 3));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "farm.process_machines" },
            true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "expected_skill_experience_deltas_json" && parameter.Value.Contains("\"SkillId\":\"mining\"", StringComparison.Ordinal));
        Assert.DoesNotContain(candidate.Parameters, parameter => parameter.Name == "skill_experience_skill_id");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var collect = Assert.Single(queue.Items.Where(item => item.OptionId == "executor.collect_machine_output"));
        Assert.Empty(collect.BlockingReasons);
        Assert.Contains(collect.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "expected_mastery_experience_delta" && parameter.Value == "3");
    }

    [Fact]
    public void CompilerRejectsMachineExperienceProjectionDrift()
    {
        var initial = Snapshot(StateJson("Farming 5 Mining 3 Luck 20", 3));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            initial,
            new[] { "farm.process_machines" },
            true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            initial.StateHash);
        var drifted = Snapshot(StateJson("Farming 5 Mining 4 Luck 20", 4));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);
        var collect = Assert.Single(queue.Items.Where(item => item.OptionId == "executor.collect_machine_output"));
        Assert.Contains("collect_machine_output_experience_projection_drifted", collect.BlockingReasons);
    }

    private static string StateJson(string raw, int miningDelta)
    {
        var deltas = JsonSerializer.Serialize(new[]
        {
            new { SkillId = "farming", SkillIndex = 0, Delta = 5 },
            new { SkillId = "mining", SkillIndex = 3, Delta = miningDelta },
            new { SkillId = "luck", SkillIndex = 5, Delta = 0 }
        });
        var escapedDeltas = JsonSerializer.Serialize(deltas);
        return $$"""
        {
          "player": {
            "location_id":{"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":63,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":15,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity":{"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[{"slot_index":0,"item_id":"390","qualified_item_id":"(O)390","stack":10,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"is_empty":true}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm":{"machines":{"value":[{
            "tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":true,"minutes_until_ready":0,
            "harvest_experience_raw":"{{raw}}","harvest_experience_entries":[],
            "harvest_experience_deltas":{{deltas}},"harvest_experience_deltas_json":{{escapedDeltas}},
            "harvest_mastery_experience_delta":3,"harvest_experience_projection_status":"exact_native_pair_parser_and_gain_sink",
            "held_item":{"item_id":"388","qualified_item_id":"(O)388","stack":1,"quality":0,"sale_price":20,"maximum_stack_size":999}
          }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "locations":{"collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
        }
        """;
    }

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-18T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
