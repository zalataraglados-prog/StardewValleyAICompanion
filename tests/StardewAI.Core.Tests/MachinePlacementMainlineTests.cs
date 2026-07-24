using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MachinePlacementMainlineTests
{
    [Fact]
    public void InventoryMachineFlowsThroughCandidatePlanAndNativeQueue()
    {
        var snapshot = Snapshot();
        var ledger = Ledger(revision: 3);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true,
                ledger);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(
                row => row.Kind == "place_machine_item"));

        Assert.True(
            candidate.Available,
            string.Join(";", candidate.BlockReasons));
        Assert.Equal("(BC)12", candidate.QualifiedItemId);
        Assert.Equal(4, candidate.SlotIndex);
        Assert.Equal(7, candidate.TileX);
        Assert.Equal(5, candidate.TileY);
        Assert.Equal(
            "6",
            Parameter(candidate.Parameters, "stand_tile_x"));
        Assert.Equal(
            "ready_no_active_material_reservations",
            Parameter(
                candidate.Parameters,
                "material_reservation_guard_status"));

        var ranked = new EventCandidateRanker()
            .Rank(new BaselineTrainingReport(), availability)
            .Where(row => row.CandidateId == candidate.CandidateId)
            .ToArray();
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            snapshot.StateHash);

        Assert.Equal(
            new[] { "move_to_tile", "place_machine_item" },
            plan.Steps.Select(row => row.Kind).ToArray());
        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot,
            ledger);
        var item = Assert.Single(
            queue.Items.Where(
                row => row.OptionId == "executor.place_machine"));

        Assert.Equal("pending", item.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal(
            "place_machine",
            Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void RemotePlacementEmitsOneConnectorWithPlacementContinuation()
    {
        var snapshot = Snapshot(includeRemoteLocation: true);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true,
                Ledger(revision: 3));
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(
                row => row.CandidateId.StartsWith(
                    "machine-place-route:Cellar:",
                    StringComparison.Ordinal)));

        Assert.True(
            candidate.Available,
            string.Join(";", candidate.BlockReasons));
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.Equal(
            "executor.place_machine",
            Parameter(
                candidate.Parameters,
                "continuation.option_id"));
        Assert.Equal(
            "Cellar",
            Parameter(
                candidate.Parameters,
                "continuation.machine_location_id"));
        Assert.Equal(
            "4",
            Parameter(
                candidate.Parameters,
                "continuation.machine_inventory_slot_index"));
        Assert.Contains(
            "exact_tile_selected_after_target_map_load=true",
            candidate.ExpectedEffect,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CompilerRejectsExactTileProjectionDrift()
    {
        var initial = Snapshot();
        var ledger = Ledger(revision: 3);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                initial,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true,
                ledger);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(
                row => row.Kind == "place_machine_item"));
        var ranked = new EventCandidateRanker()
            .Rank(new BaselineTrainingReport(), availability)
            .Where(row => row.CandidateId == candidate.CandidateId)
            .ToArray();
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            initial.StateHash);
        var drifted = Snapshot(rangeStartX: 9, rangeEndX: 9);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(
            plan,
            drifted,
            ledger);
        var item = Assert.Single(
            queue.Items.Where(
                row => row.OptionId == "executor.place_machine"));

        Assert.Equal("blocked", item.Status);
        Assert.Contains(
            "place_machine_projection_fingerprint_drifted",
            item.BlockingReasons);
        Assert.Contains(
            "place_machine_exact_tile_not_native_legal",
            item.BlockingReasons);
    }

    [Fact]
    public void ReservedInventoryMachineIsExcludedUpstream()
    {
        var snapshot = Snapshot();
        var ledger = Ledger(
            revision: 4,
            reservations: new[]
            {
                new MaterialReservation
                {
                    ReservationId = "reserve-keg",
                    Revision = 4,
                    Status = StrategyCommitmentStatuses.Active,
                    NodeId = "player:123",
                    SlotIndex = 4,
                    QualifiedItemId = "(BC)12",
                    Quantity = 1
                }
            });

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true,
                ledger);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(
                row => row.Kind == "place_machine_item"));

        Assert.False(candidate.Available);
        Assert.Contains(
            "machine_placement_inventory_item_reserved",
            candidate.BlockReasons);
    }

    [Fact]
    public void SameSlotReservationInChestDoesNotBlockPlayerMachine()
    {
        var snapshot = Snapshot();
        var ledger = Ledger(
            revision: 4,
            reservations: new[]
            {
                new MaterialReservation
                {
                    ReservationId = "reserve-chest-keg",
                    Revision = 4,
                    Status = StrategyCommitmentStatuses.Active,
                    NodeId = "chest:FarmHouse:3,3",
                    SlotIndex = 4,
                    QualifiedItemId = "(BC)12",
                    Quantity = 1
                }
            });

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true,
                ledger);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(
                row => row.Kind == "place_machine_item"));

        Assert.True(
            candidate.Available,
            string.Join(";", candidate.BlockReasons));
    }

    [Fact]
    public void DispatchReadinessRejectsLedgerRevisionDrift()
    {
        var snapshot = Snapshot();
        var ledger = Ledger(revision: 3);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true,
                ledger);
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(
                row => row.Kind == "place_machine_item"));
        var ranked = new EventCandidateRanker()
            .Rank(new BaselineTrainingReport(), availability)
            .Where(row => row.CandidateId == candidate.CandidateId)
            .ToArray();
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot,
            ledger);
        var item = Assert.Single(
            queue.Items.Where(
                row => row.OptionId == "executor.place_machine"));
        var service = new ActionQueueDispatchReadinessService();

        var ready = service.Evaluate(
            queue,
            item,
            ledger,
            snapshot.StateHash);
        var drifted = service.Evaluate(
            queue,
            item,
            Ledger(revision: 4),
            snapshot.StateHash);

        Assert.True(ready.Ready);
        Assert.False(drifted.Ready);
        Assert.Contains(
            "dispatch_strategy_ledger_revision_drifted",
            drifted.BlockingReasons);
    }

    [Fact]
    public void RuntimeUsesDecompileConfirmedNativePlacementChain()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains(
            "Utility.playerCanPlaceItemHere(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Utility.tryToPlaceItem(",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "location.objects.Add(targetVector",
            source,
            StringComparison.Ordinal);
    }

    private static StrategyCommitmentLedger Ledger(
        int revision,
        MaterialReservation[]? reservations = null) => new()
        {
            LedgerId = "strategy-ledger:test",
            Revision = revision,
            MaterialReservations =
                reservations ?? Array.Empty<MaterialReservation>()
        };

    private static SnapshotEnvelope Snapshot(
        int rangeStartX = 7,
        int rangeEndX = 8,
        bool includeRemoteLocation = false)
    {
        var remotePlacementLocation = includeRemoteLocation
            ? """
                ,{
                  "location_id":"Cellar",
                  "location_is_current":false,
                  "machine_operational_context_valid":true,
                  "placement_probe_status":"native_legal_tiles_available",
                  "static_legal_tile_count":2,
                  "static_legal_tile_ranges":[{"y":6,"start_x":5,"end_x":6}]
                }
              """
            : string.Empty;
        var remoteRouting = includeRemoteLocation
            ? """
            ,
            "route_graph":{"value":{"edges":[{"kind":"warp","from_location":"FarmHouse","from_x":2,"from_y":3,"target_location":"Cellar","target_x":5,"target_y":5,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors":{"value":{"location_id":"FarmHouse","connector_count":1,"connectors":[{"kind":"warp","tile_x":2,"tile_y":3,"target_location":"Cellar","target_x":5,"target_y":5,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            """
            : string.Empty;
        var stateJson = """
        {
          "player": {
            "location_id":{"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":6,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":5,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy":{"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[{"slot_index":4,"item_id":"12","qualified_item_id":"(BC)12","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity":{"value":{"occupied_stacks":1,"empty_slots":11,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "machine_placement":{"value":{
              "projection_status":"complete_all_inventory_machines_across_loaded_persistent_locations",
              "static_projection_fingerprint":"FINGERPRINT",
              "rows":[{
                "inventory_slot_index":4,
                "item_id":"12",
                "qualified_item_id":"(BC)12",
                "display_name":"Keg",
                "stack":1,
                "runtime_type":"StardewValley.Object",
                "is_cask":false,
                "locations":[{
                  "location_id":"FarmHouse",
                  "location_is_current":true,
                  "machine_operational_context_valid":true,
                  "placement_probe_status":"native_legal_tiles_available",
                  "static_legal_tile_count":LEGAL_COUNT,
                  "static_legal_tile_ranges":[{"y":5,"start_x":START_X,"end_x":END_X}]
                }REMOTE_PLACEMENT_LOCATION]
              }]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "map":{"value":{"width":20,"height":20},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "warps":{"value":[{"x":2,"y":3,"target_location":"Cellar","target_x":5,"target_y":5}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"FarmHouse","width":20,"height":20,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}REMOTE_ROUTING
          },
          "menus": {
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "time":{"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("START_X", rangeStartX.ToString())
        .Replace("END_X", rangeEndX.ToString())
        .Replace(
            "LEGAL_COUNT",
            Math.Max(0, rangeEndX - rangeStartX + 1).ToString())
        .Replace(
            "FINGERPRINT",
            "machine-placement:" + rangeStartX + "-" + rangeEndX)
        .Replace(
            "REMOTE_PLACEMENT_LOCATION",
            remotePlacementLocation)
        .Replace("REMOTE_ROUTING", remoteRouting);
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
                stateJson,
                JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-24T00:00:00Z",
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
