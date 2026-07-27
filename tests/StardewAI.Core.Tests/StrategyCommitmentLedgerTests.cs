using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Tests;

public sealed class StrategyCommitmentLedgerTests
{
    [Fact]
    public void NativeCropCatalogBindsCrossSeasonCommitmentAndRevision()
    {
        var snapshot = Snapshot(103, 1, "winter", 20);
        var service = new CropCommitmentLedgerService();
        var first = service.Upsert(null, snapshot, Request(snapshot, 0, 100), "2026-07-19T00:00:00Z");

        Assert.True(first.Accepted, string.Join(";", first.Errors));
        Assert.Equal(1, first.Ledger!.Revision);
        var commitment = Assert.Single(first.Ledger.CropPlantingCommitments);
        Assert.Equal("400", commitment.HarvestItemId);
        Assert.Equal("(O)400", commitment.HarvestItemQualifiedId);
        Assert.Equal(new[] { "category_fruits", "item_strawberry" }, commitment.HarvestContextTags);
        Assert.Equal(112, commitment.PlantingTotalDay);
        Assert.Equal(120, commitment.FirstHarvestTotalDay);
        Assert.Equal(136, commitment.LastInSeasonHarvestTotalDay);
        Assert.Equal(4, commitment.RegrowDays);
        Assert.Equal(100, commitment.MinimumUnitsPerWave);
        Assert.Equal("upsert", Assert.Single(first.Ledger.History).Operation);

        var revised = service.Upsert(first.Ledger, snapshot, Request(snapshot, 1, 120), "2026-07-19T00:01:00Z");
        Assert.True(revised.Accepted, string.Join(";", revised.Errors));
        Assert.Equal(2, revised.Ledger!.Revision);
        Assert.Equal(2, Assert.Single(revised.Ledger.CropPlantingCommitments).Revision);
        Assert.Equal(120, Assert.Single(revised.Ledger.CropPlantingCommitments).TileCount);
        Assert.Equal(new[] { "upsert", "upsert" }, revised.Ledger.History.Select(row => row.Operation));

        var stale = service.Upsert(revised.Ledger, snapshot, Request(snapshot, 1, 140), "2026-07-19T00:02:00Z");
        Assert.False(stale.Accepted);
        Assert.Contains("ledger_revision_conflict", stale.Errors);
    }

    [Fact]
    public void CancellationAndAutomaticCompletionPreserveHistory()
    {
        var snapshot = Snapshot(103, 1, "winter", 20);
        var service = new CropCommitmentLedgerService();
        var created = service.Upsert(null, snapshot, Request(snapshot, 0, 100), "2026-07-19T00:00:00Z").Ledger!;
        var cancelled = service.Cancel(created, snapshot, "year2-spring-strawberry", new StrategyCommitmentCancelRequest
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = 1,
            Reason = "strategy_reallocated_tiles"
        }, "2026-07-19T00:01:00Z");

        Assert.True(cancelled.Accepted, string.Join(";", cancelled.Errors));
        Assert.Equal(StrategyCommitmentStatuses.Cancelled, Assert.Single(cancelled.Ledger!.CropPlantingCommitments).Status);
        Assert.Equal("strategy_reallocated_tiles", Assert.Single(cancelled.Ledger.CropPlantingCommitments).CancelReason);
        Assert.Equal(new[] { "upsert", "cancel" }, cancelled.Ledger.History.Select(row => row.Operation));

