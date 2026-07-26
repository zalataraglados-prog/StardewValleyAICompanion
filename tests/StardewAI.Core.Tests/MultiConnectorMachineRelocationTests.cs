using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Tests;

public sealed class MultiConnectorMachineRelocationTests
{
    [Fact]
    public void CandidateCommitsExactTwoConnectorRouteAndFullCost()
    {
        var snapshot = Snapshot(
            "Farm",
            sourcePresent: true,
            inventoryMachine: false);
        var candidate = RelocationCandidate(
            Evaluate(snapshot, Ledger()));
        var segments = JsonSerializer.Deserialize<
            MachineRelocationRouteSegment[]>(Parameter(
                candidate,
                "relocation_route_segments_json"))!;

        Assert.Equal(2, segments.Length);
        Assert.Collection(
            segments,
            first =>
            {
                Assert.Equal("Farm", first.FromLocationId);
                Assert.Equal("FarmHouse", first.TargetLocationId);
                Assert.Equal("building_door", first.Kind);
                Assert.Equal(360, first.EstimatedTicks);
            },
            second =>
            {
                Assert.Equal("FarmHouse", second.FromLocationId);
                Assert.Equal("Cellar", second.TargetLocationId);
                Assert.Equal("action_warp", second.Kind);
                Assert.Equal(51, second.ApproachDistanceTiles);
                Assert.Equal(3120, second.EstimatedTicks);
            });
        Assert.Equal(
            3480,
            int.Parse(Parameter(
                candidate,
                "relocation_route_estimated_ticks")));
        Assert.Equal(
            "2",
            Parameter(
                candidate,
                "relocation_route_connector_count"));
        Assert.Equal(
            "existing_machine_cluster_resolved_route_over_eight_cycles",
            Parameter(candidate, "layout_benefit_policy"));
    }

    [Fact]
    public void RecoveredMachineFollowsCommittedSuffixThenPlacesExactly()
    {
        var source = Snapshot(
            "Farm",
            sourcePresent: true,
            inventoryMachine: false);
        var intent = Intent(
            RelocationCandidate(Evaluate(source, Ledger())),
            source.StateHash);
        var ledger = Ledger(intent);

        var farmRoute = PlacementRoute(
            Evaluate(
                Snapshot(
                    "Farm",
                    sourcePresent: false,
                    inventoryMachine: true),
                ledger));
        Assert.True(
            farmRoute.Available,
            string.Join(";", farmRoute.BlockReasons));
        Assert.Equal(
            "0",
            Parameter(
                farmRoute,
                "machine_route.committed_segment_index"));
        Assert.Equal(
            "2",
            Parameter(
                farmRoute,
                "machine_route.remaining_connector_count"));

        var houseRoute = PlacementRoute(
            Evaluate(
                Snapshot(
                    "FarmHouse",
                    sourcePresent: false,
                    inventoryMachine: true),
                ledger));
        Assert.True(
            houseRoute.Available,
            string.Join(";", houseRoute.BlockReasons));
        Assert.Equal(
            "1",
            Parameter(
                houseRoute,
                "machine_route.committed_segment_index"));
        Assert.Equal(
            "1",
            Parameter(
                houseRoute,
                "machine_route.remaining_connector_count"));
        Assert.Equal("Cellar", Parameter(
            houseRoute,
            "expected_target_location"));

        var target = Evaluate(
            Snapshot(
                "Cellar",
                sourcePresent: false,
                inventoryMachine: true),
            ledger);
        var placement = Assert.Single(
            target.Options[0].EventCandidates.Where(candidate =>
                candidate.Kind == "place_machine_item"));
        Assert.True(
            placement.Available,
            string.Join(";", placement.BlockReasons));
        Assert.Equal(intent.TargetTileX, placement.TileX);
        Assert.Equal(intent.TargetTileY, placement.TileY);
    }

