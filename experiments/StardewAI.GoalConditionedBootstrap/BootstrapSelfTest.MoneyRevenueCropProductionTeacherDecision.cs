using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

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

        var tampered = JsonSerializer.Deserialize<
            MoneyRevenueCropProductionDecisionRequest>(
                JsonSerializer.Serialize(decision, JsonDefaults.Options),
                JsonDefaults.Options)!;
        tampered.Commitments[0].Tiles[0] =
            new MoneyRevenueCropPlantingTile(99, 99);
        var tamperedPath = Path.Combine(root, "tampered-decision.json");
        Write(tamperedPath, tampered);
        var rejected = false;
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
    }

    private static SnapshotEnvelope MoneyCropTeacherSourceSnapshot()
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
            money = MoneyCropRuntimeField(450),
            inventory = MoneyCropRuntimeField(new[]
            {
                new
                {
                    slot_index = 0,
                    qualified_item_id = "(O)476",
                    stack = 1
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
                    "476", 6, null, "192", 30, false)
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
                                qualified_item_id = "(O)476",
                                stack = 1
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
                shops = new[]
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
                    }
                }
            })
        }, JsonDefaults.Options);
        snapshot.GameTick = 100;
        snapshot.State = state;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(state);
        return snapshot;
    }

    private static SnapshotEnvelope MoneyCropTeacherLayoutSnapshot()
    {
        var source = MoneyCropTeacherSourceSnapshot();
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
                        "farm_site_preparation_context.v1",
                    projection_status =
                        "complete_bounded_current_farm_region",
                    location_id = "Farm",
                    bounds = new
                    {
                        min_x = 68,
                        min_y = 16,
                        max_x = 71,
                        max_y = 18
                    },
                    tile_count = 12,
                    tiles = Enumerable.Range(0, 12).Select(index => new
                    {
                        tile_x = 68 + index % 4,
                        tile_y = 16 + index / 4,
                        status = index == 0
                            ? "already_prepared"
                            : "ready_to_till"
                    }).ToArray()
                })
            }, JsonDefaults.Options)
        };
        source.GameTick = 101;
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
        qualified_item_id = "(O)" + seedId,
        price,
        currency = 0,
        executor_purchase_preview_enabled = true
    };
}
