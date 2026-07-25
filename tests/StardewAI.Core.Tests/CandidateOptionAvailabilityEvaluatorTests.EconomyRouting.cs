using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed partial class CandidateOptionAvailabilityEvaluatorTests
{
    [Fact]
    public void BuySuppliesAvailableWhenShopHasValueCandidate()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(BuySnapshot(entryOverride: """
              {
                "item_id":"472",
                "qualified_item_id":"(O)472",
                "price":20,
                "stock":2147483647,
                "infinite_stock":true,
                "can_buy_item":true,
                "can_afford_one_with_currency":true,
                "can_afford_one_with_trade_item":true,
                "could_inventory_accept":true,
                "executor_purchase_enabled":false
              }
            """), new[] { "economy.buy_supplies" })
            .Options[0];

        Assert.False(option.Available);
        Assert.True(option.ReadEligible);
        Assert.Equal("unbound", option.BindingStatus);
        Assert.Equal("not_evaluated", option.CompileStatus);
        Assert.False(option.PreviewOnly);
        Assert.DoesNotContain("purchase_executor_disabled", option.BlockingReasons);
        Assert.DoesNotContain("no_value_available_purchase_candidates", option.BlockingReasons);
        var candidate = Assert.Single(option.EconomicCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("buy_shop_item", candidate.Kind);
        Assert.Equal("(O)472", candidate.QualifiedItemId);
        Assert.Equal(20, candidate.UnitPrice);
    }

    [Fact]
    public void BuySuppliesUsesLocationsShopStockPreviewBeforeMenuOpens()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(BuyPreviewSnapshot("""
              {
                "item_id":"378",
                "qualified_item_id":"(O)378",
                "display_name":"Copper Ore",
                "price":150,
                "stock":2147483647,
                "infinite_stock":true,
                "currency_balance":500,
                "executor_purchase_preview_enabled":true,
                "executor_block_reasons":[]
              }
            """), new[] { "economy.buy_supplies" })
            .Options[0];

        Assert.False(option.Available);
        Assert.True(option.ReadEligible);
        Assert.Equal("unbound", option.BindingStatus);
        Assert.Equal("not_evaluated", option.CompileStatus);
        Assert.False(option.PreviewOnly);
        Assert.DoesNotContain("menus.shop_stock", option.MissingStateFactors);
        Assert.DoesNotContain("no_value_available_purchase_candidates", option.BlockingReasons);
        var candidate = Assert.Single(option.EconomicCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("buy-preview:Blacksmith:0", candidate.CandidateId);
        Assert.Equal("buy_shop_item", candidate.Kind);
        Assert.Equal("Blacksmith", candidate.ShopId);
        Assert.Equal("(O)378", candidate.QualifiedItemId);
        Assert.Equal(150, candidate.UnitPrice);
    }

    [Fact]
    public void BuySuppliesBlocksWhenNoShopStockEntryPassesValueGates()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(BuySnapshot(entryOverride: """
              {
                "item_id":"472",
                "qualified_item_id":"(O)472",
                "price":20,
                "stock":0,
                "infinite_stock":false,
                "can_buy_item":true,
                "can_afford_one_with_currency":false,
                "can_afford_one_with_trade_item":true,
                "could_inventory_accept":true,
                "executor_purchase_enabled":false
              }
            """), new[] { "economy.buy_supplies" })
            .Options[0];

        Assert.False(option.Available);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("no_value_available_purchase_candidates", option.BlockingReasons);
        Assert.Contains("shop_item_out_of_stock", option.BlockingReasons);
        Assert.Contains("insufficient_currency_for_purchase", option.BlockingReasons);
        Assert.DoesNotContain("purchase_executor_disabled", option.BlockingReasons);
        var candidate = Assert.Single(option.EconomicCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("shop_item_out_of_stock", candidate.BlockReasons);
    }

    [Fact]
    public void SellItemsAvailableWhenUnprotectedInventoryCandidateExists()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(SellSnapshot(inventoryItemOverride: """
              {
                "slot_index":0,
                "qualified_item_id":"(O)24",
                "stack":3,
                "category":-75,
                "can_be_shipped":true,
                "sell_to_store_price":35,
                "sale_price":35,
                "protected_from_auto_sell":false,
                "auto_sell_protection_reasons":[],
                "is_empty":false
              }
            """), new[] { "economy.sell_items" })
            .Options[0];

        Assert.False(option.Available);
        Assert.True(option.ReadEligible);
        Assert.Equal("unbound", option.BindingStatus);
        Assert.Equal("not_evaluated", option.CompileStatus);
        Assert.False(option.PreviewOnly);
        Assert.DoesNotContain("no_value_available_sell_candidates", option.BlockingReasons);
        var candidate = Assert.Single(option.EconomicCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("sell_shop_item", candidate.Kind);
        Assert.Equal(0, candidate.SlotIndex);
        Assert.False(candidate.CanShip);
        Assert.True(candidate.CanShopSell);
        Assert.Equal("SeedShop", candidate.ShopId);
        Assert.Equal(35, candidate.UnitPrice);
        Assert.Equal(105, candidate.TotalValue);
    }

    [Fact]
    public void SellItemsRejectsShopWithNoCategoryOrTagAcceptance()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(SellSnapshot(
                inventoryItemOverride: """
                  {
                    "slot_index":0,
                    "qualified_item_id":"(O)24",
                    "stack":3,
                    "category":-75,
                    "context_tags":["item_parsnip"],
                    "sell_to_store_price":35,
                    "protected_from_auto_sell":false,
                    "auto_sell_protection_reasons":[],
                    "is_empty":false
                  }
                """,
                sellContextOverride: """
                  {"kind":"shop_sell_context","shop_id":"SeedShop","currency":0,"read_only":false,"safety_timer":0,"held_item_present":false,"storage_shop":false,"sell_percentage":1.0,"custom_on_sell_present":false,"categories_to_sell":[],"tag_groups_to_sell":[]}
                """), new[] { "economy.sell_items" })
            .Options[0];

        var candidate = Assert.Single(option.EconomicCandidates);
        Assert.False(candidate.Available);
        Assert.False(candidate.CanShopSell);
        Assert.Contains("item_not_accepted_by_active_shop", candidate.BlockReasons);
    }

    [Fact]
    public void SellItemsUsesAllTagsAndNativeSellPercentage()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(SellSnapshot(
                inventoryItemOverride: """
                  {
                    "slot_index":0,
                    "item_id":"24",
                    "qualified_item_id":"(O)24",
                    "stack":3,
                    "category":-75,
                    "context_tags":["item_parsnip","color_yellow"],
                    "sell_to_store_price":35,
                    "protected_from_auto_sell":false,
                    "auto_sell_protection_reasons":[],
                    "is_empty":false
                  }
                """,
                sellContextOverride: """
                  {"kind":"shop_sell_context","shop_id":"TagShop","currency":0,"read_only":false,"safety_timer":0,"held_item_present":false,"storage_shop":false,"sell_percentage":0.5,"custom_on_sell_present":false,"categories_to_sell":[],"tag_groups_to_sell":[["item_parsnip","color_yellow"]]}
                """), new[] { "economy.sell_items" })
            .Options[0];

        var candidate = Assert.Single(option.EconomicCandidates);
        Assert.True(candidate.Available);
        Assert.True(candidate.CanShopSell);
        Assert.Equal("TagShop", candidate.ShopId);
        Assert.Equal(17, candidate.UnitPrice);
        Assert.Equal(51, candidate.TotalValue);
    }

    [Fact]
    public void SellItemsBlocksWhenInventoryCandidatesAreProtected()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(SellSnapshot(inventoryItemOverride: """
              {
                "slot_index":0,
                "qualified_item_id":"(O)24",
                "stack":3,
                "category":-75,
                "can_be_shipped":true,
                "sell_to_store_price":35,
                "sale_price":35,
                "protected_from_auto_sell":true,
                "auto_sell_protection_reasons":["special_item"],
                "is_empty":false
              }
            """), new[] { "economy.sell_items" })
            .Options[0];

        Assert.False(option.Available);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("no_value_available_sell_candidates", option.BlockingReasons);
        Assert.Contains("inventory_item_protected_from_auto_sell", option.BlockingReasons);
        var candidate = Assert.Single(option.EconomicCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("inventory_item_protected_from_auto_sell", candidate.BlockReasons);
    }

    [Fact]
    public void BoundRouteCandidateReusesCompilerTargetBranchGate()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":20,"height":40,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"unsupported_for_route_training_count":1,"rows":[{"tile_x":12,"tile_y":34,"branch":"SkullDoor","route_training_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[]
            {
                Candidate("exploration.visit_location",
                    Parameter("target_tile_x", "12"),
                    Parameter("target_tile_y", "34"))
            })
            .Options[0];

        Assert.False(option.Available);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("unsupported_route_action_branch_at_target", option.BlockingReasons);
        Assert.DoesNotContain("route_executor_disabled", option.BlockingReasons);
        Assert.DoesNotContain("queue_global_compiler_block", option.BlockingReasons);
    }

    [Fact]
    public void VisitLocationEmitsRouteConnectorEventCandidatesFromTransparentConnectors()
    {
        var snapshot = RouteConnectorSnapshot(routeTrainingBlocked: false);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "exploration.visit_location" })
            .Options[0];

        Assert.False(option.Available);
        Assert.True(option.ReadEligible);
        Assert.Equal("unbound", option.BindingStatus);
        Assert.Equal("not_evaluated", option.CompileStatus);
        Assert.False(option.PreviewOnly);
        Assert.DoesNotContain("route_executor_disabled", option.BlockingReasons);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("route:Farm:12,10:warp", candidate.CandidateId);
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.True(candidate.Available);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(12, candidate.TileX);
        Assert.Equal(10, candidate.TileY);
        Assert.Equal("player.tile=12,10;route_connector=warp;expected_target_location=Town;fresh_snapshot_replan_required=true;expected_arrival_tile=1,2", candidate.ExpectedEffect);
        Assert.Equal(120, candidate.EstimatedTicks);
        Assert.Empty(candidate.BlockReasons);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "execution_option_id" && parameter.Value == "executor.traverse_connector");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "connector_kind" && parameter.Value == "warp");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_target_location" && parameter.Value == "Town");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_arrival_tile_x" && parameter.Value == "1");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_arrival_tile_y" && parameter.Value == "2");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "estimated_ticks" && parameter.Value == "120");
    }

    [Fact]
    public void RouteConnectorEventCandidateKeepsCompilerBlockReasonsAtCandidateLevel()
    {
        var snapshot = RouteConnectorSnapshot(routeTrainingBlocked: true);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "exploration.visit_location" })
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("unsupported_route_action_branch_at_target", candidate.BlockReasons);
        Assert.DoesNotContain("queue_global_compiler_block", candidate.BlockReasons);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("no_available_route_connector_candidates", option.BlockingReasons);
        Assert.Contains("unsupported_route_action_branch_at_target", option.BlockingReasons);
    }

    [Fact]
    public void VisitLocationEmitsRouteRepairClearObstacleCandidateWhenConnectorTileIsClearable()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[{"tile_x":12,"tile_y":10,"type":"Grass"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":20,"height":20,"notable_tiles":[{"tile_x":12,"tile_y":10,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"Farm","connector_count":1,"connectors":[{"kind":"warp","tile_x":12,"tile_y":10,"target_location":"Town","target_x":1,"target_y":2,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":12,"tile_y":10,"branch":"Warp","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "exploration.visit_location" })
            .Options[0];

        Assert.False(option.Available);
        Assert.True(option.ReadEligible);
        Assert.Equal("unbound", option.BindingStatus);
        Assert.Equal("not_evaluated", option.CompileStatus);
        Assert.DoesNotContain("route_executor_disabled", option.BlockingReasons);
        var route = Assert.Single(option.EventCandidates, candidate => candidate.Kind == "route_connector_tile");
        Assert.False(route.Available);
        Assert.Contains("route_path_target_blocked_by_collision_grid", route.BlockReasons);
        var repair = Assert.Single(option.EventCandidates, candidate => candidate.Kind == "clear_obstacle_tile");
        Assert.True(repair.Available);
        Assert.Equal("route_repair_clearable_obstacle", repair.AvailabilityClass);
        Assert.StartsWith("route-repair:route:Farm:12,10:warp:clear:Farm:12,10:grass", repair.CandidateId);
        Assert.Equal("route_repair_for=route:Farm:12,10:warp;move_to_adjacent=11,10;current_location.obstacle[12,10]=clear;clear_kind=grass;source=Grass;max_tool_swings=8", repair.ExpectedEffect);
    }

    [Fact]
    public void VisitLocationEmitsRouteRepairClearObstacleCandidateWhenPathSegmentIsClearable()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[{"tile_x":1,"tile_y":0,"type":"Grass"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":4,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"Farm","connector_count":1,"connectors":[{"kind":"warp","tile_x":3,"tile_y":0,"target_location":"Town","target_x":1,"target_y":2,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":3,"tile_y":0,"branch":"Warp","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "exploration.visit_location" })
            .Options[0];

        var route = Assert.Single(option.EventCandidates, candidate => candidate.Kind == "route_connector_tile");
        Assert.False(route.Available);
        Assert.Contains("route_path_blocked_by_collision_grid", route.BlockReasons);
        var repair = Assert.Single(option.EventCandidates, candidate => candidate.Kind == "clear_obstacle_tile");
        Assert.True(repair.Available);
        Assert.StartsWith("route-repair:route:Farm:3,0:warp:clear:Farm:1,0:grass", repair.CandidateId);
        Assert.Equal("route_repair_for=route:Farm:3,0:warp;move_to_adjacent=0,0;current_location.obstacle[1,0]=clear;clear_kind=grass;source=Grass;max_tool_swings=8", repair.ExpectedEffect);
    }

    [Fact]
    public void ClearObstacleCandidateIsBlockedWhenTransparentEnergyIsInsufficient()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[{"tile_x":1,"tile_y":0,"qualified_item_id":"(O)343","name":"Stone"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":4,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.clear_obstacle" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.Equal("blocked", option.Status);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Equal(2, candidate.EnergyCost);
        Assert.Contains("insufficient_energy_for_clear_obstacle", candidate.BlockReasons);
        Assert.Contains("no_available_clear_obstacle_candidates", option.BlockingReasons);
    }

    [Fact]
    public void VisitLocationDoesNotEmitRouteRepairCandidateWhenClearEnergyBudgetIsInsufficient()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[{"tile_x":1,"tile_y":0,"qualified_item_id":"(O)343","name":"Stone"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":4,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"Farm","connector_count":1,"connectors":[{"kind":"warp","tile_x":3,"tile_y":0,"target_location":"Town","target_x":1,"target_y":2,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":3,"tile_y":0,"branch":"Warp","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "exploration.visit_location" })
            .Options[0];

        Assert.Single(option.EventCandidates, candidate => candidate.Kind == "route_connector_tile");
        Assert.DoesNotContain(option.EventCandidates, candidate => candidate.Kind == "clear_obstacle_tile");
    }

    [Fact]
    public void ClearObstacleCandidateIsBlockedWhenItWouldExceedDayTimeBudget()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":2559,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[{"tile_x":1,"tile_y":0,"qualified_item_id":"(O)343","name":"Stone"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":4,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.clear_obstacle" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("clear_obstacle_would_exceed_day_time_budget", candidate.BlockReasons);
        Assert.Contains("no_available_clear_obstacle_candidates", option.BlockingReasons);
    }

    [Fact]
    public void BoundInteractCandidateReusesCompilerMissingTargetGate()
    {
        var snapshot = InteractSnapshot(menuOpen: false, branch: "OpenShop", blocked: false);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { Candidate("executor.interact") }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.False(option.Available);
        Assert.Equal("blocked", option.Status);
        Assert.Equal("unbound", option.BindingStatus);
        Assert.Equal("not_evaluated", option.CompileStatus);
        Assert.DoesNotContain("interact_target_tile_required", option.BlockingReasons);
        Assert.DoesNotContain("interact_executor_disabled", option.BlockingReasons);
        Assert.DoesNotContain("queue_global_compiler_block", option.BlockingReasons);
    }

    [Fact]
    public void ValidBoundInteractCandidateIsAvailable()
    {
        var snapshot = InteractSnapshot(menuOpen: false, branch: "OpenShop", blocked: false);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[]
            {
                Candidate("executor.interact",
                    Parameter("target_tile_x", "11"),
                    Parameter("target_tile_y", "10"),
                    Parameter("interaction_kind", "map_action"),
                    Parameter("expected_action_type", "OpenShop"))
            }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.True(option.Available);
        Assert.Equal("bound", option.BindingStatus);
        Assert.Equal("ready", option.CompileStatus);
        Assert.False(option.PreviewOnly);
        Assert.True(option.ExecutorEnabled);
        Assert.DoesNotContain("interact_executor_disabled", option.BlockingReasons);
        Assert.DoesNotContain("interact_expected_action_type_mismatch", option.BlockingReasons);
        Assert.DoesNotContain("queue_global_compiler_block", option.BlockingReasons);
    }

    [Fact]
    public void InteractOptionEmitsEndpointCandidatesWithMoveToAdjacentPreview()
    {
        var snapshot = InteractEndpointSnapshot(menuOpen: false, branch: "OpenShop", routeTrainingBlocked: false);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.interact" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.False(option.Available);
        Assert.Equal("unbound", option.Status);
        Assert.Equal("unbound", option.BindingStatus);
        Assert.Equal("not_evaluated", option.CompileStatus);
        Assert.DoesNotContain("interact_target_tile_required", option.BlockingReasons);
        Assert.DoesNotContain("interact_executor_disabled", option.BlockingReasons);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("interact:Town:11,10:OpenShop:SeedShop", candidate.CandidateId);
        Assert.Equal("interact_endpoint", candidate.Kind);
        Assert.Empty(candidate.BlockReasons);
        Assert.True(candidate.Available);
        Assert.Equal("Town", candidate.LocationId);
        Assert.Equal(11, candidate.TileX);
        Assert.Equal(10, candidate.TileY);
        Assert.Equal("move_to_adjacent=10,10;preview_interact=OpenShop", candidate.ExpectedEffect);
        Assert.Equal(30, candidate.EstimatedTicks);
    }

    [Fact]
    public void InteractOptionEmitsJojaShopEndpointCandidate()
    {
        var snapshot = InteractEndpointSnapshot(
            menuOpen: false,
            branch: "JojaShop",
            routeTrainingBlocked: false,
            action: "JojaShop",
            parsed: "\"parsed\":{\"kind\":\"joja_shop\",\"shop_id\":\"Joja\"}");

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.interact" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("interact:Town:11,10:JojaShop:Joja", candidate.CandidateId);
        Assert.Equal("move_to_adjacent=10,10;preview_interact=JojaShop", candidate.ExpectedEffect);
        Assert.DoesNotContain("interact_expected_action_type_mismatch", candidate.BlockReasons);
    }

    [Fact]
    public void InteractOptionEmitsDialogueShopEndpointCandidateWithOwnerPresent()
    {
        var snapshot = InteractEndpointSnapshot(
            menuOpen: false,
            branch: "AnimalShop",
            routeTrainingBlocked: false,
            action: "AnimalShop",
            parsed: "\"parsed\":{\"kind\":\"dialogue_shop\",\"shop_id\":\"AnimalShop\",\"owner_npc\":\"Marnie\",\"owner_service_area\":{\"x\":9,\"y\":8,\"width\":5,\"height\":3},\"dialogue_key\":\"Marnie\",\"shop_response_key\":\"Supplies\"}",
            ownerServiceStatus: "\"owner_service_status\":{\"owner_required\":true,\"owner_npc\":\"Marnie\",\"owner_found\":true,\"owner_tile_x\":11,\"owner_tile_y\":9,\"in_service_area\":true,\"block_reason\":null}",
            npcPositions: "[{\"name\":\"Marnie\",\"location_id\":\"AnimalShop\",\"tile_x\":11,\"tile_y\":9}]");

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.interact" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("interact:Town:11,10:AnimalShop:AnimalShop", candidate.CandidateId);
        Assert.Equal("move_to_adjacent=10,10;preview_interact=AnimalShop", candidate.ExpectedEffect);
        Assert.DoesNotContain("interact_expected_action_type_mismatch", candidate.BlockReasons);
        Assert.DoesNotContain("interact_shop_owner_npc_not_at_service_counter", candidate.BlockReasons);
    }

    [Fact]
    public void InteractOptionBlocksDialogueShopEndpointWhenOwnerNpcAbsent()
    {
        var snapshot = InteractEndpointSnapshot(
            menuOpen: false,
            branch: "Carpenter",
            routeTrainingBlocked: false,
            action: "Carpenter",
            parsed: "\"parsed\":{\"kind\":\"dialogue_shop\",\"shop_id\":\"Carpenter\",\"owner_npc\":\"Robin\",\"owner_service_area\":{\"x\":6,\"y\":17,\"width\":5,\"height\":3},\"dialogue_key\":\"carpenter\",\"shop_response_key\":\"Shop\"}",
            ownerServiceStatus: "\"owner_service_status\":{\"owner_required\":true,\"owner_npc\":\"Robin\",\"owner_found\":true,\"owner_tile_x\":21,\"owner_tile_y\":4,\"in_service_area\":false,\"block_reason\":\"owner_npc_not_at_service_counter\"}",
            npcPositions: "[{\"name\":\"Robin\",\"location_id\":\"ScienceHouse\",\"tile_x\":21,\"tile_y\":4}]");

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.interact" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("interact_shop_owner_npc_not_at_service_counter", candidate.BlockReasons);
        Assert.DoesNotContain("interact_expected_action_type_mismatch", candidate.BlockReasons);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("no_available_interact_endpoint_candidates", option.BlockingReasons);
        Assert.Contains("interact_shop_owner_npc_not_at_service_counter", option.BlockingReasons);
    }

    [Fact]
    public void InteractOptionBlocksShopEndpointWhenServiceTimeStatusDisallows()
    {
        var snapshot = InteractEndpointSnapshot(
            menuOpen: false,
            branch: "OpenShop",
            routeTrainingBlocked: false,
            serviceTimeStatus: "\"service_time_status\":{\"current_time\":800,\"time_gate_known\":true,\"open_time\":900,\"close_time\":1700,\"time_allowed\":false,\"allowed_now\":false,\"block_reasons\":[\"shop_not_open_yet\"]}");

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.interact" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("interact_shop_service_time_blocked", candidate.BlockReasons);
        Assert.Equal("blocked", option.Status);
        Assert.Equal("windowed_available", candidate.AvailabilityClass);
        Assert.False(candidate.AllowedNow);
        Assert.True(candidate.AllowedToday);
        Assert.Equal(900, candidate.NextOpenTime);
        Assert.Equal(900, candidate.EffectiveOpenTime);
        Assert.Equal(1700, candidate.ClosesAt);
        Assert.Equal(3600, candidate.WaitCost);
        Assert.Contains("shop_not_open_yet", candidate.GateReasons);
        Assert.Contains("interact_shop_service_time_blocked", candidate.GateReasons);
        Assert.Contains("no_available_interact_endpoint_candidates", option.BlockingReasons);
    }


    [Fact]
    public void InteractEndpointCandidateBlocksWhenMenuIsOpenAndBranchBlocked()
    {
        var snapshot = InteractEndpointSnapshot(menuOpen: true, branch: "SkullDoor", routeTrainingBlocked: true);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.interact" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("interact_menu_must_be_clear", candidate.BlockReasons);
        Assert.Contains("interact_unsupported_action_branch_at_target", candidate.BlockReasons);
        Assert.Contains("interact_expected_action_type_mismatch", candidate.BlockReasons);
    }

}
