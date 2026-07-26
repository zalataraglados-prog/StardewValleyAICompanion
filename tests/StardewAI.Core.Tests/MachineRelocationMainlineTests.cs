using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MachineRelocationMainlineTests
{
    [Fact]
    public void PositiveLayoutBenefitFlowsThroughNativeRemovalHead()
    {
        var snapshot = Snapshot(
            relocationRangeStartX: 7,
            relocationRangeEndX: 8,
            sourceRemovalSafe: true);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true);
        var relocationCandidates = availability.Options[0]
            .EventCandidates.Where(row =>
                row.Kind == "relocate_machine_item")
            .ToArray();
        Assert.True(
            relocationCandidates.Length == 1,
            "option_reasons=" + string.Join(
                ";",
                availability.Options[0].HardBlockReasons) +
            ";candidate_kinds=" + string.Join(
                ";",
                availability.Options[0].EventCandidates.Select(row =>
                    row.Kind + ":" +
                    string.Join(",", row.BlockReasons))));
        var candidate = relocationCandidates[0];

        Assert.True(
            candidate.Available,
            string.Join(";", candidate.BlockReasons));
        Assert.Equal(15, candidate.TileX);
        Assert.Equal(5, candidate.TileY);
        Assert.Equal(
            "7",
            Parameter(
                candidate.Parameters,
                "relocation_target_tile_x"));
        Assert.Equal(
            "10",
            Parameter(
                candidate.Parameters,
                "layout_current_cluster_distance"));
        Assert.True(
            int.Parse(Parameter(
                candidate.Parameters,
                "layout_net_benefit_ticks")) > 0);

        var ranked = new EventCandidateRanker()
            .Rank(new(), availability)
            .Where(row => row.CandidateId == candidate.CandidateId)
            .ToArray();
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            snapshot.StateHash);

        Assert.Equal(
            new[] { "move_to_tile", "remove_machine_item" },
            plan.Steps.Select(row => row.Kind).ToArray());
        Assert.Equal(
            "7",
            Parameter(
                plan.Steps[1].Parameters,
                "relocation_target_tile_x"));

        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot,
            new StrategyCommitmentLedger
            {
                LedgerId = "strategy-ledger:test",
                MachineRelocationIntents = new[]
                {
                    new MachineRelocationIntent
                    {
                        IntentId = Parameter(
                            candidate.Parameters,
                            "relocation_intent_id"),
                        Status =
                            StrategyCommitmentStatuses.Active,
                        QualifiedItemId = "(BC)13",
                        SourceLocationId = "Farm",
                        SourceTileX = 15,
                        SourceTileY = 5,
                        TargetLocationId = "Farm",
                        TargetTileX = 7,
                        TargetTileY = 5
                    }
                }
            });
        var removal = Assert.Single(queue.Items.Where(row =>
            row.OptionId == "executor.remove_machine"));

        Assert.Equal("pending", removal.Status);
        Assert.Empty(removal.BlockingReasons);
        Assert.Equal(
            "remove_machine",
            Assert.Single(removal.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void LayoutWithoutRouteImprovementIsExcludedUpstream()
    {
        var snapshot = Snapshot(
            relocationRangeStartX: 18,
            relocationRangeEndX: 19,
            sourceRemovalSafe: true);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true);

        Assert.DoesNotContain(
            availability.Options[0].EventCandidates,
            row => row.Kind == "relocate_machine_item");
    }

    [Fact]
    public void BusyOutlierIsExcludedUpstream()
    {
        var snapshot = Snapshot(
            relocationRangeStartX: 7,
            relocationRangeEndX: 8,
            sourceRemovalSafe: false);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true);

        Assert.DoesNotContain(
            availability.Options[0].EventCandidates,
            row => row.Kind == "relocate_machine_item");
    }

    private static SnapshotEnvelope Snapshot(
        int relocationRangeStartX,
        int relocationRangeEndX,
        bool sourceRemovalSafe)
    {
        var sourceMinutes = sourceRemovalSafe ? -1 : 30;
        var sourceRemovalStatus = sourceRemovalSafe
            ? "safe_idle_native_pickaxe"
            : "blocked";
        var sourceRemovalReasons = sourceRemovalSafe
            ? "[]"
            : """["machine_removal_processing"]""";
        var stateJson = """
        {
          "player": {
            "location_id":{"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":14,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":5,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy":{"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[{"slot_index":1,"item_id":"Pickaxe","qualified_item_id":"(T)Pickaxe","stack":1,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity":{"value":{"occupied_stacks":1,"empty_slots":11,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "machine_placement":{"value":{
              "projection_status":"complete_inventory_and_relocation_machine_types_across_loaded_persistent_locations",
              "static_projection_fingerprint":"machine-layout:test",
              "rows":[],
              "relocation_rows":[{
                "projection_role":"placed_machine_relocation_probe",
                "inventory_slot_index":-1,
                "item_id":"13",
                "qualified_item_id":"(BC)13",
                "display_name":"Furnace",
                "stack":1,
                "runtime_type":"StardewValley.Object",
                "is_cask":false,
                "locations":[{
                  "location_id":"Farm",
                  "location_is_current":true,
                  "machine_operational_context_valid":true,
                  "placement_probe_status":"native_legal_tiles_available",
                  "static_legal_tile_count":LEGAL_COUNT,
                  "static_legal_tile_ranges":[{"y":5,"start_x":START_X,"end_x":END_X}]
                }]
              }]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines":{"value":[
              {
                "location_id":"Farm",
                "location_is_current":true,
                "tile_x":15,
                "tile_y":5,
                "qualified_item_id":"(BC)13",
                "machine_has_input":true,
                "machine_has_output":true,
                "runtime_type":"StardewValley.Object",
                "object_type":"Crafting",
                "fragility":0,
                "ready_for_harvest":false,
                "minutes_until_ready":SOURCE_MINUTES,
                "held_item":null,
                "removal_status":"SOURCE_REMOVAL_STATUS",
                "removal_safe_now":SOURCE_REMOVAL_SAFE,
                "removal_block_reasons":SOURCE_REMOVAL_REASONS,
                "removal_tool_slot_index":1,
                "removal_tool_qualified_item_id":"(T)Pickaxe",
                "removal_native_contract":"NATIVE_CONTRACT",
                "removal_projection_fingerprint":"source-fingerprint"
              },
              {
                "location_id":"Farm",
                "location_is_current":true,
                "tile_x":5,
                "tile_y":5,
                "qualified_item_id":"(BC)13",
                "machine_has_input":true,
                "machine_has_output":true,
                "runtime_type":"StardewValley.Object",
                "object_type":"Crafting",
                "fragility":0,
                "ready_for_harvest":false,
                "minutes_until_ready":30,
                "held_item":null,
                "removal_status":"blocked",
                "removal_safe_now":false,
                "removal_block_reasons":["machine_removal_processing"],
                "removal_tool_slot_index":1,
                "removal_tool_qualified_item_id":"(T)Pickaxe",
                "removal_native_contract":"NATIVE_CONTRACT",
                "removal_projection_fingerprint":"peer-fingerprint"
              }
            ],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "map":{"value":{"width":25,"height":20},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"Farm","width":25,"height":20,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "time":{"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("START_X", relocationRangeStartX.ToString())
        .Replace("END_X", relocationRangeEndX.ToString())
        .Replace(
            "LEGAL_COUNT",
            (relocationRangeEndX - relocationRangeStartX + 1)
                .ToString())
        .Replace("SOURCE_MINUTES", sourceMinutes.ToString())
        .Replace(
            "SOURCE_REMOVAL_STATUS",
            sourceRemovalStatus)
        .Replace(
            "SOURCE_REMOVAL_SAFE",
            sourceRemovalSafe.ToString().ToLowerInvariant())
        .Replace(
            "SOURCE_REMOVAL_REASONS",
            sourceRemovalReasons)
        .Replace(
            "NATIVE_CONTRACT",
            "Pickaxe.DoFunction_to_Object.performToolAction_then_performRemoveAction_and_exact_machine_debris");
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
                stateJson,
                JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-26T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string Parameter(
        IEnumerable<StardewAI.Contracts.Execution.SmallModelActionParameter>
            parameters,
        string name) =>
        parameters.Single(row => row.Name == name).Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