    [Fact]
    public void IntermediateConnectorDriftBlocksCommittedContinuation()
    {
        var source = Snapshot(
            "Farm",
            sourcePresent: true,
            inventoryMachine: false);
        var intent = Intent(
            RelocationCandidate(Evaluate(source, Ledger())),
            source.StateHash);
        var availability = Evaluate(
            Snapshot(
                "FarmHouse",
                sourcePresent: false,
                inventoryMachine: true,
                driftSecondConnector: true),
            Ledger(intent));
        var route = PlacementRoute(availability);

        Assert.False(route.Available);
        Assert.Contains(
            "machine_relocation_committed_route_drifted",
            route.BlockReasons);
    }

    [Fact]
    public void LedgerAcceptsExactRouteAndRejectsIntermediateCostDrift()
    {
        var snapshot = Snapshot(
            "Farm",
            sourcePresent: true,
            inventoryMachine: false);
        var candidate = RelocationCandidate(
            Evaluate(snapshot, Ledger()));
        var request = UpsertRequest(candidate, snapshot);
        var service = new MachineRelocationIntentLedgerService();

        var accepted = service.Upsert(
            null,
            snapshot,
            request,
            "2026-07-26T10:00:00Z");

        Assert.True(
            accepted.Accepted,
            string.Join(";", accepted.Errors));
        Assert.Equal(
            2,
            Assert.Single(
                accepted.Ledger!.MachineRelocationIntents)
                .RouteSegments.Length);

        request.RouteSegments[1].ApproachDistanceTiles--;
        var drifted = service.Upsert(
            null,
            snapshot,
            request,
            "2026-07-26T10:01:00Z");

        Assert.False(drifted.Accepted);
        Assert.Contains(
            "machine_relocation_resolved_connector_drifted",
            drifted.Errors);
    }

    private static EventCandidate RelocationCandidate(
        OptionAvailabilityEnvelope availability)
    {
        var candidates = availability.Options[0].EventCandidates
            .Where(candidate =>
                candidate.Kind == "relocate_machine_item")
            .ToArray();
        Assert.True(
            candidates.Length == 1,
            string.Join(
                Environment.NewLine,
                availability.Options[0].EventCandidates.Select(
                    candidate =>
                        candidate.Kind + ":" +
                        candidate.CandidateId + ":" +
                        string.Join(",", candidate.BlockReasons))));
        return candidates[0];
    }

    private static EventCandidate PlacementRoute(
        OptionAvailabilityEnvelope availability) =>
        Assert.Single(availability.Options[0].EventCandidates.Where(
            candidate =>
                candidate.Kind == "route_connector_tile" &&
                candidate.CandidateId.StartsWith(
                    "machine-place-route:",
                    StringComparison.Ordinal)));

