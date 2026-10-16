using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueCropProductionObligationPlan()
    {
        var root = TempArtifactRoot("money-crop-production-obligation-plan");
        var snapshot = MoneyCropProductionSnapshot();
        var mutation = new CropCommitmentLedgerService().Upsert(
            null,
            snapshot,
            new CropPlantingCommitmentUpsertRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = 0,
                CommitmentId = "money.year2.spring.strawberry",
                SourceDecisionId = "teacher.money.crop.year2.spring.v1",
                SeedId = "745",
                TileCount = 100,
                PlantingYear = 2,
                PlantingSeason = "spring",
                PlantingDayOfMonth = 1,
                LocationContext = "outdoor_seasonal"
            },
            "2026-10-16T00:00:00Z");
        Require(mutation.Accepted && mutation.Ledger is not null,
            "Money crop-production fixture commitment was rejected.");
        var ledger = mutation.Ledger!;

        var snapshotPath = Path.Combine(root, "snapshot.json");
        var ledgerPath = Path.Combine(root, "ledger.json");
        var requestPath = Path.Combine(root, "request.json");
        Write(snapshotPath, snapshot);
        Write(ledgerPath, ledger);
        Write(requestPath, new MoneyRevenueCropProductionObligationPlanRequest
        {
            SnapshotPath = snapshotPath,
            StrategyLedgerPath = ledgerPath
        });

        var plan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            requestPath);
        var planPath = Path.Combine(root, "plan.json");
        Write(planPath, plan);
        var commitment = plan.Commitments.Single();
        Require(plan.Status == "crop_production_evidence_collection_ready" &&
                plan.ActiveCommitmentCount == 1 &&
                plan.MinimumGrossRevenue == 60_000 &&
                !plan.MinimumGrossRevenueReachesTarget &&
                !plan.FutureCropProductionProofReady &&
                !plan.FreshSaveRevenueScheduleReady &&
                !plan.FormalProductTrainingAuthorized &&
                commitment.CurrentUnreservedSeedQuantity == 40 &&
                commitment.SeedQuantityShortfall == 60 &&
                commitment.RequiredWateringTotalDays.Length == 24 &&
                commitment.HarvestWaves.Select(value =>
                    value.HarvestTotalDay).SequenceEqual(
                        new[] { 120, 124, 128, 132, 136 }) &&
                commitment.MinimumTotalUnits == 500 &&
                commitment.PendingEvidenceRequestIds.Length == 10 &&
                commitment.PendingEvidenceRequestIds.Contains(
                    "planting-layout:money.year2.spring.strawberry",
                    StringComparer.Ordinal) &&
                commitment.PendingEvidenceRequestIds.Contains(
                    "seed-acquisition:money.year2.spring.strawberry:60",
                    StringComparer.Ordinal),
            "Money crop-production obligation plan lost an exact future obligation.");
        VerifyMoneyCropSitePreparationPlan(root, planPath);
        VerifyMoneyCropPlantingLayoutCheckpoint(root, planPath, plan);
        VerifyMoneyCropSeedReservationProof(root, snapshot);

        ledger.CropPlantingCommitments[0].HarvestTotalDays =
            new[] { 120 };
        var tamperedLedgerPath = Path.Combine(root, "ledger-tampered.json");
        var tamperedRequestPath = Path.Combine(root, "request-tampered.json");
        Write(tamperedLedgerPath, ledger);
        Write(tamperedRequestPath,
            new MoneyRevenueCropProductionObligationPlanRequest
            {
                SnapshotPath = snapshotPath,
                StrategyLedgerPath = tamperedLedgerPath
            });
        var rejected = false;
        try
        {
            _ = MoneyRevenueCropProductionObligationPlanBuilder.Build(
                tamperedRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A tampered Money crop-production harvest schedule was admitted.");

        ledger.CropPlantingCommitments[0].HarvestTotalDays = null!;
        Write(tamperedLedgerPath, ledger);
        rejected = false;
        try
        {
            _ = MoneyRevenueCropProductionObligationPlanBuilder.Build(
                tamperedRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A null Money crop-production harvest schedule was admitted.");
    }

    private static void VerifyMoneyCropSeedReservationProof(
        string root,
        SnapshotEnvelope snapshot)
    {
        var mutation = new CropCommitmentLedgerService().Upsert(
            null,
            snapshot,
            new CropPlantingCommitmentUpsertRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = 0,
                CommitmentId = "money.seed-reservation.fixture",
                SourceDecisionId = "teacher.money.seed-reservation.v1",
                SeedId = "745",
                TileCount = 40,
                PlantingYear = 2,
                PlantingSeason = "spring",
                PlantingDayOfMonth = 1,
                LocationContext = "outdoor_seasonal"
            },
            "2026-10-16T00:00:00Z");
        Require(mutation.Accepted && mutation.Ledger is not null,
            "Money seed-reservation fixture commitment was rejected.");
        var sourceLedger = mutation.Ledger!;
        var snapshotPath = Path.Combine(root, "seed-reservation-snapshot.json");
        var sourceLedgerPath = Path.Combine(root, "seed-reservation-source-ledger.json");
        var planRequestPath = Path.Combine(root, "seed-reservation-plan-request.json");
        Write(snapshotPath, snapshot);
        Write(sourceLedgerPath, sourceLedger);
        Write(planRequestPath, new MoneyRevenueCropProductionObligationPlanRequest
        {
            SnapshotPath = snapshotPath,
            StrategyLedgerPath = sourceLedgerPath
        });
        var plan = MoneyRevenueCropProductionObligationPlanBuilder.Build(planRequestPath);
        var planPath = Path.Combine(root, "seed-reservation-plan.json");
        Write(planPath, plan);

        sourceLedger.Revision++;
        sourceLedger.MaterialReservations = new[]
        {
            new MaterialReservation
            {
                ReservationId = "money.seed-reservation.fixture:seed:0",
                Revision = 1,
                SourceDecisionId = "teacher.money.seed-reservation.v1",
                SourceStateHash = snapshot.StateHash,
                GoalId = "earn_money",
                OwnerPlayerId = 123,
                NodeId = "player:123",
                SlotIndex = 0,
                QualifiedItemId = "(O)745",
                Quantity = 40,
                Purpose = "crop_seed_supply:money.seed-reservation.fixture"
            }
        };
        var committedLedgerPath = Path.Combine(root, "seed-reservation-committed-ledger.json");
        Write(committedLedgerPath, sourceLedger);
        var proofRequestPath = Path.Combine(root, "seed-reservation-proof-request.json");
        Write(proofRequestPath, new MoneyRevenueCropSeedReservationProofRequest
        {
            CropProductionPlanPath = planPath,
            CommitmentId = "money.seed-reservation.fixture",
            ReservationSnapshotPath = snapshotPath,
            CommittedStrategyLedgerPath = committedLedgerPath
        });
        var proof = MoneyRevenueCropSeedReservationProofBuilder.Build(proofRequestPath);
        Require(proof.SeedReservationVerified && proof.RequiredQuantity == 40 &&
                proof.ReservedQuantity == 40 && proof.ReservationIds.Length == 1 &&
                !proof.FormalProductTrainingAuthorized,
            "Money crop seed-reservation proof lost its exact committed quantity.");

        var proofPath = Path.Combine(root, "seed-reservation-proof.json");
        var indexPath = Path.Combine(root, "seed-reservation-fragment-index.json");
        var checkpointRequestPath = Path.Combine(root, "seed-reservation-checkpoint-request.json");
        Write(proofPath, proof);
        Write(indexPath, new MoneyRevenueCropProductionProofFragmentIndex
        {
            CropProductionPlanPath = planPath,
            SeedReservationProofPaths = new[] { proofPath }
        });
        Write(checkpointRequestPath, new MoneyRevenueCropProductionProofCheckpointRequest
        {
            CropProductionPlanPath = planPath,
            FragmentIndexPath = indexPath
        });
        var checkpoint = MoneyRevenueCropProductionProofCheckpointBuilder.Build(checkpointRequestPath);
        Require(checkpoint.VerifiedFragmentCount == 1 &&
                checkpoint.VerifiedSeedReservations.Length == 1 &&
                checkpoint.VerifiedRequestIds.SequenceEqual(
                    new[] { "seed-reservation:money.seed-reservation.fixture" },
                    StringComparer.Ordinal),
            "Money crop seed-reservation proof did not enter the exact checkpoint denominator.");

        sourceLedger.MaterialReservations[0].Quantity = 39;
        var tamperedLedgerPath = Path.Combine(root, "seed-reservation-ledger-tampered.json");
        Write(tamperedLedgerPath, sourceLedger);
        var tamperedRequestPath = Path.Combine(root, "seed-reservation-request-tampered.json");
        Write(tamperedRequestPath, new MoneyRevenueCropSeedReservationProofRequest
        {
            CropProductionPlanPath = planPath,
            CommitmentId = "money.seed-reservation.fixture",
            ReservationSnapshotPath = snapshotPath,
            CommittedStrategyLedgerPath = tamperedLedgerPath
        });
        var rejected = false;
        try
        {
            _ = MoneyRevenueCropSeedReservationProofBuilder.Build(tamperedRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A partial Money crop seed reservation was admitted as full proof.");
    }

    private static void VerifyMoneyCropSitePreparationPlan(
        string root,
        string planPath)
    {
        var snapshotPath = Path.Combine(
            root,
            "site-preparation-snapshot.json");
        var requestPath = Path.Combine(
            root,
            "site-preparation-request.json");
        var snapshot = MoneyCropSitePreparationSnapshot();
        var tiles = Enumerable.Range(0, 100)
            .Select(index => new MoneyRevenueCropPlantingTile(
                5 + index % 10,
                6 + index / 10))
            .ToArray();
        Write(snapshotPath, snapshot);
        Write(requestPath, new MoneyRevenueCropSitePreparationPlanRequest
        {
            CropProductionPlanPath = planPath,
            CommitmentId = "money.year2.spring.strawberry",
            PreparationSnapshotPath = snapshotPath,
            LocationId = "Farm",
            Tiles = tiles
        });

        var plan = MoneyRevenueCropSitePreparationPlanBuilder.Build(
            requestPath);

        Require(plan.Status == "site_preparation_plan_ready" &&
                plan.TileCount == 100 &&
                plan.AlreadyPreparedCount == 0 &&
                plan.DirectTillCount == 100 &&
                plan.ClearThenRebindCount == 0 &&
                plan.BlockedCount == 0 &&
                plan.SitePreparationPlanReady &&
                plan.PlantingLayoutProofStillRequired &&
                !plan.FormalProductTrainingAuthorized &&
                plan.Steps.All(step =>
                    step.ActionOptionId == "executor.till_soil" &&
                    step.Parameters.Any(parameter =>
                        parameter.Name == "fresh_snapshot_rebind_required" &&
                        parameter.Value == "true")),
            "Money crop site-preparation plan lost its bounded till bindings.");
    }

    private static void VerifyMoneyCropPlantingLayoutCheckpoint(
        string root,
        string planPath,
        MoneyRevenueCropProductionObligationPlan plan)
    {
        var plantingSnapshotPath = Path.Combine(
            root,
            "planting-day-snapshot.json");
        var plantingSnapshot = MoneyCropPlantingDaySnapshot();
        Write(plantingSnapshotPath, plantingSnapshot);
        var tiles = Enumerable.Range(0, 100)
            .Select(index => new MoneyRevenueCropPlantingTile(
                5 + index % 10,
                6 + index / 10))
            .ToArray();
        var layoutRequestPath = Path.Combine(root, "layout-request.json");
        var layoutRequest = new MoneyRevenueCropPlantingLayoutProofRequest
        {
            CropProductionPlanPath = planPath,
            CommitmentId = "money.year2.spring.strawberry",
            PlantingSnapshotPath = plantingSnapshotPath,
            LocationId = "Farm",
            Tiles = tiles
        };
        Write(layoutRequestPath, layoutRequest);
        var proof = MoneyRevenueCropPlantingLayoutProofBuilder.Build(
            layoutRequestPath);
        Require(proof.Status == "verified_planting_layout" &&
                proof.EvidenceRequestId ==
                    "planting-layout:money.year2.spring.strawberry" &&
                proof.PlantingTotalDay == 112 &&
                proof.TileCount == 100 &&
                proof.PlantingLayoutVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money crop planting-layout proof lost exact planting-day evidence.");

        var proofPath = Path.Combine(root, "layout-proof.json");
        var fragmentIndexPath = Path.Combine(root, "fragment-index.json");
        var checkpointRequestPath = Path.Combine(
            root,
            "checkpoint-request.json");
        Write(proofPath, proof);
        Write(fragmentIndexPath,
            new MoneyRevenueCropProductionProofFragmentIndex
            {
                CropProductionPlanPath = planPath,
                PlantingLayoutProofPaths = new[] { proofPath }
            });
        Write(checkpointRequestPath,
            new MoneyRevenueCropProductionProofCheckpointRequest
            {
                CropProductionPlanPath = planPath,
                FragmentIndexPath = fragmentIndexPath
            });
        var checkpoint = MoneyRevenueCropProductionProofCheckpointBuilder.Build(
            checkpointRequestPath);
        Require(checkpoint.Status ==
                    "crop_production_proof_collection_in_progress" &&
                checkpoint.RequestCount ==
                    plan.PendingEvidenceRequestIds.Length &&
                checkpoint.VerifiedFragmentCount == 1 &&
                checkpoint.PendingRequestCount == 9 &&
                checkpoint.VerifiedRequestIds.SequenceEqual(
                    new[]
                    {
                        "planting-layout:money.year2.spring.strawberry"
                    },
                    StringComparer.Ordinal) &&
                checkpoint.VerifiedPlantingLayouts.Length == 1 &&
                !checkpoint.FutureCropProductionProofReady &&
                !checkpoint.FreshSaveRevenueScheduleReady &&
                !checkpoint.FormalProductTrainingAuthorized,
            "Money crop-production proof checkpoint admitted incomplete future production.");

        layoutRequest.Tiles[0] = new MoneyRevenueCropPlantingTile(999, 999);
        var invalidLayoutRequestPath = Path.Combine(
            root,
            "layout-request-invalid.json");
        Write(invalidLayoutRequestPath, layoutRequest);
        var rejected = false;
        try
        {
            _ = MoneyRevenueCropPlantingLayoutProofBuilder.Build(
                invalidLayoutRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "An unavailable Money crop planting-layout tile was admitted.");

        proof.PlantingLayoutVerified = false;
        var tamperedProofPath = Path.Combine(root, "layout-proof-tampered.json");
        var tamperedFragmentIndexPath = Path.Combine(
            root,
            "fragment-index-tampered.json");
        var tamperedCheckpointRequestPath = Path.Combine(
            root,
            "checkpoint-request-tampered.json");
        Write(tamperedProofPath, proof);
        Write(tamperedFragmentIndexPath,
            new MoneyRevenueCropProductionProofFragmentIndex
            {
                CropProductionPlanPath = planPath,
                PlantingLayoutProofPaths = new[] { tamperedProofPath }
            });
        Write(tamperedCheckpointRequestPath,
            new MoneyRevenueCropProductionProofCheckpointRequest
            {
                CropProductionPlanPath = planPath,
                FragmentIndexPath = tamperedFragmentIndexPath
            });
        rejected = false;
        try
        {
            _ = MoneyRevenueCropProductionProofCheckpointBuilder.Build(
                tamperedCheckpointRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A tampered Money crop planting-layout proof was admitted.");
    }

    private static SnapshotEnvelope MoneyCropPlantingDaySnapshot()
    {
        var snapshot = MoneyCropProductionSnapshot();
        var state = snapshot.State.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        state["time"] = JsonSerializer.SerializeToElement(new
        {
            total_days = MoneyCropField(112),
            year = MoneyCropField(2),
            season = MoneyCropField("spring"),
            day = MoneyCropField(1)
        }, JsonDefaults.Options);
        state["player"] = JsonSerializer.SerializeToElement(new
        {
            location_id = MoneyCropField("Farm")
        }, JsonDefaults.Options);
        var hoeDirtTiles = Enumerable.Range(0, 100).Select(index => new
        {
            tile_x = 5 + index % 10,
            tile_y = 6 + index / 10,
            has_crop = false,
            is_garden_pot = false,
            occupied_object_qualified_id = (string?)null,
            seed_results = new[]
            {
                new
                {
                    slot_index = 0,
                    seed_id = "745",
                    hard_rule_allows_planting = true,
                    can_mature_before_season_end_with_paddy_if_eligible = true,
                    adjusted_grow_days_with_paddy_if_eligible = 7,
                    days_remaining_in_season = 27
                }
            }
        }).ToArray();
        state["current_location"] = JsonSerializer.SerializeToElement(new
        {
            planting_context = MoneyCropField(new
            {
                location_id = "Farm",
                season = "spring",
                is_outdoors = true,
                is_farm = true,
                is_greenhouse = false,
                hoe_dirt_tiles = hoeDirtTiles
            })
        }, JsonDefaults.Options);
        snapshot.GameTick = 200;
        snapshot.State = state;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static SnapshotEnvelope MoneyCropSitePreparationSnapshot()
    {
        var snapshot = MoneyCropProductionSnapshot();
        var state = snapshot.State.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        state["time"] = JsonSerializer.SerializeToElement(new
        {
            total_days = MoneyCropRuntimeField(111),
            year = MoneyCropRuntimeField(1),
            season = MoneyCropRuntimeField("winter"),
            day = MoneyCropRuntimeField(28),
            time = MoneyCropRuntimeField(900)
        }, JsonDefaults.Options);
        state["player"] = JsonSerializer.SerializeToElement(new
        {
            location_id = MoneyCropRuntimeField("Farm"),
            tile_x = MoneyCropRuntimeField(4),
            tile_y = MoneyCropRuntimeField(6),
            energy = MoneyCropRuntimeField(270),
            inventory = MoneyCropRuntimeField(Array.Empty<object>())
        }, JsonDefaults.Options);
        var siteTiles = Enumerable.Range(0, 100).Select(index => new
        {
            tile_x = 5 + index % 10,
            tile_y = 6 + index / 10,
            status = "ready_to_till",
            clearable_blocker_kinds = Array.Empty<string>()
        }).ToArray();
        state["current_location"] = JsonSerializer.SerializeToElement(new
        {
            map = MoneyCropRuntimeField(new { id = "Farm" }),
            objects = MoneyCropRuntimeField(Array.Empty<object>()),
            terrain_features = MoneyCropRuntimeField(Array.Empty<object>()),
            site_preparation_context = MoneyCropRuntimeField(new
            {
                projection_status =
                    "complete_bounded_current_farm_region",
                location_id = "Farm",
                hoe_tool_slot_index = 2,
                hoe_tool_qualified_item_id = "(T)Hoe",
                hoe_energy_cost_per_single_tile_action = 1.7,
                tiles = siteTiles
            })
        }, JsonDefaults.Options);
        state["farm"] = JsonSerializer.SerializeToElement(new
        {
            crop_catalog = MoneyCropRuntimeField(new[]
            {
                new
                {
                    seed_id = "745",
                    seasons = new[] { "spring" },
                    grow_days = 8,
                    regrow_days = 4,
                    harvest_item_id = "400",
                    harvest_item_qualified_id = "(O)400",
                    harvest_context_tags = new[]
                    {
                        "category_fruits",
                        "item_strawberry"
                    },
                    harvest_unit_sale_price = 120,
                    harvest_min_stack = 1,
                    needs_watering = true
                }
            }),
            material_inventory_graph = snapshot.State["farm"]
                .GetProperty("material_inventory_graph"),
            resource_clumps = MoneyCropRuntimeField(Array.Empty<object>())
        }, JsonDefaults.Options);
        state["menus"] = JsonSerializer.SerializeToElement(new
        {
            active_menu = MoneyCropRuntimeField(new
            {
                is_open = false,
                type = "none"
            })
        }, JsonDefaults.Options);
        state["locations"] = JsonSerializer.SerializeToElement(new
        {
            collision_grid = MoneyCropRuntimeField(new
            {
                location_id = "Farm",
                width = 100,
                height = 100,
                notable_tiles = Array.Empty<object>()
            }),
            route_action_branch_coverage = MoneyCropRuntimeField(new
            {
                rows = Array.Empty<object>()
            })
        }, JsonDefaults.Options);
        snapshot.GameTick = 150;
        snapshot.State = state;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static object MoneyCropField<T>(T value) => new
    {
        value,
        status = FieldStatus.Available
    };

    private static object MoneyCropRuntimeField<T>(T value) => new
    {
        value,
        status = FieldStatus.Available,
        source = new
        {
            kind = "test",
            path = "money_crop_site_preparation_self_test"
        },
        adapter = "test",
        read_at_tick = 150,
        confidence = 1.0
    };

    private static SnapshotEnvelope MoneyCropProductionSnapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """
            {
              "identity": {
                "save_id": {"value":"MoneyCropFarm","status":"available"},
                "player_id": {"value":"123","status":"available"}
              },
              "time": {
                "total_days": {"value":103,"status":"available"},
                "year": {"value":1,"status":"available"},
                "season": {"value":"winter","status":"available"},
                "day": {"value":20,"status":"available"}
              },
              "farm": {
                "crop_catalog": {"value":[{
                  "seed_id":"745",
                  "seasons":["spring"],
                  "grow_days":8,
                  "regrow_days":4,
                  "harvest_item_id":"400",
                  "harvest_item_qualified_id":"(O)400",
                  "harvest_context_tags":["category_fruits","item_strawberry"],
                  "harvest_unit_sale_price":120,
                  "harvest_min_stack":1,
                  "needs_watering":true
                }],"status":"available"},
                "material_inventory_graph": {"value":{
                  "schema_version":"material_inventory_graph.v1",
                  "status":"available",
                  "player_id":123,
                  "inventory_nodes":[{
                    "node_id":"player:123",
                    "inventory_kind":"player_inventory",
                    "supply_state":"available",
                    "owner_player_id":123,
                    "ownership_class":"actor_owned",
                    "actor_use_authorized":true,
                    "slots":[{
                      "slot_index":0,
                      "item_id":"745",
                      "qualified_item_id":"(O)745",
                      "context_tags":[],
                      "stack":40
                    }]
                  }],
                  "access_points":[],
                  "workbench_links":[],
                  "quantity_rows":[],
                  "physical_inventory_count":1,
                  "access_point_count":0,
                  "deduplicated_access_point_count":0,
                  "default_shared_resource_policy":"deny_without_explicit_authorization"
                },"status":"available"}
              }
            }
            """,
            JsonDefaults.Options)!;
        var snapshot = new SnapshotEnvelope
        {
            BridgeVersion = "self-test",
            GameVersion = "1.6.15",
            SaveId = new FieldEnvelope<string?>
            {
                Value = "MoneyCropFarm",
                Status = FieldStatus.Available
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "123",
                Status = FieldStatus.Available
            },
            GameTick = 100,
            State = state
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }
}
