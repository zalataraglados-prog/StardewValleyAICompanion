using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MachineRelocationContinuationTests
{
    [Fact]
    public void FreshSnapshotBindsRecoveredMachineToExactIntentTarget()
    {
        var snapshot = Snapshot();
        var ledger = Ledger(
            new MachineRelocationIntent
            {
                IntentId = "layout:Farm:15,5->7,5:(BC)13",
                Revision = 1,
                Status = StrategyCommitmentStatuses.Active,
                SourceDecisionId = "machine-relocate:test",
                SourceStateHash = "source-state",
                QualifiedItemId = "(BC)13",
                ItemId = "13",
                SourceLocationId = "Farm",
                SourceTileX = 15,
                SourceTileY = 5,
                TargetLocationId = "Farm",
                TargetTileX = 7,
                TargetTileY = 5,
                MachinePlacementProjectionFingerprint =
                    "machine-layout:before",
                LayoutNetBenefitTicks = 7200
            });
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true,
                ledger);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(row =>
                row.Kind == "place_machine_item"));

        Assert.True(
            candidate.Available,
            string.Join(";", candidate.BlockReasons));
        Assert.Equal(7, candidate.TileX);
        Assert.Equal(5, candidate.TileY);
        Assert.Equal(
            "transparent_machine_relocation_exact_target_placement",
            candidate.AvailabilityClass);
        Assert.Equal(
            "layout:Farm:15,5->7,5:(BC)13",
            Parameter(
                candidate.Parameters,
                "relocation_intent_id"));

        var ranked = new EventCandidateRanker()
            .Rank(new(), availability)
            .Where(row => row.CandidateId == candidate.CandidateId)
            .ToArray();
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot,
            ledger);
        var placement = Assert.Single(queue.Items.Where(row =>
            row.OptionId == "executor.place_machine"));

        Assert.Equal("pending", placement.Status);
        Assert.Empty(placement.BlockingReasons);
    }

    [Fact]
    public void CompilerRejectsRelocationTargetThatDriftsFromLedger()
    {
        var snapshot = Snapshot();
        var ledger = Ledger(
            new MachineRelocationIntent
            {
                IntentId = "layout:test",
                Revision = 1,
                Status = StrategyCommitmentStatuses.Active,
                SourceStateHash = "source-state",
                QualifiedItemId = "(BC)13",
                SourceLocationId = "Farm",
                SourceTileX = 15,
                SourceTileY = 5,
                TargetLocationId = "Farm",
                TargetTileX = 7,
                TargetTileY = 5,
                MachinePlacementProjectionFingerprint =
                    "machine-layout:before"
            });
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                true,
                ledger);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(row =>
                row.Kind == "place_machine_item"));
        var ranked = new EventCandidateRanker()
            .Rank(new(), availability)
            .Where(row => row.CandidateId == candidate.CandidateId)
            .ToArray();
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            snapshot.StateHash);
        var placementStep = plan.Steps.Single(row =>
            row.Kind == "place_machine_item");
        placementStep.TargetTileX = 8;

        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot,
            ledger);
        var placement = Assert.Single(queue.Items.Where(row =>
            row.OptionId == "executor.place_machine"));

        Assert.Equal("blocked", placement.Status);
        Assert.Contains(
            "place_machine_relocation_intent_drifted",
            placement.BlockingReasons);
    }

    private static StrategyCommitmentLedger Ledger(
        params MachineRelocationIntent[] intents) => new()
        {
            LedgerId = "strategy-ledger:test-save:test-player",
            SaveId = "test-save",
            PlayerId = "test-player",
            Revision = 0,
            MachineRelocationIntents = intents
        };

    private static SnapshotEnvelope Snapshot()
    {
        var machines = new List<string>
        {
            """
            {
              "location_id":"Farm",
              "location_is_current":true,
              "tile_x":5,
              "tile_y":5,
              "qualified_item_id":"(BC)13",
              "machine_has_input":true,
              "machine_has_output":true,
              "removal_status":"blocked",
              "removal_safe_now":false,
              "removal_block_reasons":["machine_removal_processing"]
            }
            """
        };
        var inventory = """
              [
                {"slot_index":1,"item_id":"Pickaxe","qualified_item_id":"(T)Pickaxe","stack":1,"is_empty":false},
                {"slot_index":4,"item_id":"13","qualified_item_id":"(BC)13","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}
              ]
              """;
        var inventoryRows = """
              [{
                "projection_role":"inventory_machine",
                "inventory_slot_index":4,
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
                  "static_legal_tile_count":2,
                  "static_legal_tile_ranges":[
                    {"y":5,"start_x":7,"end_x":7},
                    {"y":5,"start_x":12,"end_x":12}
                  ]
                }]
              }]
              """;
        var stateJson = """
        {
          "identity": {
            "save_id":{"value":"test-save","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "player_id":{"value":"test-player","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id":{"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":11,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":5,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy":{"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":INVENTORY,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity":{"value":{"occupied_stacks":2,"empty_slots":10,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "machine_placement":{"value":{
              "projection_status":"complete_inventory_and_relocation_machine_types_across_loaded_persistent_locations",
              "static_projection_fingerprint":"CURRENT_FINGERPRINT",
              "rows":INVENTORY_ROWS,
              "relocation_rows":[{
                "projection_role":"placed_machine_relocation_probe",
                "inventory_slot_index":-1,
                "item_id":"13",
                "qualified_item_id":"(BC)13",
                "locations":[{
                  "location_id":"Farm",
                  "machine_operational_context_valid":true,
                  "placement_probe_status":"native_legal_tiles_available",
                  "static_legal_tile_ranges":[{"y":5,"start_x":7,"end_x":8}]
                }]
              }]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines":{"value":[MACHINES],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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
        .Replace("INVENTORY_ROWS", inventoryRows)
        .Replace("INVENTORY", inventory)
        .Replace("MACHINES", string.Join(",", machines))
        .Replace(
            "CURRENT_FINGERPRINT",
            "machine-layout:after");
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
                stateJson,
                JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-26T01:00:00Z",
            Completeness = "complete",
            SaveId = new FieldEnvelope<string?>
            {
                Value = "test-save",
                Status = FieldStatus.Available
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "test-player",
                Status = FieldStatus.Available
            },
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
