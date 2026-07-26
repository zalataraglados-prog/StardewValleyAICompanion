using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CrossLocationMachineRelocationMainlineTests
{
    [Fact]
    public void PositiveOneConnectorTargetClusterCompilesRemovalHead()
    {
        var snapshot = Snapshot(
            "Farm",
            sourcePresent: true,
            inventoryMachine: false);
        var availability = Evaluate(snapshot, Ledger());
        var candidate = Assert.Single(
            availability.Options[0].EventCandidates.Where(row =>
                row.Kind == "relocate_machine_item"));

        Assert.True(
            candidate.Available,
            string.Join(";", candidate.BlockReasons));
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(
            "FarmHouse",
            Parameter(
                candidate.Parameters,
                "relocation_target_location_id"));
        Assert.Equal(
            "1",
            Parameter(
                candidate.Parameters,
                "relocation_route_connector_count"));
        Assert.Equal(
            "existing_machine_cluster_one_connector_over_eight_cycles",
            Parameter(
                candidate.Parameters,
                "layout_benefit_policy"));
        Assert.Equal(
            "connector_arrival_static_bfs_reachable_native_legal_then_runtime_rechecked",
            Parameter(
                candidate.Parameters,
                "relocation_target_selection_policy"));
        Assert.Equal(31, int.Parse(Parameter(
            candidate.Parameters,
            "relocation_target_tile_x")));
        Assert.Equal(29, int.Parse(Parameter(
            candidate.Parameters,
            "relocation_target_tile_y")));
        Assert.Equal(31, int.Parse(Parameter(
            candidate.Parameters,
            "relocation_target_stand_tile_x")));
        Assert.Equal(30, int.Parse(Parameter(
            candidate.Parameters,
            "relocation_target_stand_tile_y")));
        Assert.Equal(4, int.Parse(Parameter(
            candidate.Parameters,
            "relocation_target_route_distance_tiles")));
        Assert.True(
            int.Parse(Parameter(
                candidate.Parameters,
                "layout_net_benefit_ticks")) > 0);

        var plan = Plan(snapshot, availability, candidate.CandidateId);
        var removalStep = Assert.Single(plan.Steps.Where(row =>
            row.Kind == "remove_machine_item"));
        var intent = Intent(
            candidate,
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot,
            Ledger(intent));
        var removal = Assert.Single(queue.Items.Where(row =>
            row.OptionId == "executor.remove_machine"));

        Assert.Equal("pending", removal.Status);
        Assert.Empty(removal.BlockingReasons);
        Assert.Equal(
            "FarmHouse",
            Parameter(
                removalStep.Parameters,
                "relocation_target_location_id"));
        Assert.Equal(
            "4",
            Parameter(
                removalStep.Parameters,
                "relocation_target_route_distance_tiles"));
    }

    [Fact]
    public void RecoveredMachineRoutesOnlyToIntentTargetThenPlacesExactly()
    {
        var source = Snapshot(
            "Farm",
            sourcePresent: true,
            inventoryMachine: false);
        var sourceAvailability = Evaluate(source, Ledger());
        var relocation = Assert.Single(
            sourceAvailability.Options[0].EventCandidates.Where(row =>
                row.Kind == "relocate_machine_item"));
        var intent = Intent(relocation, source.StateHash);
        var ledger = Ledger(intent);

        var recovered = Snapshot(
            "Farm",
            sourcePresent: false,
            inventoryMachine: true);
        var recoveredAvailability = Evaluate(recovered, ledger);

        Assert.DoesNotContain(
            recoveredAvailability.Options[0].EventCandidates,
            row => row.Kind == "place_machine_item");
        var route = Assert.Single(
            recoveredAvailability.Options[0].EventCandidates.Where(row =>
                row.Kind == "route_connector_tile" &&
                row.CandidateId.StartsWith(
                    "machine-place-route:",
                    StringComparison.Ordinal) &&
                Parameter(
                    row.Parameters,
                    "continuation.machine_location_id") ==
                    "FarmHouse" &&
                Parameter(
                    row.Parameters,
                    "continuation.relocation_intent_id") ==
                    intent.IntentId));
        Assert.True(route.Available, string.Join(";", route.BlockReasons));

        var target = Snapshot(
            "FarmHouse",
            sourcePresent: false,
            inventoryMachine: true);
        var targetAvailability = Evaluate(target, ledger);
        var place = Assert.Single(
            targetAvailability.Options[0].EventCandidates.Where(row =>
                row.Kind == "place_machine_item"));

        Assert.True(place.Available, string.Join(";", place.BlockReasons));
        Assert.Equal(intent.TargetTileX, place.TileX);
        Assert.Equal(intent.TargetTileY, place.TileY);
        Assert.Equal(
            intent.IntentId,
            Parameter(place.Parameters, "relocation_intent_id"));

        var targetPlan = Plan(
            target,
            targetAvailability,
            place.CandidateId);
        var targetQueue = new ActionQueueCompiler().Compile(
            targetPlan,
            target,
            ledger);
        var placement = Assert.Single(targetQueue.Items.Where(row =>
            row.OptionId == "executor.place_machine"));

        Assert.Equal("pending", placement.Status);
        Assert.Empty(placement.BlockingReasons);
    }

    [Fact]
    public void KeepsBestRelocationCandidateForEachTargetLocation()
    {
        var snapshot = Snapshot(
            "Farm",
            sourcePresent: true,
            inventoryMachine: false,
            sameLocationBenefit: true);

        var candidates = Evaluate(snapshot, Ledger())
            .Options[0]
            .EventCandidates
            .Where(row => row.Kind == "relocate_machine_item")
            .ToArray();

        Assert.Equal(2, candidates.Length);
        Assert.Equal(
            new[] { "Farm", "FarmHouse" },
            candidates
                .Select(row => Parameter(
                    row.Parameters,
                    "relocation_target_location_id"))
                .OrderBy(row => row, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void ExcludesCrossLocationCandidateWhenDeepTargetIsUnreachable()
    {
        var snapshot = Snapshot(
            "Farm",
            sourcePresent: true,
            inventoryMachine: false,
            targetReachable: false);

        Assert.DoesNotContain(
            Evaluate(snapshot, Ledger())
                .Options[0]
                .EventCandidates,
            row => row.Kind == "relocate_machine_item" &&
                Parameter(
                    row.Parameters,
                    "relocation_target_location_id") ==
                    "FarmHouse");
    }

    [Fact]
    public void ExcludesCrossLocationCandidateWhenReachabilityCountDrifts()
    {
        var snapshot = Snapshot(
            "Farm",
            sourcePresent: true,
            inventoryMachine: false,
            malformedReachabilityCount: true);

        Assert.DoesNotContain(
            Evaluate(snapshot, Ledger())
                .Options[0]
                .EventCandidates,
            row => row.Kind == "relocate_machine_item" &&
                Parameter(
                    row.Parameters,
                    "relocation_target_location_id") ==
                    "FarmHouse");
    }

    private static OptionAvailabilityEnvelope Evaluate(
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "farm.process_machines" },
            includeExecutorCalibrationOptions: true,
            ledger);

    private static SmallModelPlanEnvelope Plan(
        SnapshotEnvelope snapshot,
        OptionAvailabilityEnvelope availability,
        string candidateId)
    {
        var ranked = new EventCandidateRanker()
            .Rank(new(), availability)
            .Where(row => row.CandidateId == candidateId)
            .ToArray();
        return new DailyPlanCompiler().Compile(
            ranked,
            snapshot.StateHash);
    }

    private static MachineRelocationIntent Intent(
        StardewAI.Contracts.Options.EventCandidate candidate,
        string sourceStateHash) => new()
        {
            IntentId = Parameter(
                candidate.Parameters,
                "relocation_intent_id"),
            Revision = 1,
            Status = StrategyCommitmentStatuses.Active,
            SourceStateHash = sourceStateHash,
            QualifiedItemId = "(BC)12",
            ItemId = "12",
            SourceLocationId = "Farm",
            SourceTileX = 15,
            SourceTileY = 5,
            TargetLocationId = "FarmHouse",
            TargetTileX = int.Parse(Parameter(
                candidate.Parameters,
                "relocation_target_tile_x")),
            TargetTileY = int.Parse(Parameter(
                candidate.Parameters,
                "relocation_target_tile_y")),
            MachinePlacementProjectionFingerprint =
                "machine-layout:cross",
            LayoutNetBenefitTicks = int.Parse(Parameter(
                candidate.Parameters,
                "layout_net_benefit_ticks")),
            RouteConnectorCount = 1,
            LayoutRelocationCostTicks = int.Parse(Parameter(
                candidate.Parameters,
                "layout_relocation_cost_ticks")),
            RouteConnectorKind = Parameter(
                candidate.Parameters,
                "relocation_route_connector_kind"),
            RouteEstimatedTicks = int.Parse(Parameter(
                candidate.Parameters,
                "relocation_route_estimated_ticks")),
            TargetArrivalTileX = int.Parse(Parameter(
                candidate.Parameters,
                "relocation_target_arrival_tile_x")),
            TargetArrivalTileY = int.Parse(Parameter(
                candidate.Parameters,
                "relocation_target_arrival_tile_y")),
            TargetStandTileX = int.Parse(Parameter(
                candidate.Parameters,
                "relocation_target_stand_tile_x")),
            TargetStandTileY = int.Parse(Parameter(
                candidate.Parameters,
                "relocation_target_stand_tile_y")),
            TargetRouteDistanceTiles = int.Parse(Parameter(
                candidate.Parameters,
                "relocation_target_route_distance_tiles")),
            LayoutBenefitPolicy =
                "existing_machine_cluster_one_connector_over_eight_cycles",
            TargetSelectionPolicy =
                "connector_arrival_static_bfs_reachable_native_legal_then_runtime_rechecked",
            TimeEstimatePolicy =
                "source_approach_plus_live_connector_plus_target_static_bfs_runtime_rechecked"
        };

    private static StrategyCommitmentLedger Ledger(
        params MachineRelocationIntent[] intents) => new()
        {
            LedgerId = "strategy-ledger:test",
            Revision = 3,
            MachineRelocationIntents = intents
        };

    private static SnapshotEnvelope Snapshot(
        string currentLocation,
        bool sourcePresent,
        bool inventoryMachine,
        bool sameLocationBenefit = false,
        bool targetReachable = true,
        bool malformedReachabilityCount = false)
    {
        var farmStaticRanges = sameLocationBenefit
            ? """[{"y":5,"start_x":6,"end_x":7}]"""
            : """[{"y":5,"start_x":18,"end_x":19}]""";
        var inventory = inventoryMachine
            ? """
              [{"slot_index":4,"item_id":"12","qualified_item_id":"(BC)12","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}]
              """
            : """
              [{"slot_index":1,"item_id":"Pickaxe","qualified_item_id":"(T)Pickaxe","stack":1,"is_empty":false}]
              """;
        var placementRows = inventoryMachine
            ? """
              [{
                "projection_role":"inventory_machine",
                "inventory_slot_index":4,
                "item_id":"12",
                "qualified_item_id":"(BC)12",
                "stack":1,
                "locations":[
                  {
                    "location_id":"Farm",
                    "location_is_current":CURRENT_IS_FARM,
                    "location_is_player_controlled":true,
                    "machine_operational_context_valid":true,
                    "placement_probe_status":"native_legal_tiles_available",
                    "map_width":80,
                    "map_height":70,
                    "static_legal_tile_count":2,
                    "static_legal_tile_ranges":FARM_STATIC_RANGES
                  },
                  {
                    "location_id":"FarmHouse",
                    "location_is_current":CURRENT_IS_HOME,
                    "location_is_player_controlled":true,
                    "machine_operational_context_valid":true,
                    "placement_probe_status":"native_legal_tiles_available",
                    "map_width":80,
                    "map_height":40,
                    "static_legal_tile_count":1,
                    "static_legal_tile_ranges":[{"y":29,"start_x":31,"end_x":31}]
                  }
                ]
              }]
              """
            : "[]";
        var sourceMachine = sourcePresent
            ? """
              {
                "location_id":"Farm",
                "location_is_current":true,
                "tile_x":15,
                "tile_y":5,
                "item_id":"12",
                "qualified_item_id":"(BC)12",
                "machine_has_input":true,
                "machine_has_output":true,
                "removal_status":"safe_idle_native_pickaxe",
                "removal_safe_now":true,
                "removal_tool_slot_index":1,
                "removal_tool_qualified_item_id":"(T)Pickaxe",
                "removal_native_contract":"native_pickaxe",
                "removal_projection_fingerprint":"source-fingerprint"
              },
              """
            : string.Empty;
        var stateJson = """
        {
          "player":{
            "location_id":{"value":"CURRENT_LOCATION","status":"available"},
            "tile_x":{"value":14,"status":"available"},
            "tile_y":{"value":5,"status":"available"},
            "energy":{"value":270,"status":"available"},
            "inventory":{"value":INVENTORY,"status":"available"},
            "inventory_capacity":{"value":{"occupied_stacks":1,"empty_slots":11,"has_empty_slot":true},"status":"available"},
            "machine_placement":{"value":{
              "projection_status":"complete_inventory_and_relocation_machine_types_across_loaded_persistent_locations",
              "static_projection_fingerprint":"machine-layout:cross",
              "rows":PLACEMENT_ROWS,
              "relocation_rows":[{
                "projection_role":"placed_machine_relocation_probe",
                "item_id":"12",
                "qualified_item_id":"(BC)12",
                "locations":[
                  {
                    "location_id":"Farm",
                    "location_is_current":CURRENT_IS_FARM,
                    "location_is_player_controlled":true,
                    "machine_operational_context_valid":true,
                    "placement_probe_status":"native_legal_tiles_available",
                    "map_width":80,
                    "map_height":70,
                    "static_legal_tile_count":2,
                    "static_legal_tile_ranges":FARM_STATIC_RANGES
                  },
                  {
                    "location_id":"FarmHouse",
                    "location_is_current":CURRENT_IS_HOME,
                    "location_is_player_controlled":true,
                    "machine_operational_context_valid":true,
                    "placement_probe_status":"native_legal_tiles_available",
                    "map_width":80,
                    "map_height":40,
                    "static_legal_tile_count":1,
                    "static_legal_tile_ranges":[{"y":29,"start_x":31,"end_x":31}]
                  }
                ]
              }],
              "relocation_route_reachability":{
                "schema_version":"machine_relocation_route_reachability.v1",
                "projection_status":"complete_static_native_walkability_for_relocation_scope",
                "locations":[{
                  "location_id":"FarmHouse",
                  "projection_status":"native_static_walkable_tiles_available",
                  "map_width":80,
                  "map_height":40,
                  "static_walkable_tile_count":TARGET_REACHABILITY_COUNT,
                  "static_walkable_tile_ranges":TARGET_REACHABILITY_RANGES
                }]
              }
            },"status":"available"}
          },
          "farm":{
            "machines":{"value":[
              SOURCE_MACHINE
              {
                "location_id":"Farm",
                "tile_x":5,
                "tile_y":5,
                "item_id":"12",
                "qualified_item_id":"(BC)12",
                "machine_has_input":true,
                "machine_has_output":true,
                "removal_status":"blocked",
                "removal_safe_now":false
              },
              {
                "location_id":"FarmHouse",
                "tile_x":29,
                "tile_y":29,
                "item_id":"12",
                "qualified_item_id":"(BC)12",
                "machine_has_input":true,
                "machine_has_output":true,
                "removal_status":"blocked",
                "removal_safe_now":false
              },
              {
                "location_id":"FarmHouse",
                "tile_x":30,
                "tile_y":29,
                "item_id":"12",
                "qualified_item_id":"(BC)12",
                "machine_has_input":true,
                "machine_has_output":true,
                "removal_status":"blocked",
                "removal_safe_now":false
              }
            ],"status":"available"}
          },
          "current_location":{
            "map":{"value":{"width":80,"height":70},"status":"available"}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"CURRENT_LOCATION","width":80,"height":70,"notable_tiles":[]},"status":"available"},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available"},
            "route_graph":{"value":{"edges":[
              {"kind":"building_door","from_location":"Farm","from_x":20,"from_y":5,"target_location":"FarmHouse","target_x":27,"target_y":30,"resolved":true},
              {"kind":"building_door","from_location":"FarmHouse","from_x":27,"from_y":30,"target_location":"Farm","target_x":20,"target_y":5,"resolved":true}
            ]},"status":"available"},
            "route_connectors":{"value":{
              "location_id":"CURRENT_LOCATION",
              "connectors":CONNECTORS
            },"status":"available"}
          },
          "menus":{
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}
          },
          "time":{
            "time":{"value":600,"status":"available"}
          }
        }
        """
        .Replace("INVENTORY", inventory)
        .Replace("PLACEMENT_ROWS", placementRows)
        .Replace("FARM_STATIC_RANGES", farmStaticRanges)
        .Replace("SOURCE_MACHINE", sourceMachine)
        .Replace(
            "TARGET_REACHABILITY_RANGES",
            targetReachable
                ? """
                  [
                    {"y":29,"start_x":27,"end_x":28},
                    {"y":29,"start_x":31,"end_x":31},
                    {"y":30,"start_x":27,"end_x":31}
                  ]
                  """
                : """
                  [
                    {"y":29,"start_x":31,"end_x":31},
                    {"y":30,"start_x":27,"end_x":27}
                  ]
                  """)
        .Replace(
            "TARGET_REACHABILITY_COUNT",
            malformedReachabilityCount
                ? "7"
                : targetReachable
                    ? "8"
                    : "2")
        .Replace(
            "CONNECTORS",
            currentLocation == "Farm"
                ? """
                  [{"kind":"building_door","tile_x":20,"tile_y":5,"target_location":"FarmHouse","target_x":27,"target_y":30,"resolved":true}]
                  """
                : """
                  [{"kind":"building_door","tile_x":27,"tile_y":30,"target_location":"Farm","target_x":20,"target_y":5,"resolved":true}]
                  """)
        .Replace(
            "CURRENT_IS_FARM",
            (currentLocation == "Farm").ToString().ToLowerInvariant())
        .Replace(
            "CURRENT_IS_HOME",
            (currentLocation == "FarmHouse").ToString().ToLowerInvariant())
        .Replace("CURRENT_LOCATION", currentLocation);
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
                stateJson,
                JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-26T06:00:00Z",
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
