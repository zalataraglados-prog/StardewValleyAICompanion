using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class StoragePlacementMainlineTests
{
    [Fact]
    public void InventoryChestFlowsThroughCandidatePlanAndNativeQueue()
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
                row => row.Kind == "place_storage_item"));

        Assert.True(
            candidate.Available,
            string.Join(";", candidate.BlockReasons));
        Assert.Equal("(BC)130", candidate.QualifiedItemId);
        Assert.Equal(4, candidate.SlotIndex);
        Assert.Equal(2, candidate.TileX);
        Assert.Equal(1, candidate.TileY);
        Assert.Equal(
            "ordinary_material",
            Parameter(candidate.Parameters, "storage_role"));
        Assert.Equal(
            "ready_no_active_material_reservations",
            Parameter(
                candidate.Parameters,
                "material_reservation_guard_status"));

        var ranked = new EventCandidateRanker()
            .Rank(new BaselineTrainingReport(), availability)
            .Where(row =>
                row.CandidateId == candidate.CandidateId)
            .ToArray();
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            snapshot.StateHash);

        Assert.Equal(
            new[] { "move_to_tile", "place_storage_item" },
            plan.Steps.Select(row => row.Kind).ToArray());
        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot,
            ledger);
        var item = Assert.Single(
            queue.Items.Where(
                row => row.OptionId ==
                    "executor.place_storage"));

        Assert.Equal("pending", item.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal(
            "place_storage",
            Assert.Single(
                item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerRejectsRouteSafeLayoutDrift()
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
                row => row.Kind == "place_storage_item"));
        var ranked = new EventCandidateRanker()
            .Rank(new BaselineTrainingReport(), availability)
            .Where(row =>
                row.CandidateId == candidate.CandidateId)
            .ToArray();
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            initial.StateHash);
        var drifted = Snapshot(
            rangeStartX: 3,
            rangeEndX: 3);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(
            plan,
            drifted,
            ledger);
        var item = Assert.Single(
            queue.Items.Where(
                row => row.OptionId ==
                    "executor.place_storage"));

        Assert.Equal("blocked", item.Status);
        Assert.Contains(
            "place_storage_projection_fingerprint_drifted",
            item.BlockingReasons);
        Assert.Contains(
            "place_storage_route_safe_layout_drifted",
            item.BlockingReasons);
    }

    [Fact]
    public void RemotePlacementEmitsOneConnectorWithStorageContinuation()
    {
        var snapshot = Snapshot(
            includeRemoteLocation: true);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true,
                Ledger(revision: 3));
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(
                row => row.CandidateId.StartsWith(
                    "storage-place-route:Cellar:",
                    StringComparison.Ordinal)));

        Assert.True(
            candidate.Available,
            string.Join(";", candidate.BlockReasons));
        Assert.Equal(
            "route_connector_tile",
            candidate.Kind);
        Assert.Equal(
            "executor.place_storage",
            Parameter(
                candidate.Parameters,
                "continuation.option_id"));
        Assert.Equal(
            "Cellar",
            Parameter(
                candidate.Parameters,
                "continuation.storage_location_id"));
        Assert.Equal(
            "4",
            Parameter(
                candidate.Parameters,
                "continuation.storage_inventory_slot_index"));
        Assert.Equal(
            "ordinary_material",
            Parameter(
                candidate.Parameters,
                "continuation.storage_role"));

        var ranked = new EventCandidateRanker()
            .Rank(new BaselineTrainingReport(), availability)
            .Where(row =>
                row.CandidateId == candidate.CandidateId)
            .ToArray();
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            snapshot.StateHash);
        var step = Assert.Single(plan.Steps);

        Assert.Equal("traverse_connector", step.Kind);
        Assert.Equal(
            "Cellar",
            Parameter(
                step.Parameters,
                "continuation.storage_location_id"));
        Assert.Equal(
            "fresh_snapshot_after_each_connector",
            Parameter(
                step.Parameters,
                "storage_route.snapshot_policy"));
        Assert.Contains(
            "exact_tile_selected_after_target_map_load=true",
            candidate.ExpectedEffect,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReservedInventoryChestIsExcludedUpstream()
    {
        var snapshot = Snapshot();
        var ledger = Ledger(
            revision: 4,
            reservations: new[]
            {
                new MaterialReservation
                {
                    ReservationId = "reserve-chest",
                    Revision = 4,
                    Status = StrategyCommitmentStatuses.Active,
                    NodeId = "player:123",
                    SlotIndex = 4,
                    QualifiedItemId = "(BC)130",
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
                row => row.Kind == "place_storage_item"));

        Assert.False(candidate.Available);
        Assert.Contains(
            "storage_placement_inventory_item_reserved",
            candidate.BlockReasons);
    }

    [Fact]
    public void RuntimeUsesNativeStoragePlacementChain()
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
        Assert.Contains(
            "placedChest.playerChest.Value",
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
        int rangeStartX = 2,
        int rangeEndX = 3,
        bool includeRemoteLocation = false)
    {
        var remoteStorageLocation = includeRemoteLocation
            ? """
                ,{
                  "location_id":"Cellar",
                  "location_is_current":false,
                  "placement_probe_status":"native_legal_tiles_available",
                  "static_legal_tile_count":2,
                  "static_legal_tile_ranges":[{"y":6,"start_x":5,"end_x":6}]
                }
              """
            : string.Empty;
        var stateJson = """
        {
          "player": {
            "location_id":{"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy":{"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[{"slot_index":4,"item_id":"130","qualified_item_id":"(BC)130","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity":{"value":{"occupied_stacks":1,"empty_slots":11,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "storage_placement":{"value":{
              "schema_version":"storage_placement.v1",
              "projection_status":"complete_inventory_player_chests_across_persistent_player_locations",
              "static_projection_fingerprint":"STORAGE_FINGERPRINT",
              "rows":[{
                "inventory_slot_index":4,
                "item_id":"130",
                "qualified_item_id":"(BC)130",
                "display_name":"Chest",
                "stack":1,
                "runtime_type":"StardewValley.Object",
                "native_storage_branch":"native_object_placement_normal_chest",
                "placed_runtime_type":"StardewValley.Objects.Chest",
                "special_chest_type":"None",
                "actual_capacity":36,
                "global_inventory_id":"",
                "ordinary_material_storage":true,
                "shared_global_storage":false,
                "shipping_storage":false,
                "fridge_storage":false,
                "locations":[{
                  "location_id":"FarmHouse",
                  "location_is_current":true,
                  "placement_probe_status":"native_legal_tiles_available",
                  "static_legal_tile_count":LEGAL_COUNT,
                  "static_legal_tile_ranges":[{"y":1,"start_x":START_X,"end_x":END_X}]
                }REMOTE_STORAGE_LOCATION]
              }]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "map":{"value":{"width":10,"height":10},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "warps":{"value":[{"x":2,"y":3,"target_location":"Cellar","target_x":5,"target_y":5}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "chests":{"value":{
              "schema_version":"storage_infrastructure.v1",
              "status":"available",
              "scope_location_id":"FarmHouse",
              "access_points":[]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{
              "location_id":"FarmHouse",
              "width":10,
              "height":10,
              "notable_tiles":[]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph":{"value":{"edges":[{"kind":"warp","from_location":"FarmHouse","from_x":2,"from_y":3,"target_location":"Cellar","target_x":5,"target_y":5,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors":{"value":{"location_id":"FarmHouse","connector_count":1,"connectors":[{"kind":"warp","tile_x":2,"tile_y":3,"target_location":"Cellar","target_x":5,"target_y":5,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
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
        .Replace(
            "START_X",
            rangeStartX.ToString())
        .Replace(
            "END_X",
            rangeEndX.ToString())
        .Replace(
            "LEGAL_COUNT",
            Math.Max(
                0,
                rangeEndX - rangeStartX + 1).ToString())
        .Replace(
            "STORAGE_FINGERPRINT",
            "storage-placement:" +
            rangeStartX + "-" + rangeEndX)
        .Replace(
            "REMOTE_STORAGE_LOCATION",
            remoteStorageLocation);
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
                stateJson,
                JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-25T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string Parameter(
        IEnumerable<SmallModelActionParameter> parameters,
        string name) =>
        parameters.Single(row =>
            row.Name == name).Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