    private static OptionAvailabilityEnvelope Evaluate(
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "farm.process_machines" },
            includeExecutorCalibrationOptions: true,
            ledger);

    private static MachineRelocationIntent Intent(
        EventCandidate candidate,
        string sourceStateHash) => new()
        {
            IntentId = Parameter(
                candidate,
                "relocation_intent_id"),
            Revision = 1,
            Status = StrategyCommitmentStatuses.Active,
            SourceStateHash = sourceStateHash,
            QualifiedItemId = "(BC)12",
            ItemId = "12",
            SourceLocationId = "Farm",
            SourceTileX = 15,
            SourceTileY = 5,
            TargetLocationId = "Cellar",
            TargetTileX = int.Parse(Parameter(
                candidate,
                "relocation_target_tile_x")),
            TargetTileY = int.Parse(Parameter(
                candidate,
                "relocation_target_tile_y")),
            MachinePlacementProjectionFingerprint =
                "machine-layout:multi",
            LayoutNetBenefitTicks = int.Parse(Parameter(
                candidate,
                "layout_net_benefit_ticks")),
            RouteConnectorCount = int.Parse(Parameter(
                candidate,
                "relocation_route_connector_count")),
            RouteConnectorKind = Parameter(
                candidate,
                "relocation_route_connector_kind"),
            RouteEstimatedTicks = int.Parse(Parameter(
                candidate,
                "relocation_route_estimated_ticks")),
            RouteSegments = JsonSerializer.Deserialize<
                MachineRelocationRouteSegment[]>(Parameter(
                    candidate,
                    "relocation_route_segments_json"))!,
            TargetArrivalTileX = int.Parse(Parameter(
                candidate,
                "relocation_target_arrival_tile_x")),
            TargetArrivalTileY = int.Parse(Parameter(
                candidate,
                "relocation_target_arrival_tile_y")),
            TargetStandTileX = int.Parse(Parameter(
                candidate,
                "relocation_target_stand_tile_x")),
            TargetStandTileY = int.Parse(Parameter(
                candidate,
                "relocation_target_stand_tile_y")),
            TargetRouteDistanceTiles = int.Parse(Parameter(
                candidate,
                "relocation_target_route_distance_tiles")),
            LayoutRelocationCostTicks = int.Parse(Parameter(
                candidate,
                "layout_relocation_cost_ticks")),
            LayoutBenefitPolicy = Parameter(
                candidate,
                "layout_benefit_policy"),
            TargetSelectionPolicy = Parameter(
                candidate,
                "relocation_target_selection_policy"),
            TimeEstimatePolicy = Parameter(
                candidate,
                "layout_time_estimate_policy")
        };

    private static MachineRelocationIntentUpsertRequest UpsertRequest(
        EventCandidate candidate,
        SnapshotEnvelope snapshot) => new()
        {
            StateHash = snapshot.StateHash,
            IntentId = Parameter(
                candidate,
                "relocation_intent_id"),
            SourceDecisionId = candidate.CandidateId,
            QualifiedItemId = "(BC)12",
            ItemId = "12",
            SourceLocationId = "Farm",
            SourceTileX = 15,
            SourceTileY = 5,
            TargetLocationId = "Cellar",
            TargetTileX = int.Parse(Parameter(
                candidate,
                "relocation_target_tile_x")),
            TargetTileY = int.Parse(Parameter(
                candidate,
                "relocation_target_tile_y")),
            MachinePlacementProjectionFingerprint =
                "machine-layout:multi",
            LayoutNetBenefitTicks = int.Parse(Parameter(
                candidate,
                "layout_net_benefit_ticks")),
            RouteConnectorCount = int.Parse(Parameter(
                candidate,
                "relocation_route_connector_count")),
            RouteConnectorKind = Parameter(
                candidate,
                "relocation_route_connector_kind"),
            RouteEstimatedTicks = int.Parse(Parameter(
                candidate,
                "relocation_route_estimated_ticks")),
            RouteSegments = JsonSerializer.Deserialize<
                MachineRelocationRouteSegment[]>(Parameter(
                    candidate,
                    "relocation_route_segments_json"))!,
            TargetArrivalTileX = int.Parse(Parameter(
                candidate,
                "relocation_target_arrival_tile_x")),
            TargetArrivalTileY = int.Parse(Parameter(
                candidate,
                "relocation_target_arrival_tile_y")),
            TargetStandTileX = int.Parse(Parameter(
                candidate,
                "relocation_target_stand_tile_x")),
            TargetStandTileY = int.Parse(Parameter(
                candidate,
                "relocation_target_stand_tile_y")),
            TargetRouteDistanceTiles = int.Parse(Parameter(
                candidate,
                "relocation_target_route_distance_tiles")),
            LayoutRelocationCostTicks = int.Parse(Parameter(
                candidate,
                "layout_relocation_cost_ticks")),
            LayoutBenefitPolicy = Parameter(
                candidate,
                "layout_benefit_policy"),
            TargetSelectionPolicy = Parameter(
                candidate,
                "relocation_target_selection_policy"),
            TimeEstimatePolicy = Parameter(
                candidate,
                "layout_time_estimate_policy")
        };

    private static StrategyCommitmentLedger Ledger(
        params MachineRelocationIntent[] intents) => new()
        {
            LedgerId = "strategy-ledger:multi",
            Revision = 1,
            MachineRelocationIntents = intents
        };

    private static SnapshotEnvelope Snapshot(
        string currentLocation,
        bool sourcePresent,
        bool inventoryMachine,
        bool driftSecondConnector = false)
    {
        var currentX = currentLocation switch
        {
            "FarmHouse" => 27,
            "Cellar" => 5,
            _ => 14
        };
        var currentY = currentLocation switch
        {
            "FarmHouse" => 30,
            _ => 5
        };
        var inventory = inventoryMachine
            ? """
              [{"slot_index":4,"item_id":"12","qualified_item_id":"(BC)12","stack":1,"is_empty":false}]
              """
            : """
              [{"slot_index":1,"item_id":"Pickaxe","qualified_item_id":"(T)Pickaxe","stack":1,"is_empty":false}]
              """;
        var placementRows = inventoryMachine
            ? """
              [{
                "inventory_slot_index":4,
                "item_id":"12",
                "qualified_item_id":"(BC)12",
                "stack":1,
                "locations":[{
                  "location_id":"Cellar",
                  "location_is_player_controlled":true,
                  "machine_operational_context_valid":true,
                  "placement_probe_status":"native_legal_tiles_available",
                  "static_legal_tile_count":1,
                  "static_legal_tile_ranges":[{"y":5,"start_x":9,"end_x":9}]
                }]
              }]
              """
            : "[]";
        var sourceMachine = sourcePresent
            ? """
              {
                "location_id":"Farm","tile_x":15,"tile_y":5,
                "item_id":"12","qualified_item_id":"(BC)12",
                "machine_has_input":true,"machine_has_output":true,
                "removal_status":"safe_idle_native_pickaxe",
                "removal_safe_now":true,
                "removal_tool_slot_index":1,
                "removal_tool_qualified_item_id":"(T)Pickaxe",
                "removal_native_contract":"native_pickaxe",
                "removal_projection_fingerprint":"source-fingerprint"
              },
              """
            : string.Empty;
        var secondX = driftSecondConnector ? 4 : 2;
        var connectors = currentLocation switch
        {
            "Farm" => """
              [{"kind":"building_door","tile_x":20,"tile_y":5,
                "target_location":"FarmHouse","target_x":27,"target_y":30,
                "resolved":true}]
              """,
            "FarmHouse" => $$"""
              [{"kind":"action_warp","tile_x":{{secondX}},"tile_y":3,
                "target_location":"Cellar","target_x":5,"target_y":5,
                "resolved":true,"source_property":"Buildings.Action"}]
              """,
            _ => "[]"
        };
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"{{{currentLocation}}}","status":"available"},
            "tile_x":{"value":{{{currentX}}},"status":"available"},
            "tile_y":{"value":{{{currentY}}},"status":"available"},
            "energy":{"value":270,"status":"available"},
            "inventory":{"value":{{{inventory}}},"status":"available"},
            "inventory_capacity":{"value":{"occupied_stacks":1,"empty_slots":11,"has_empty_slot":true},"status":"available"},
            "machine_placement":{"value":{
              "static_projection_fingerprint":"machine-layout:multi",
              "rows":{{{placementRows}}},
              "relocation_rows":[{
                "item_id":"12","qualified_item_id":"(BC)12",
                "locations":[{
                  "location_id":"Cellar",
                  "location_is_player_controlled":true,
                  "machine_operational_context_valid":true,
                  "placement_probe_status":"native_legal_tiles_available",
                  "map_width":40,"map_height":30,
                  "static_legal_tile_count":1,
                  "static_legal_tile_ranges":[{"y":5,"start_x":9,"end_x":9}]
                }]
              }],
              "relocation_route_reachability":{
                "projection_status":"complete_static_native_walkability_for_relocation_scope",
                "locations":[
                  {
                    "location_id":"FarmHouse",
                    "projection_status":"native_static_walkable_tiles_available",
                    "map_width":80,"map_height":40,
                    "static_walkable_tile_count":53,
                    "static_walkable_tile_ranges":[
                      {"y":3,"start_x":2,"end_x":2},
                      {"y":4,"start_x":2,"end_x":2},
                      {"y":5,"start_x":2,"end_x":2},
                      {"y":6,"start_x":2,"end_x":2},
                      {"y":7,"start_x":2,"end_x":2},
                      {"y":8,"start_x":2,"end_x":2},
                      {"y":9,"start_x":2,"end_x":2},
                      {"y":10,"start_x":2,"end_x":2},
                      {"y":11,"start_x":2,"end_x":2},
                      {"y":12,"start_x":2,"end_x":2},
                      {"y":13,"start_x":2,"end_x":2},
                      {"y":14,"start_x":2,"end_x":2},
                      {"y":15,"start_x":2,"end_x":2},
                      {"y":16,"start_x":2,"end_x":2},
                      {"y":17,"start_x":2,"end_x":2},
                      {"y":18,"start_x":2,"end_x":2},
                      {"y":19,"start_x":2,"end_x":2},
                      {"y":20,"start_x":2,"end_x":2},
                      {"y":21,"start_x":2,"end_x":2},
                      {"y":22,"start_x":2,"end_x":2},
                      {"y":23,"start_x":2,"end_x":2},
                      {"y":24,"start_x":2,"end_x":2},
                      {"y":25,"start_x":2,"end_x":2},
                      {"y":26,"start_x":2,"end_x":2},
                      {"y":27,"start_x":2,"end_x":2},
                      {"y":28,"start_x":2,"end_x":2},
                      {"y":29,"start_x":2,"end_x":2},
                      {"y":30,"start_x":2,"end_x":27}
                    ]
                  },
                  {
                    "location_id":"Cellar",
                    "projection_status":"native_static_walkable_tiles_available",
                    "map_width":40,"map_height":30,
                    "static_walkable_tile_count":5,
                    "static_walkable_tile_ranges":[
                      {"y":5,"start_x":5,"end_x":9}
                    ]
                  }
                ]
              }
            },"status":"available"}
          },
          "farm":{"machines":{"value":[
            {{{sourceMachine}}}
            {"location_id":"Farm","tile_x":5,"tile_y":5,
             "item_id":"12","qualified_item_id":"(BC)12",
             "machine_has_input":true,"machine_has_output":true,
             "removal_safe_now":false},
            {"location_id":"Cellar","tile_x":10,"tile_y":5,
             "item_id":"12","qualified_item_id":"(BC)12",
             "machine_has_input":true,"machine_has_output":true,
             "removal_safe_now":false},
            {"location_id":"Cellar","tile_x":11,"tile_y":5,
             "item_id":"12","qualified_item_id":"(BC)12",
             "machine_has_input":true,"machine_has_output":true,
             "removal_safe_now":false}
          ],"status":"available"}},
          "current_location":{"map":{"value":{"width":80,"height":40},"status":"available"}},
          "locations":{
            "collision_grid":{"value":{
              "location_id":"{{{currentLocation}}}",
              "width":80,"height":40,"notable_tiles":[]
            },"status":"available"},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available"},
            "route_gate_context":{"value":{"action_gates":[
              {"kind":"action_warp","tile_x":{{{secondX}}},
               "tile_y":3,"allowed_now":true}
            ]},"status":"available"},
            "route_graph":{"value":{"edges":[
              {"kind":"building_door","from_location":"Farm",
               "from_x":20,"from_y":5,"target_location":"FarmHouse",
               "target_x":27,"target_y":30,"resolved":true},
              {"kind":"action_warp","from_location":"FarmHouse",
               "from_x":{{{secondX}}},"from_y":3,"target_location":"Cellar",
               "target_x":5,"target_y":5,"resolved":true}
            ]},"status":"available"},
            "route_connectors":{"value":{
              "location_id":"{{{currentLocation}}}",
              "connectors":{{{connectors}}}
            },"status":"available"}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
          "time":{"time":{"value":600,"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            SaveId = new FieldEnvelope<string?>
            {
                Value = "MultiRouteFarm",
                Status = FieldStatus.Available
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "123",
                Status = FieldStatus.Available
            },
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            State = state
        };
    }

    private static string Parameter(
        EventCandidate candidate,
        string name) =>
        candidate.Parameters.Single(parameter =>
            parameter.Name == name).Value;
}
