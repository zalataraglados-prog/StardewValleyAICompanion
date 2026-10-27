using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueCropProductionTeacherDecision()
    {
        var root = TempArtifactRoot("money-crop-production-teacher-decision");
        var sourcePath = Path.Combine(root, "source-snapshot.json");
        var layoutPath = Path.Combine(root, "farm-layout-snapshot.json");
        var teacherRequestPath = Path.Combine(root, "teacher-request.json");
        var decisionPath = Path.Combine(root, "decision.json");
        Write(sourcePath, MoneyCropTeacherSourceSnapshot());
        Write(layoutPath, MoneyCropTeacherLayoutSnapshot());
        Write(teacherRequestPath,
            new MoneyRevenueCropProductionTeacherRequest
            {
                SnapshotPath = sourcePath,
                FarmLayoutSnapshotPaths = new[] { layoutPath },
                DecisionId = "teacher.money.crop.spring7.v1",
                DecisionRecordedAt = "2026-10-18T00:00:00Z",
                MaximumTileCount = 8,
                MaxQueueItemCount = 8
            });

        var purchaseAvailability =
            new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    MoneyCropTeacherSourceSnapshot(),
                    new[] { "economy.buy_supplies" })
                .Options[0];
        Write(
            Path.Combine(root, "purchase-availability.json"),
            purchaseAvailability);

        var decision =
            MoneyRevenueCropProductionTeacherDecisionBuilder.Build(
                teacherRequestPath);
        Write(decisionPath, decision);
        var receipt = MoneyRevenueCropProductionDecisionReceiptBuilder.Build(
            decisionPath);
        Require(decision.SchemaVersion ==
                    "money_revenue_crop_production_decision_request.v2" &&
                decision.TeacherSelectionPolicy ==
                    MoneyRevenueCropProductionTeacherDecisionBuilder
                        .SelectionPolicy &&
                decision.TeacherCandidateEvaluations.Count(value =>
                    value.Admitted) == 3 &&
                decision.TeacherCandidateEvaluations.Count(value =>
                    value.Selected) == 1 &&
                decision.TeacherCandidateEvaluations.Any(value =>
                    value.SeedId == "476" &&
                    value.Admitted &&
                    value.ExistingSeedQuantity == 1 &&
                    value.ShopId.Length == 0) &&
                decision.TeacherCandidateEvaluations.Any(value =>
                    value.SeedId == "478" &&
                    !value.Admitted &&
                    value.BlockReasons.Contains(
                        "no_current_seed_supply",
                        StringComparer.Ordinal)) &&
                decision.TeacherCandidateEvaluations.Any(value =>
                    value.SeedId == "745" &&
                    !value.Admitted &&
                    value.BlockReasons.Contains(
                        "no_current_seed_supply",
                        StringComparer.Ordinal)) &&
                decision.TeacherCandidateEvaluations.Any(value =>
                    value.SeedId == "474" &&
                    value.Selected &&
                    value.ShopId == "SeedShop" &&
                    value.SeedUnitPrice == 80 &&
                    value.RequiredWateringActionCount == 60 &&
                    value.AvailableWateringActionsWithoutRefill == 40 &&
                    value.WateringRefillRouteVerified &&
                    !value.WateringCapacityLimited) &&
                decision.Commitments.Length == 1 &&
                decision.Commitments[0].SeedId == "474" &&
                decision.Commitments[0].TileCount == 5 &&
                decision.Commitments[0].Tiles.Length == 5 &&
                decision.Commitments[0].Tiles.Distinct().Count() == 5 &&
                receipt.SchemaVersion ==
                    "money_revenue_crop_production_decision_receipt.v2" &&
                receipt.AllCommitmentsAccepted &&
                receipt.TeacherRequestSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(
                        teacherRequestPath),
            "Money crop Teacher did not select and bind the conservative cauliflower plan.");

        var lowWaterSourcePath = Path.Combine(
            root,
            "low-water-source-snapshot.json");
        var lowWaterLayoutPath = Path.Combine(
            root,
            "low-water-farm-layout-snapshot.json");
        Write(lowWaterSourcePath,
            MoneyCropTeacherSourceSnapshot(3, "472", 14));
        Write(lowWaterLayoutPath,
            MoneyCropTeacherLayoutSnapshot(3, true, "472", 14));
        var lowWaterRequestPath = Path.Combine(
            root,
            "low-water-teacher-request.json");
        Write(lowWaterRequestPath,
            new MoneyRevenueCropProductionTeacherRequest
            {
                SnapshotPath = lowWaterSourcePath,
                FarmLayoutSnapshotPaths = new[] { lowWaterLayoutPath },
                DecisionId = "teacher.money.crop.spring7.low-water-refill",
                DecisionRecordedAt = "2026-10-18T00:00:00Z",
                MaximumTileCount = 8,
                MaxQueueItemCount = 8
            });
        var lowWaterDecision =
            MoneyRevenueCropProductionTeacherDecisionBuilder.Build(
                lowWaterRequestPath);
        Require(lowWaterDecision.TeacherCandidateEvaluations.Any(value =>
                    value.SeedId == "472" &&
                    value.ExistingSeedQuantity == 14 &&
                    value.Admitted &&
                    value.AvailableWateringActionsWithoutRefill == 3 &&
                    value.WateringRefillRouteVerified &&
                    value.TileCount == 8),
            "A reachable native refill source did not admit a low-water crop plan.");

        var clearanceLayoutPath = Path.Combine(
            root,
            "low-water-clearance-refill-layout-snapshot.json");
        Write(clearanceLayoutPath,
            MoneyCropTeacherLayoutSnapshot(
                3,
                includeRefillSource: true,
                existingSeedId: "472",
                existingSeedQuantity: 14,
                requireRefillClearance: true));
        var clearanceRequestPath = Path.Combine(
            root,
            "low-water-clearance-refill-request.json");
        Write(clearanceRequestPath,
            new MoneyRevenueCropProductionTeacherRequest
            {
                SnapshotPath = lowWaterSourcePath,
                FarmLayoutSnapshotPaths = new[] { clearanceLayoutPath },
                DecisionId =
                    "teacher.money.crop.spring7.low-water-clearance-refill",
                DecisionRecordedAt = "2026-10-18T00:00:00Z",
                MaximumTileCount = 8,
                MaxQueueItemCount = 8
            });
        var clearanceDecision =
            MoneyRevenueCropProductionTeacherDecisionBuilder.Build(
                clearanceRequestPath);
        var clearanceCommitment = clearanceDecision.Commitments.Single();
        Require(clearanceDecision.TeacherCandidateEvaluations.Any(value =>
                    value.SeedId == "472" &&
                    value.Admitted &&
                    value.WateringRefillRouteVerified &&
                    value.TileCount == 7) &&
                clearanceCommitment.WateringRefillPreparationTiles
                    .SequenceEqual(new[]
                    {
                        new MoneyRevenueCropPlantingTile(2, 2)
                    }) &&
                clearanceCommitment.WateringRefillApproachTile ==
                    new MoneyRevenueCropPlantingTile(1, 0) &&
                !clearanceCommitment.Tiles.Contains(
                    clearanceCommitment.WateringRefillPreparationTiles[0]),
            "A bounded one-clearance native refill corridor and its terminal approach were not bound separately from crop tiles.");

        var noRefillLayoutPath = Path.Combine(
            root,
            "low-water-no-refill-layout-snapshot.json");
        Write(noRefillLayoutPath,
            MoneyCropTeacherLayoutSnapshot(
                3,
                includeRefillSource: false,
                existingSeedId: "472",
                existingSeedQuantity: 14));
        var noRefillRequestPath = Path.Combine(
            root,
            "low-water-no-refill-request.json");
        Write(noRefillRequestPath,
            new MoneyRevenueCropProductionTeacherRequest
            {
                SnapshotPath = lowWaterSourcePath,
                FarmLayoutSnapshotPaths = new[] { noRefillLayoutPath },
                DecisionId = "teacher.money.crop.spring7.low-water-no-refill",
                DecisionRecordedAt = "2026-10-18T00:00:00Z",
                MaximumTileCount = 8,
                MaxQueueItemCount = 8
            });
        var rejected = false;
        try
        {
            _ = MoneyRevenueCropProductionTeacherDecisionBuilder.Build(
                noRefillRequestPath);
        }
        catch (InvalidDataException ex)
        {
            rejected = ex.Message.Contains(
                "watering_can_supply_without_verified_refill_insufficient",
                StringComparison.Ordinal);
        }
        Require(rejected,
            "A low-water crop plan without native refill evidence was admitted.");

        var tampered = JsonSerializer.Deserialize<
            MoneyRevenueCropProductionDecisionRequest>(
                JsonSerializer.Serialize(decision, JsonDefaults.Options),
                JsonDefaults.Options)!;
        tampered.Commitments[0].Tiles[0] =
            new MoneyRevenueCropPlantingTile(99, 99);
        var tamperedPath = Path.Combine(root, "tampered-decision.json");
        Write(tamperedPath, tampered);
        rejected = false;
        try
        {
            _ = MoneyRevenueCropProductionDecisionReceiptBuilder.Build(
                tamperedPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A crop-production decision that drifted from its Teacher was admitted.");

        var driftedLayout = MoneyCropTeacherLayoutSnapshot();
        var driftedState = driftedLayout.State.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        var player = JsonNode.Parse(driftedState["player"].GetRawText())!
            .AsObject();
        player["money"] = JsonSerializer.SerializeToNode(
            MoneyCropRuntimeField(449),
            JsonDefaults.Options);
        driftedState["player"] = JsonSerializer.SerializeToElement(
            player,
            JsonDefaults.Options);
        driftedLayout.State = driftedState;
        driftedLayout.StateHash = SnapshotHash.ComputeStateHash(
            driftedLayout.State);
        var driftedLayoutPath = Path.Combine(root, "drifted-layout.json");
        Write(driftedLayoutPath, driftedLayout);
        var driftedRequestPath = Path.Combine(root, "drifted-request.json");
        Write(driftedRequestPath,
            new MoneyRevenueCropProductionTeacherRequest
            {
                SnapshotPath = sourcePath,
                FarmLayoutSnapshotPaths = new[] { driftedLayoutPath },
                DecisionId = "teacher.money.crop.spring7.drifted",
                DecisionRecordedAt = "2026-10-18T00:00:00Z"
            });
        rejected = false;
        try
        {
            _ = MoneyRevenueCropProductionTeacherDecisionBuilder.Build(
                driftedRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A crop-production layout captured from a different resource boundary was admitted.");

        var partialLayout = MoneyCropTeacherLayoutSnapshot();
        var partialState = partialLayout.State.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        partialState["farm"] = JsonSerializer.SerializeToElement(new
        {
            site_preparation_context = MoneyCropRuntimeField(new
            {
                schema_version = "farm_site_preparation_context.v2",
                projection_status =
                    "complete_bounded_current_farm_region",
                location_id = "Farm",
                map_width = 4,
                map_height = 3,
                bounds = new
                {
                    min_x = 0,
                    min_y = 0,
                    max_x = 2,
                    max_y = 2
                },
                tile_count = 9,
                tiles = Enumerable.Range(0, 9).Select(index => new
                {
                    tile_x = index % 3,
                    tile_y = index / 3,
                    status = "ready_to_till",
                    map_passable = true,
                    native_blocked_without_characters = false,
                    watering_can_refill_source = false,
                    watering_refill_approach_standable = true
                }).ToArray()
            })
        }, JsonDefaults.Options);
        partialLayout.State = partialState;
        partialLayout.StateHash = SnapshotHash.ComputeStateHash(partialState);
        var partialLayoutPath = Path.Combine(root, "partial-layout.json");
        Write(partialLayoutPath, partialLayout);
        var partialRequestPath = Path.Combine(root, "partial-request.json");
        Write(partialRequestPath,
            new MoneyRevenueCropProductionTeacherRequest
            {
                SnapshotPath = sourcePath,
                FarmLayoutSnapshotPaths = new[] { partialLayoutPath },
                DecisionId = "teacher.money.crop.spring7.partial",
                DecisionRecordedAt = "2026-10-18T00:00:00Z"
            });
        rejected = false;
        try
        {
            _ = MoneyRevenueCropProductionTeacherDecisionBuilder.Build(
                partialRequestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A crop-production Teacher layout with incomplete Farm coverage was admitted.");
    }

    private static SnapshotEnvelope MoneyCropTeacherSourceSnapshot(
        int wateringCanWaterLeft = 40,
        string existingSeedId = "476",
        int existingSeedQuantity = 1)
    {
        var snapshot = MoneyCropProductionSnapshot();
        var state = snapshot.State.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        state["time"] = JsonSerializer.SerializeToElement(new
        {
            total_days = MoneyCropRuntimeField(6),
            year = MoneyCropRuntimeField(1),
            season = MoneyCropRuntimeField("spring"),
            day = MoneyCropRuntimeField(7),
            time = MoneyCropRuntimeField(600)
        }, JsonDefaults.Options);
        state["player"] = JsonSerializer.SerializeToElement(new
        {
            location_id = MoneyCropRuntimeField("FarmHouse"),
            tile_x = MoneyCropRuntimeField(0),
            tile_y = MoneyCropRuntimeField(0),
            energy = MoneyCropRuntimeField(270),
            money = MoneyCropRuntimeField(450),
            watering_can_state = MoneyCropRuntimeField(new
            {
                projection_status = "exact_vanilla_power_zero",
                slot_index = 1,
                runtime_type = "StardewValley.Tools.WateringCan",
                upgrade_level = 0,
                water_left = wateringCanWaterLeft,
                capacity = 40,
                is_bottomless = false,
                farmer_has_watering_can_enchantment = false,
                is_efficient = false,
                farming_level = 0,
                energy_cost_per_power_zero_action = 2d
            }),
            inventory = MoneyCropRuntimeField(new[]
            {
                new
                {
                    slot_index = 0,
                    qualified_item_id = "(O)" + existingSeedId,
                    stack = existingSeedQuantity
                }
            }),
            seed_inventory = MoneyCropRuntimeField(new[]
            {
                new
                {
                    slot_index = 0,
                    qualified_item_id = "(O)" + existingSeedId,
                    stack = existingSeedQuantity
                }
            })
        }, JsonDefaults.Options);
        state["farm"] = JsonSerializer.SerializeToElement(new
        {
            crop_catalog = MoneyCropRuntimeField(new object[]
            {
                MoneyCropTeacherCatalogRow(
                    "472", 4, null, "24", 70, false),
                MoneyCropTeacherCatalogRow(
                    "473", 10, 3, "188", 80, true),
                MoneyCropTeacherCatalogRow(
                    "474", 12, null, "190", 350, false),
                MoneyCropTeacherCatalogRow(
                    "476", 6, null, "192", 30, false),
                MoneyCropTeacherCatalogRow(
                    "478", 13, null, "276", 220, false),
                MoneyCropTeacherCatalogRow(
                    "745", 8, 4, "400", 120, false)
            }),
            shipping_bins = MoneyCropRuntimeField(new[]
            {
                new
                {
                    tile_x = 71,
                    tile_y = 14,
                    completed = true
                }
            }),
            material_inventory_graph = MoneyCropRuntimeField(new
            {
                schema_version = "material_inventory_graph.v1",
                status = "available",
                player_id = 123,
                inventory_nodes = new object[]
                {
                    new
                    {
                        node_id = "player:inventory",
                        supply_state = "available",
                        actor_use_authorized = true,
                        slots = new[]
                        {
                            new
                            {
                                slot_index = 0,
                                qualified_item_id = "(O)" + existingSeedId,
                                stack = existingSeedQuantity
                            }
                        }
                    }
                },
                access_points = Array.Empty<object>(),
                workbench_links = Array.Empty<object>(),
                quantity_rows = Array.Empty<object>(),
                physical_inventory_count = 1,
                access_point_count = 0,
                deduplicated_access_point_count = 0,
                default_shared_resource_policy =
                    "deny_without_explicit_authorization"
            })
        }, JsonDefaults.Options);
        state["locations"] = JsonSerializer.SerializeToElement(new
        {
            shops = MoneyCropRuntimeField(new
            {
                stores_closed_for_festival = false,
                shops = new object[]
                {
                    new
                    {
                        shop_id = "SeedShop",
                        stock_preview = new
                        {
                            entries = new[]
                            {
                                MoneyCropTeacherStock("472", 20),
                                MoneyCropTeacherStock("473", 60),
                                MoneyCropTeacherStock("474", 80)
                            }
                        }
                    },
                    new
                    {
                        shop_id = "Festival_EggFestival_Pierre",
                        stock_preview = new
                        {
                            entries = new[]
                            {
                                MoneyCropTeacherStock("745", 100)
                            }
                        }
                    },
                    new
                    {
                        shop_id = "Festival_NightMarket_MagicBoat_Day1",
                        stock_preview = new
                        {
                            entries = new[]
                            {
                                MoneyCropTeacherStock("474", 10)
                            }
                        }
                    },
                    new
                    {
                        shop_id = "Sandy",
                        stock_preview = new
                        {
                            entries = new[]
                            {
                                MoneyCropTeacherStock("478", 100)
                            }
                        }
                    }
                }
            }),
            collision_grid = MoneyCropRuntimeField(new
            {
                location_id = "FarmHouse",
                width = 4,
                height = 4,
                notable_tiles = Array.Empty<object>()
            }),
            route_connectors = MoneyCropRuntimeField(new
            {
                location_id = "FarmHouse",
                connectors = new[]
                {
                    new
                    {
                        kind = "warp",
                        tile_x = 1,
                        tile_y = 0,
                        target_location = "Farm",
                        target_x = 1,
                        target_y = 1,
                        resolved = true
                    }
                }
            }),
            route_action_branch_coverage = MoneyCropRuntimeField(new
            {
                rows = Array.Empty<object>()
            }),
            route_graph = MoneyCropRuntimeField(new
            {
                edges = new object[]
                {
                    MoneyCropTeacherRouteEdge(
                        "FarmHouse", 1, 0, "Farm", 1, 1, true),
                    MoneyCropTeacherRouteEdge(
                        "Farm", 2, 0, "BusStop", 1, 1, true),
                    MoneyCropTeacherRouteEdge(
                        "BusStop", 2, 0, "Town", 1, 1, true),
                    MoneyCropTeacherRouteEdge(
                        "Town", 2, 0, "SeedShop", 1, 1, true),
                    MoneyCropTeacherRouteEdge(
                        "BusStop", 22, 8, "Desert", 18, 27, false),
                    MoneyCropTeacherRouteEdge(
                        "Desert", 6, 51, "SandyHouse", 4, 9, true),
                    new
                    {
                        kind = "shop_endpoint",
                        from_location = "SeedShop",
                        from_x = 4,
                        from_y = 18,
                        shop_id = "SeedShop",
                        action_type = "Buy",
                        direct_time_allowed = true,
                        festival_closed = false,
                        allowed_now = true,
                        resolved = false
                    },
                    new
                    {
                        kind = "shop_endpoint",
                        from_location = "SandyHouse",
                        from_x = 2,
                        from_y = 6,
                        shop_id = "Sandy",
                        action_type = "Buy",
                        direct_time_allowed = true,
                        festival_closed = false,
                        allowed_now = true,
                        resolved = false
                    }
                }
            })
        }, JsonDefaults.Options);
        state["current_location"] = JsonSerializer.SerializeToElement(new
        {
            map = MoneyCropRuntimeField(new { id = "FarmHouse" }),
            objects = MoneyCropRuntimeField(Array.Empty<object>()),
            terrain_features = MoneyCropRuntimeField(Array.Empty<object>()),
            shop_action_tiles = MoneyCropRuntimeField(Array.Empty<object>())
        }, JsonDefaults.Options);
        state["menus"] = JsonSerializer.SerializeToElement(new
        {
            active_menu = MoneyCropRuntimeField(new
            {
                is_open = false,
                type = "none"
            })
        }, JsonDefaults.Options);
        snapshot.GameTick = 150;
        snapshot.State = state;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(state);
        return snapshot;
    }

    private static SnapshotEnvelope MoneyCropTeacherLayoutSnapshot(
        int wateringCanWaterLeft = 40,
        bool includeRefillSource = true,
        string existingSeedId = "476",
        int existingSeedQuantity = 1,
        bool requireRefillClearance = false)
    {
        var source = MoneyCropTeacherSourceSnapshot(
            wateringCanWaterLeft,
            existingSeedId,
            existingSeedQuantity);
        var state = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["identity"] = source.State["identity"],
            ["time"] = source.State["time"],
            ["player"] = source.State["player"],
            ["farm"] = JsonSerializer.SerializeToElement(new
            {
                site_preparation_context = MoneyCropRuntimeField(new
                {
                    schema_version =
                        "farm_site_preparation_context.v2",
                    projection_status =
                        "complete_bounded_current_farm_region",
                    location_id = "Farm",
                    map_width = 4,
                    map_height = 3,
                    bounds = new
                    {
                        min_x = 0,
                        min_y = 0,
                        max_x = 3,
                        max_y = 2
                    },
                    tile_count = 12,
                    tiles = Enumerable.Range(0, 12).Select(index => new
                    {
                        tile_x = index % 4,
                        tile_y = index / 4,
                        status = includeRefillSource &&
                                index == (requireRefillClearance ? 0 : 11)
                            ? "blocked_not_diggable"
                            : requireRefillClearance && index is 2 or 6
                                ? "blocked_not_diggable"
                            : requireRefillClearance && index == 10
                                ? "requires_single_clearance_then_till"
                            : index == 0
                                ? "already_prepared"
                                : "ready_to_till",
                        map_passable = !includeRefillSource ||
                            index != (requireRefillClearance ? 0 : 11) &&
                            (!requireRefillClearance || index is not (2 or 6)),
                        native_blocked_without_characters =
                            includeRefillSource &&
                            index == (requireRefillClearance ? 0 : 11) ||
                            requireRefillClearance && index is 2 or 6 or 10,
                        watering_can_refill_source =
                            includeRefillSource &&
                            index == (requireRefillClearance ? 0 : 11),
                        watering_refill_approach_standable =
                            (!includeRefillSource ||
                                index != (requireRefillClearance ? 0 : 11)) &&
                            (!requireRefillClearance ||
                                index is not (2 or 6 or 10))
                    }).ToArray()
                })
            }, JsonDefaults.Options)
        };
        source.GameTick = 151;
        source.State = state;
        source.StateHash = SnapshotHash.ComputeStateHash(state);
        return source;
    }

    private static object MoneyCropTeacherCatalogRow(
        string seedId,
        int growDays,
        int? regrowDays,
        string harvestItemId,
        int salePrice,
        bool isRaised) => new
    {
        seed_id = seedId,
        seasons = new[] { "spring" },
        grow_days = growDays,
        regrow_days = regrowDays,
        harvest_item_id = harvestItemId,
        harvest_item_qualified_id = "(O)" + harvestItemId,
        harvest_context_tags = new[] { "category_vegetable" },
        harvest_unit_sale_price = salePrice,
        harvest_min_stack = 1,
        harvest_method = "Grab",
        is_paddy_crop = false,
        is_raised = isRaised,
        needs_watering = true,
        plantable_location_rule_count = 0
    };

    private static object MoneyCropTeacherStock(
        string seedId,
        int price) => new
    {
        item_id = seedId,
        qualified_item_id = "(O)" + seedId,
        display_name = "Seed " + seedId,
        price,
        currency = 0,
        stock = int.MaxValue,
        infinite_stock = true,
        stack = 1,
        quality = 0,
        currency_balance = 450,
        executor_purchase_preview_enabled = true,
        executor_block_reasons = Array.Empty<string>()
    };

    private static object MoneyCropTeacherRouteEdge(
        string fromLocation,
        int fromX,
        int fromY,
        string targetLocation,
        int targetX,
        int targetY,
        bool resolved) => new
    {
        kind = "warp",
        from_location = fromLocation,
        from_x = fromX,
        from_y = fromY,
        target_location = targetLocation,
        target_x = targetX,
        target_y = targetY,
        resolved
    };
}