        var later = Snapshot(137, 2, "spring", 26);
        var active = service.Upsert(null, snapshot, Request(snapshot, 0, 100), "2026-07-19T00:00:00Z").Ledger!;
        var completed = service.ReconcileCompleted(active, later, "2026-07-19T00:03:00Z");
        Assert.Equal(2, completed.Revision);
        Assert.Equal(StrategyCommitmentStatuses.Completed, Assert.Single(completed.CropPlantingCommitments).Status);
        Assert.Equal(new[] { "upsert", "complete" }, completed.History.Select(row => row.Operation));
    }

    [Fact]
    public void InvalidSeasonIsRejectedInsteadOfInventingCropTiming()
    {
        var snapshot = Snapshot(103, 1, "winter", 20);
        var request = Request(snapshot, 0, 100);
        request.PlantingSeason = "summer";
        request.PlantingDayOfMonth = 1;

        var result = new CropCommitmentLedgerService().Upsert(null, snapshot, request, "2026-07-19T00:00:00Z");

        Assert.False(result.Accepted);
        Assert.Contains("crop_not_plantable_in_committed_season", result.Errors);
    }

    [Fact]
    public void MaterialReservationUsesExactAuthorizedSlotAndPreservesCropCommitments()
    {
        var snapshot = Snapshot(103, 1, "winter", 20);
        var cropService = new CropCommitmentLedgerService();
        var materialService = new MaterialReservationLedgerService();
        var cropLedger = cropService.Upsert(
            null,
            snapshot,
            Request(snapshot, 0, 100),
            "2026-07-19T00:00:00Z").Ledger!;
        var created = materialService.Upsert(
            cropLedger,
            snapshot,
            MaterialRequest(snapshot, cropLedger.Revision, "build-keg", 20),
            "2026-07-19T00:01:00Z");

        Assert.True(created.Accepted, string.Join(";", created.Errors));
        Assert.Single(created.Ledger!.CropPlantingCommitments);
        var reservation = Assert.Single(created.Ledger.MaterialReservations);
        Assert.Equal(StrategyCommitmentStatuses.Active, reservation.Status);
        Assert.Equal(123, reservation.OwnerPlayerId);
        Assert.Equal("goal.machine.keg", reservation.GoalId);
        Assert.Equal(2, created.Ledger.Revision);

        var revisedCrop = cropService.Upsert(
            created.Ledger,
            snapshot,
            Request(snapshot, 2, 120),
            "2026-07-19T00:02:00Z");
        Assert.True(revisedCrop.Accepted, string.Join(";", revisedCrop.Errors));
        Assert.Single(revisedCrop.Ledger!.MaterialReservations);
    }

    [Fact]
    public void MaterialReservationRejectsOverbookingAndCancellationReleasesSupply()
    {
        var snapshot = Snapshot(103, 1, "winter", 20);
        var service = new MaterialReservationLedgerService();
        var first = service.Upsert(
            null,
            snapshot,
            MaterialRequest(snapshot, 0, "first", 20),
            "2026-07-19T00:00:00Z");
        Assert.True(first.Accepted, string.Join(";", first.Errors));

        var overbooked = service.Upsert(
            first.Ledger,
            snapshot,
            MaterialRequest(snapshot, 1, "second", 11),
            "2026-07-19T00:01:00Z");
        Assert.False(overbooked.Accepted);
        Assert.Contains(
            "material_reservation_insufficient_unreserved_quantity",
            overbooked.Errors);

        var cancelled = service.Cancel(
            first.Ledger,
            snapshot,
            "first",
            new StrategyCommitmentCancelRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = 1,
                Reason = "goal_replanned"
            },
            "2026-07-19T00:02:00Z");
        Assert.True(cancelled.Accepted, string.Join(";", cancelled.Errors));

        var replacement = service.Upsert(
            cancelled.Ledger,
            snapshot,
            MaterialRequest(snapshot, 2, "second", 30),
            "2026-07-19T00:03:00Z");
        Assert.True(replacement.Accepted, string.Join(";", replacement.Errors));
        Assert.Equal(
            StrategyCommitmentStatuses.Cancelled,
            replacement.Ledger!.MaterialReservations.Single(row => row.ReservationId == "first").Status);
    }

    [Fact]
    public void MaterialReservationRejectsUnauthorizedNodeAndStaleLedgerRevision()
    {
        var snapshot = Snapshot(103, 1, "winter", 20);
        var service = new MaterialReservationLedgerService();
        var unauthorized = MaterialRequest(snapshot, 0, "shared", 1);
        unauthorized.NodeId = "global:JunimoChests";

        var rejected = service.Upsert(
            null,
            snapshot,
            unauthorized,
            "2026-07-19T00:00:00Z");
        Assert.False(rejected.Accepted);
        Assert.Contains("material_reservation_node_not_actor_authorized", rejected.Errors);

        var first = service.Upsert(
            null,
            snapshot,
            MaterialRequest(snapshot, 0, "first", 1),
            "2026-07-19T00:01:00Z");
        var stale = service.Upsert(
            first.Ledger,
            snapshot,
            MaterialRequest(snapshot, 0, "second", 1),
            "2026-07-19T00:02:00Z");
        Assert.False(stale.Accepted);
        Assert.Contains("ledger_revision_conflict", stale.Errors);
    }

    [Fact]
    public void MachineRelocationIntentPersistsAndCompletesAtExactTarget()
    {
        var initial = MachineRelocationSnapshot(targetPresent: false);
        var service = new MachineRelocationIntentLedgerService();
        var request = new MachineRelocationIntentUpsertRequest
        {
            StateHash = initial.StateHash,
            IntentId = "layout:Farm:15,5->7,5:(BC)13",
            SourceDecisionId = "machine-relocate:test",
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
        };

        var upsert = service.Upsert(
            null,
            initial,
            request,
            "2026-07-26T01:00:00Z");

        Assert.True(
            upsert.Accepted,
            string.Join(";", upsert.Errors));
        Assert.Equal(
            StrategyCommitmentStatuses.Active,
            Assert.Single(
                upsert.Ledger!.MachineRelocationIntents).Status);

        var completed = service.ReconcileCompleted(
            upsert.Ledger,
            MachineRelocationSnapshot(targetPresent: true),
            "2026-07-26T01:01:00Z");
        var intent = Assert.Single(
            completed.MachineRelocationIntents);

        Assert.Equal(
            StrategyCommitmentStatuses.Completed,
            intent.Status);
        Assert.Equal(
            "exact_target_machine_observed",
            intent.CompletionReason);
    }

    [Fact]
    public void MachineSupportIntentBindsPlacementAndCompletesOnProcessing()
    {
        var initial = MachineSupportSnapshot(processing: false);
        var service = new MachineSupportIntentLedgerService();
        var selected = service.Upsert(
            null,
            initial,
            new MachineSupportIntentUpsertRequest
            {
                StateHash = initial.StateHash,
                IntentId = "machine-support:money:keg",
                Stage = MachineSupportIntentStages.CraftSelected,
                SourceDecisionId = "machine-craft:keg",
                GoalId = "goal.economy.earn_money",
                QualifiedItemId = "(BC)12",
                ItemId = "12",
                DemandClass =
                    "production_capacity_requirement",
                SupportKind =
                    "machine_capacity_current_backlog",
                EvidenceStatus =
                    "bounded_current_backlog_positive|complete_exact_native_consumption_sale_value",
                GrossBenefit = 400,
                OpportunityCost = 60,
                NetBenefit = 340,
                SupportScore = 0.034,
                RequiredAdditionalMachineCount = 1
            },
            "2026-07-27T01:00:00Z");

        Assert.True(
            selected.Accepted,
            string.Join(";", selected.Errors));
        var selectedIntent = Assert.Single(
            selected.Ledger!.MachineSupportIntents);
        Assert.Equal(
            MachineSupportIntentStages.CraftSelected,
            selectedIntent.Stage);
        Assert.Null(selectedIntent.TargetTileX);

        var bound = service.Upsert(
            selected.Ledger,
            initial,
            new MachineSupportIntentUpsertRequest
            {
                StateHash = initial.StateHash,
                ExpectedLedgerRevision =
                    selected.Ledger.Revision,
                IntentId = selectedIntent.IntentId,
                Stage =
                    MachineSupportIntentStages.PlacementBound,
                SourceDecisionId = "machine-place:keg",
                GoalId = selectedIntent.GoalId,
                QualifiedItemId =
                    selectedIntent.QualifiedItemId,
                ItemId = selectedIntent.ItemId,
                TargetLocationId = "Farm",
                TargetTileX = 7,
                TargetTileY = 5
            },
            "2026-07-27T01:01:00Z");

        Assert.True(
            bound.Accepted,
            string.Join(";", bound.Errors));
        var boundIntent = Assert.Single(
            bound.Ledger!.MachineSupportIntents);
        Assert.Equal(
            MachineSupportIntentStages.PlacementBound,
            boundIntent.Stage);
        Assert.Equal(7, boundIntent.TargetTileX);

        var completed = service.ReconcileCompleted(
            bound.Ledger,
            MachineSupportSnapshot(processing: true),
            "2026-07-27T01:02:00Z");
        var completedIntent = Assert.Single(
            completed.MachineSupportIntents);
        Assert.Equal(
            StrategyCommitmentStatuses.Completed,
            completedIntent.Status);
        Assert.Equal(
            "exact_target_machine_processing_observed",
            completedIntent.CompletionReason);
        Assert.Equal(
            new[]
            {
                "machine_support_select",
                "machine_support_bind_placement",
                "machine_support_complete"
            },
            completed.History.Select(row => row.Operation));
    }

    [Fact]
    public void CrossLocationRelocationRequiresTypedResolvedRouteAndBenefit()
    {
        var snapshot = CrossLocationMachineRelocationSnapshot();
        var service = new MachineRelocationIntentLedgerService();
        var request = new MachineRelocationIntentUpsertRequest
        {
            StateHash = snapshot.StateHash,
            IntentId =
                "layout:Farm:15,5->FarmHouse:36,23:(BC)13",
            SourceDecisionId = "machine-relocate:cross",
            QualifiedItemId = "(BC)13",
            ItemId = "13",
            SourceLocationId = "Farm",
            SourceTileX = 15,
            SourceTileY = 5,
            TargetLocationId = "FarmHouse",
            TargetTileX = 36,
            TargetTileY = 23,
            MachinePlacementProjectionFingerprint =
                "machine-layout:cross",
            LayoutNetBenefitTicks = 6000,
            RouteConnectorCount = 1,
            RouteConnectorKind = "building_door",
            RouteEstimatedTicks = 1200,
            RouteSegments =
            [
                new MachineRelocationRouteSegment
                {
                    Index = 0,
                    Kind = "building_door",
                    FromLocationId = "Farm",
                    FromTileX = 20,
                    FromTileY = 5,
                    TargetLocationId = "FarmHouse",
                    ArrivalTileX = 34,
                    ArrivalTileY = 24,
                    ApproachDistanceTiles = 19,
                    EstimatedTicks = 1200
                }
            ],
            TargetArrivalTileX = 34,
            TargetArrivalTileY = 24,
            TargetStandTileX = 36,
            TargetStandTileY = 24,
            TargetRouteDistanceTiles = 2,
            LayoutRelocationCostTicks = 1800,
            LayoutBenefitPolicy =
                "existing_machine_cluster_resolved_route_over_eight_cycles",
            TargetSelectionPolicy =
                "resolved_route_final_arrival_static_bfs_reachable_native_legal_then_runtime_rechecked",
            TimeEstimatePolicy =
                "source_approach_plus_resolved_route_static_bfs_plus_target_static_bfs_runtime_rechecked"
        };

        var accepted = service.Upsert(
            null,
            snapshot,
            request,
            "2026-07-26T06:00:00Z");

        Assert.True(
            accepted.Accepted,
            string.Join(";", accepted.Errors));
        var intent = Assert.Single(
            accepted.Ledger!.MachineRelocationIntents);
        Assert.Equal("FarmHouse", intent.TargetLocationId);
        Assert.Equal(1, intent.RouteConnectorCount);
        Assert.Equal("building_door", intent.RouteConnectorKind);
        Assert.Equal(1200, intent.RouteEstimatedTicks);
        Assert.Equal(34, intent.TargetArrivalTileX);
        Assert.Equal(24, intent.TargetArrivalTileY);
        Assert.Equal(36, intent.TargetStandTileX);
        Assert.Equal(24, intent.TargetStandTileY);
        Assert.Equal(2, intent.TargetRouteDistanceTiles);

        request.RouteConnectorCount = 2;
        var rejected = service.Upsert(
            null,
            snapshot,
            request,
            "2026-07-26T06:01:00Z");

        Assert.False(rejected.Accepted);
        Assert.Contains(
            "machine_relocation_route_connector_count_invalid",
            rejected.Errors);

        request.RouteConnectorCount = 1;
        request.TargetRouteDistanceTiles = 1;
        var driftedDistance = service.Upsert(
            null,
            snapshot,
            request,
            "2026-07-26T06:02:00Z");

        Assert.False(driftedDistance.Accepted);
        Assert.Contains(
            "machine_relocation_target_route_projection_invalid",
            driftedDistance.Errors);
    }

    private static CropPlantingCommitmentUpsertRequest Request(SnapshotEnvelope snapshot, int revision, int tileCount) => new()
    {
        StateHash = snapshot.StateHash,
        ExpectedLedgerRevision = revision,
        CommitmentId = "year2-spring-strawberry",
        SourceDecisionId = "strategy.crop.year2.spring.v1",
        SeedId = "745",
        TileCount = tileCount,
        PlantingYear = 2,
        PlantingSeason = "spring",
        PlantingDayOfMonth = 1,
        LocationContext = "outdoor_seasonal"
    };

    private static MaterialReservationUpsertRequest MaterialRequest(
        SnapshotEnvelope snapshot,
        int revision,
        string reservationId,
        int quantity) => new()
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = revision,
            ReservationId = reservationId,
            SourceDecisionId = "strategy.machine.keg.v1",
            GoalId = "goal.machine.keg",
            NodeId = "player:123",
            SlotIndex = 0,
            QualifiedItemId = "(O)388",
            Quantity = quantity,
            Purpose = "reserve wood for keg"
        };

    private static SnapshotEnvelope Snapshot(int totalDays, int year, string season, int day)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($$"""
        {
          "identity": {
            "save_id": {"value":"TestFarm","status":"available"},
            "player_id": {"value":"123","status":"available"}
          },
          "time": {
            "total_days": {"value":{{totalDays}},"status":"available"},
            "year": {"value":{{year}},"status":"available"},
            "season": {"value":"{{season}}","status":"available"},
            "day": {"value":{{day}},"status":"available"}
          },
          "farm": {
            "crop_catalog": {"value":[{
              "seed_id":"745","seasons":["spring"],"grow_days":8,"regrow_days":4,
              "harvest_item_id":"400","harvest_item_qualified_id":"(O)400","harvest_context_tags":["category_fruits","item_strawberry"],"harvest_min_stack":1
            }],"status":"available"},
            "material_inventory_graph": {"value":{
              "schema_version":"material_inventory_graph.v1",
              "status":"available",
              "player_id":123,
              "inventory_nodes":[
                {
                  "node_id":"player:123","inventory_kind":"player_inventory","supply_state":"available",
                  "owner_player_id":123,"ownership_class":"actor_owned","actor_use_authorized":true,
                  "slots":[{"slot_index":0,"item_id":"388","qualified_item_id":"(O)388","stack":30}]
                },
                {
                  "node_id":"global:JunimoChests","inventory_kind":"global_chest_inventory","supply_state":"available",
                  "owner_player_id":0,"ownership_class":"shared_team_global","actor_use_authorized":false,
                  "slots":[{"slot_index":0,"item_id":"388","qualified_item_id":"(O)388","stack":30}]
                }
              ]
            },"status":"available"}
          }
        }
        """)!;
        return new SnapshotEnvelope
        {
            SaveId = new FieldEnvelope<string?> { Value = "TestFarm", Status = FieldStatus.Available },
            PlayerId = new FieldEnvelope<string?> { Value = "123", Status = FieldStatus.Available },
            GameTick = totalDays,
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }

    private static SnapshotEnvelope MachineRelocationSnapshot(
        bool targetPresent)
    {
        var target = targetPresent
            ? """,{"location_id":"Farm","tile_x":7,"tile_y":5,"qualified_item_id":"(BC)13"}"""
            : string.Empty;
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
                """
                {
                  "identity": {
                    "save_id":{"value":"TestFarm","status":"available"},
                    "player_id":{"value":"123","status":"available"}
                  },
                  "player": {
                    "machine_placement":{"value":{
                      "static_projection_fingerprint":"machine-layout:before",
                      "relocation_rows":[{
                        "qualified_item_id":"(BC)13",
                        "locations":[{
                          "location_id":"Farm",
                          "static_legal_tile_ranges":[
                            {"y":5,"start_x":7,"end_x":8}
                          ]
                        }]
                      }]
                    },"status":"available"}
                  },
                  "farm": {
                    "machines":{"value":[
                      {
                        "location_id":"Farm",
                        "tile_x":15,
                        "tile_y":5,
                        "qualified_item_id":"(BC)13",
                        "removal_safe_now":true
                      }
                      TARGET
                    ],"status":"available"}
                  }
                }
                """
                .Replace("TARGET", target))!;
        return new SnapshotEnvelope
        {
            SaveId = new FieldEnvelope<string?>
            {
                Value = "TestFarm",
                Status = FieldStatus.Available
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "123",
                Status = FieldStatus.Available
            },
            GameTick = 1,
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }

    private static SnapshotEnvelope MachineSupportSnapshot(
        bool processing)
    {
        var machine = processing
            ? """
              {
                "location_id":"Farm",
                "tile_x":7,
                "tile_y":5,
                "qualified_item_id":"(BC)12",
                "minutes_until_ready":100,
                "ready_for_harvest":false
              }
              """
            : string.Empty;
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
                $$"""
                {
                  "identity": {
                    "save_id":{"value":"TestFarm","status":"available"},
                    "player_id":{"value":"123","status":"available"}
                  },
                  "player": {
                    "location_id":{
                      "value":"Farm",
                      "status":"available"
                    }
                  },
                  "farm": {
                    "machines":{
                      "value":[{{machine}}],
                      "status":"available"
                    }
                  }
                }
                """)!;
        return new SnapshotEnvelope
        {
            SaveId = new FieldEnvelope<string?>
            {
                Value = "TestFarm",
                Status = FieldStatus.Available
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "123",
                Status = FieldStatus.Available
            },
            GameTick = processing ? 2 : 1,
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }

    private static SnapshotEnvelope
        CrossLocationMachineRelocationSnapshot()
    {
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
                """
                {
                  "identity": {
                    "save_id":{"value":"TestFarm","status":"available"},
                    "player_id":{"value":"123","status":"available"}
                  },
                  "player": {
                    "machine_placement":{"value":{
                      "static_projection_fingerprint":"machine-layout:cross",
                      "relocation_rows":[{
                        "qualified_item_id":"(BC)13",
                        "locations":[{
                          "location_id":"FarmHouse",
                          "static_legal_tile_ranges":[
                            {"y":23,"start_x":36,"end_x":36}
                          ]
                        }]
                      }],
                      "relocation_route_reachability":{
                        "schema_version":"machine_relocation_route_reachability.v1",
                        "projection_status":"complete_static_native_walkability_for_relocation_scope",
                        "locations":[{
                          "location_id":"FarmHouse",
                          "projection_status":"native_static_walkable_tiles_available",
                          "map_width":80,
                          "map_height":40,
                          "static_walkable_tile_count":4,
                          "static_walkable_tile_ranges":[
                            {"y":23,"start_x":36,"end_x":36},
                            {"y":24,"start_x":34,"end_x":36}
                          ]
                        }]
                      }
                    },"status":"available"}
                  },
                  "farm": {
                    "machines":{"value":[{
                      "location_id":"Farm",
                      "tile_x":15,
                      "tile_y":5,
                      "qualified_item_id":"(BC)13",
                      "removal_safe_now":true
                    }],"status":"available"}
                  },
                  "locations": {
                    "route_graph":{"value":{"edges":[{
                      "kind":"building_door",
                      "from_location":"Farm",
                      "from_x":20,
                      "from_y":5,
                      "target_location":"FarmHouse",
                      "target_x":34,
                      "target_y":24,
                      "resolved":true
                    }]},"status":"available"}
                  }
                }
                """)!;
        return new SnapshotEnvelope
        {
            SaveId = new FieldEnvelope<string?>
            {
                Value = "TestFarm",
                Status = FieldStatus.Available
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "123",
                Status = FieldStatus.Available
            },
            GameTick = 1,
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }
}
