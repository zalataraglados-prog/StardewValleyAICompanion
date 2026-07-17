using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FullShipmentContributionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static SnapshotEnvelope BuildSnapshot(string inventoryJson, string? fullShipmentProgressJson)
    {
        var worldProgressFields = fullShipmentProgressJson != null
            ? $@"""full_shipment_progress"":{fullShipmentProgressJson},"
            : "";

        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($$"""
        {
          "time": {
            "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
            "player": {
            "inventory": {{inventoryJson}},
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":65,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":18,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "total_money_earned": {"value":10000,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "level": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_skull_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_rusty_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "married_or_roommate": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "farmhouse_upgrade_level": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "shipping_bins": {"value":[{"days_of_construction_left":0,"player_within_shipping_range":true,"tile_x":68,"tile_y":15,"tile_width":2,"tile_height":1,"interaction_stand_tile_x":67,"interaction_stand_tile_y":15}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "grandpa_score": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            {{worldProgressFields}}
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "community_center": {"value":{"location_accessible":false,"completed":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "joja_membership": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_location_id":"Farm","current_location_is_home":true,"bed_tile_x":10,"bed_tile_y":5,"bed_tile_has_bed":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"map_width":80,"map_height":80},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":80,"height":80,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": {
            "friendships": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":null,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sell_context": {"value":{"read_only":false,"held_item_present":false,"safety_timer":0,"categories_to_sell":[-75,-79,-81,-7,-2,-12,-18,-26]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-14T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string InventoryItem(string slotIndex, string itemId, string qualifiedItemId, string displayName, int stack, int sellPrice, int salePrice, bool canBeShipped, int category, bool? isEmpty = null)
    {
        return $$"""
        {"is_empty":{{(isEmpty ?? false).ToString().ToLowerInvariant()}},"item_id":"{{itemId}}","qualified_item_id":"{{qualifiedItemId}}","display_name":"{{displayName}}","slot_index":{{slotIndex}},"stack":{{stack}},"sell_to_store_price":{{sellPrice}},"sale_price":{{salePrice}},"can_be_shipped":{{canBeShipped.ToString().ToLowerInvariant()}},"category":{{category}},"auto_sell_protection_reasons":[]}
        """;
    }

    private static string Inventory(params string[] items)
    {
        return $$"""{"value":[{{string.Join(",", items)}}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}""";
    }

    private static string FullShipmentProgressField(string status, int eligibleCount, params string[] items)
    {
        return $$"""{"status":"{{status}}","value":{"eligible_item_count":{{eligibleCount}},"items":[{{string.Join(",", items)}}]},"source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}""";
    }

    private static string FsItem(string itemId, string qualifiedItemId, string displayName, int category, string objectType, int currentShippedCount, bool shipped)
    {
        return $$"""{"item_id":"{{itemId}}","qualified_item_id":"{{qualifiedItemId}}","display_name":"{{displayName}}","category":{{category}},"object_type":"{{objectType}}","current_shipped_count":{{currentShippedCount}},"shipped":{{shipped.ToString().ToLowerInvariant()}}}""";
    }

    private EventCandidate? FindShipCandidate(OptionAvailabilityEnvelope availability, string itemId)
    {
        var shipOption = availability.Options.FirstOrDefault(o => o.OptionId == "economy.ship_items");
        if (shipOption == null) return null;
        return shipOption.EventCandidates.FirstOrDefault(c =>
            string.Equals(c.ItemId, itemId, StringComparison.Ordinal));
    }

    // === Evaluator snapshot tests ===

    [Fact]
    public void ShippingCapableMissingEligibleItemHasKnownTrueAndContributesTrue()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = FullShipmentProgressField("available", 1,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false));
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.Contains("full_shipment_known=true", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_eligible=true", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_current_shipped_count=0", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_already_shipped=false", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_contributes=true", candidate.ExpectedEffect);
    }

    [Fact]
    public void ShippingCapableAlreadyShippedItemDoesNotContribute()
    {
        var inventory = Inventory(
            InventoryItem("0", "60", "(O)60", "Emerald", 1, 250, 250, true, -12));
        var fsProgress = FullShipmentProgressField("available", 1,
            FsItem("60", "(O)60", "Emerald", -12, "Minerals", 5, true));
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "60");
        Assert.NotNull(candidate);
        Assert.Contains("full_shipment_known=true", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_eligible=true", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_current_shipped_count=5", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_already_shipped=true", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_contributes=false", candidate.ExpectedEffect);
    }

    [Fact]
    public void ShopSellOnlyCandidateNeverContributes()
    {
        var inventory = Inventory(
            InventoryItem("0", "80", "(O)80", "Quartz", 1, 25, 0, false, -2));
        var fsProgress = FullShipmentProgressField("available", 1,
            FsItem("80", "(O)80", "Quartz", -2, "Minerals", 0, false));
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "80");
        Assert.Null(candidate);
    }

    [Fact]
    public void KnownIneligibleItemHasEligibleFalseAndContributesFalse()
    {
        var inventory = Inventory(
            InventoryItem("0", "999", "(O)999", "Unknown", 1, 100, 100, true, -75));
        var fsProgress = FullShipmentProgressField("available", 2,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false),
            FsItem("60", "(O)60", "Emerald", -12, "Minerals", 1, true));
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "999");
        Assert.NotNull(candidate);
        Assert.Contains("full_shipment_known=true", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_eligible=false", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_contributes=false", candidate.ExpectedEffect);
    }

    [Fact]
    public void MissingFullShipmentProgressFieldYieldsKnownFalseAndContributesFalse()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var snapshot = BuildSnapshot(inventory, null);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.Contains("full_shipment_known=false", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_contributes=false", candidate.ExpectedEffect);
    }

    [Fact]
    public void MissingCurrentShippedCountYieldsKnownFalse()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = """{"status":"available","value":{"eligible_item_count":1,"items":[{"item_id":"24","qualified_item_id":"(O)24","display_name":"Parsnip","category":-75,"object_type":"Basic","shipped":false}]},"source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}""";
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.Contains("full_shipment_known=false", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_contributes=false", candidate.ExpectedEffect);
    }

    [Fact]
    public void NonnumericCurrentShippedCountYieldsKnownFalse()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = """{"status":"available","value":{"eligible_item_count":1,"items":[{"item_id":"24","qualified_item_id":"(O)24","display_name":"Parsnip","category":-75,"object_type":"Basic","current_shipped_count":"not_a_number","shipped":false}]},"source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}""";
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.Contains("full_shipment_known=false", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_contributes=false", candidate.ExpectedEffect);
    }

    [Fact]
    public void NonpositiveSalePriceProducesCanShipFalseAndContributesFalse()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 0, true, -75));
        var fsProgress = FullShipmentProgressField("available", 1,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false));
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.Null(candidate);
    }

    [Fact]
    public void StaleOrUnavailableStatusYieldsKnownFalse()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = FullShipmentProgressField("stale", 1,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false));
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.Contains("full_shipment_known=false", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_contributes=false", candidate.ExpectedEffect);
    }

    [Fact]
    public void MalformedItemsArrayYieldsKnownFalse()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = """{"status":"available","value":{"eligible_item_count":1,"items":"not_an_array"},"source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}""";
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.Contains("full_shipment_known=false", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_contributes=false", candidate.ExpectedEffect);
    }

    [Fact]
    public void DuplicateItemIdsYieldsKnownFalse()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = FullShipmentProgressField("available", 2,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false),
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false));
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.Contains("full_shipment_known=false", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_contributes=false", candidate.ExpectedEffect);
    }

    [Fact]
    public void EligibleItemCountMismatchYieldsKnownFalse()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = FullShipmentProgressField("available", 99,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false));
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.Contains("full_shipment_known=false", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_contributes=false", candidate.ExpectedEffect);
    }

    [Fact]
    public void ShippedBoolMustBeConsistentWithCountOrKnownFalse()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = FullShipmentProgressField("available", 1,
            """{"item_id":"24","qualified_item_id":"(O)24","display_name":"Parsnip","category":-75,"object_type":"Basic","current_shipped_count":0,"shipped":true}""");
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.Contains("full_shipment_known=false", candidate.ExpectedEffect);
        Assert.Contains("full_shipment_contributes=false", candidate.ExpectedEffect);
    }

    // === Pipeline tests (ranker + binder clone) ===

    [Fact]
    public void EventCandidateRankerCopiesFullShipmentFieldsFromEconomicCandidate()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "economy.sell_items", AverageTotalReward = 0.1 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "economy.sell_items",
                    EconomicCandidates = new[]
                    {
                        new EconomicCandidate
                        {
                            CandidateId = "sell:0",
                            Kind = "sell_shop_item",
                            Available = true,
                            QualifiedItemId = "(O)74",
                            DisplayName = "Prismatic Shard",
                            SlotIndex = 0,
                            Quantity = 1,
                            UnitPrice = 500,
                            TotalValue = 500,
                            CanShip = true,
                            CanShopSell = false,
                            FullShipmentKnown = true,
                            FullShipmentEligible = true,
                            FullShipmentCurrentShippedCount = 0,
                            FullShipmentAlreadyShipped = false,
                            FullShipmentContributes = true
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var candidate = Assert.Single(ranked);
        Assert.Equal("sell:0", candidate.CandidateId);
        Assert.Equal(500, candidate.TotalValue);
        Assert.True(candidate.CanShip);
        Assert.False(candidate.CanShopSell);
        Assert.True(candidate.FullShipmentKnown);
        Assert.True(candidate.FullShipmentEligible);
        Assert.Equal(0, candidate.FullShipmentCurrentShippedCount);
        Assert.False(candidate.FullShipmentAlreadyShipped);
        Assert.True(candidate.FullShipmentContributes);
    }

    [Fact]
    public void EventCandidateRankerPreservesCanShipAndCanShopSellCorrectly()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "economy.sell_items", AverageTotalReward = 0.1 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "economy.sell_items",
                    EconomicCandidates = new[]
                    {
                        new EconomicCandidate
                        {
                            CandidateId = "sell:shop_only",
                            Kind = "sell_shop_item",
                            Available = true,
                            QualifiedItemId = "(O)24",
                            CanShip = false,
                            CanShopSell = true,
                            TotalValue = 35
                        },
                        new EconomicCandidate
                        {
                            CandidateId = "sell:bin_ok",
                            Kind = "sell_shop_item",
                            Available = true,
                            QualifiedItemId = "(O)74",
                            CanShip = true,
                            CanShopSell = false,
                            TotalValue = 500
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        Assert.Equal(2, ranked.Length);
        var shopOnly = ranked.First(c => c.CandidateId == "sell:shop_only");
        Assert.False(shopOnly.CanShip);
        Assert.True(shopOnly.CanShopSell);

        var binOk = ranked.First(c => c.CandidateId == "sell:bin_ok");
        Assert.True(binOk.CanShip);
        Assert.False(binOk.CanShopSell);
    }

    [Fact]
    public void CloneCandidatePreservesFullShipmentFields()
    {
        var source = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:item:1:0",
            OptionId = "economy.sell_items",
            Kind = "sell_shop_item",
            Rank = 1,
            Score = 0.42,
            ExpectedReward = 0.1,
            Available = true,
            ItemId = "74",
            QualifiedItemId = "(O)74",
            DisplayName = "Prismatic Shard",
            ShopId = "ShippingBin",
            SlotIndex = 0,
            Quantity = 1,
            UnitPrice = 500,
            TotalValue = 500,
            CanShip = true,
            CanShopSell = false,
            FullShipmentKnown = true,
            FullShipmentEligible = true,
            FullShipmentCurrentShippedCount = 0,
            FullShipmentAlreadyShipped = false,
            FullShipmentContributes = true,
            LocationId = "Farm",
            AllowedNow = true,
            AllowedToday = true,
            TimelineStatus = "ready_now"
        };

        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = GrandpaSnapshot().StateHash,
            DirectionId = "earn_money",
            RankedCandidates = new[] { source }
        }, GrandpaSnapshot());

        Assert.Equal("ready", result.BindingStatus);
        var bound = result.BoundCandidates[0];

        Assert.True(bound.CanShip);
        Assert.False(bound.CanShopSell);
        Assert.True(bound.FullShipmentKnown);
        Assert.True(bound.FullShipmentEligible);
        Assert.Equal(0, bound.FullShipmentCurrentShippedCount);
        Assert.False(bound.FullShipmentAlreadyShipped);
        Assert.True(bound.FullShipmentContributes);
        Assert.Equal("(O)74", bound.QualifiedItemId);
        Assert.Equal(500, bound.TotalValue);
    }

    [Fact]
    public void ShopSellOnlyCandidateRankedOnValueSignalButFullShipmentContributesFalse()
    {
        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "economy.sell_items", AverageTotalReward = 0.1 }
            }
        };
        var availability = new OptionAvailabilityEnvelope
        {
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "economy.sell_items",
                    EconomicCandidates = new[]
                    {
                        new EconomicCandidate
                        {
                            CandidateId = "sell:shop_only_gem",
                            Kind = "sell_shop_item",
                            Available = true,
                            QualifiedItemId = "(O)60",
                            DisplayName = "Emerald",
                            Quantity = 1,
                            UnitPrice = 250,
                            TotalValue = 250,
                            CanShip = false,
                            CanShopSell = true,
                            FullShipmentKnown = true,
                            FullShipmentEligible = true,
                            FullShipmentCurrentShippedCount = 0,
                            FullShipmentAlreadyShipped = false,
                            FullShipmentContributes = false
                        },
                        new EconomicCandidate
                        {
                            CandidateId = "sell:ship_eligible_gem",
                            Kind = "sell_shop_item",
                            Available = true,
                            QualifiedItemId = "(O)74",
                            DisplayName = "Prismatic Shard",
                            Quantity = 1,
                            UnitPrice = 500,
                            TotalValue = 500,
                            CanShip = true,
                            CanShopSell = false,
                            FullShipmentKnown = true,
                            FullShipmentEligible = true,
                            FullShipmentCurrentShippedCount = 0,
                            FullShipmentAlreadyShipped = false,
                            FullShipmentContributes = true
                        }
                    }
                }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        Assert.Equal(2, ranked.Length);
        var diamond = ranked.First(c => c.CandidateId == "sell:ship_eligible_gem");
        Assert.True(diamond.FullShipmentContributes);

        var emerald = ranked.First(c => c.CandidateId == "sell:shop_only_gem");
        Assert.False(emerald.FullShipmentContributes);
        Assert.False(emerald.CanShip);
    }

    // === Fail-closed and transparency tests ===

    [Fact]
    public void MissingStandTileBlocksCandidateWithTransparentReason()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = FullShipmentProgressField("available", 1,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false));

        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($$"""
        {
          "time": { "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1} },
          "player": {
            "inventory": {{inventory}},
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":65,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":18,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "total_money_earned": {"value":10000,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "level": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_skull_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_rusty_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "married_or_roommate": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "farmhouse_upgrade_level": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "shipping_bins": {"value":[{"days_of_construction_left":0,"player_within_shipping_range":true,"tile_x":68,"tile_y":15,"tile_width":2,"tile_height":1}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "grandpa_score": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "full_shipment_progress": {{fsProgress}},
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "community_center": {"value":{"location_accessible":false,"completed":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "joja_membership": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_location_id":"Farm","current_location_is_home":true,"bed_tile_x":10,"bed_tile_y":5,"bed_tile_has_bed":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"map_width":80,"map_height":80},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":80,"height":80,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": { "friendships": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1} },
          "quests": { "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1} },
          "menus": {
            "active_menu": {"value":null,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sell_context": {"value":{"read_only":false,"held_item_present":false,"safety_timer":0,"categories_to_sell":[-75,-79,-81,-7,-2,-12,-18,-26]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """, JsonOptions)!;
        var snapshot = new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-14T00:00:00Z",
            Completeness = "complete",
            State = state
        };

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.False(candidate.Available);
        Assert.Contains("shipping_bin_no_transparent_interaction_stand_tile", candidate.BlockReasons);
    }

    [Fact]
    public void ShippingBinContentsReadFromTransparentSnapshot()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = FullShipmentProgressField("available", 1,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false));

        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($$"""
        {
          "time": { "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1} },
          "player": {
            "inventory": {{inventory}},
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":65,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":18,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "total_money_earned": {"value":10000,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "level": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_skull_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_rusty_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "married_or_roommate": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "farmhouse_upgrade_level": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "shipping_bins": {"value":[{"days_of_construction_left":0,"player_within_shipping_range":true,"tile_x":68,"tile_y":15,"tile_width":2,"tile_height":1,"interaction_stand_tile_x":67,"interaction_stand_tile_y":15,"contents":[{"item_id":"24","qualified_item_id":"(O)24","count":3},{"item_id":"60","qualified_item_id":"(O)60","count":1}],"contents_total_count":4,"contents_distinct_item_count":2}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "grandpa_score": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "full_shipment_progress": {{fsProgress}},
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "community_center": {"value":{"location_accessible":false,"completed":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "joja_membership": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_location_id":"Farm","current_location_is_home":true,"bed_tile_x":10,"bed_tile_y":5,"bed_tile_has_bed":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"map_width":80,"map_height":80},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":80,"height":80,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": { "friendships": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1} },
          "quests": { "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1} },
          "menus": {
            "active_menu": {"value":null,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sell_context": {"value":{"read_only":false,"held_item_present":false,"safety_timer":0,"categories_to_sell":[-75,-79,-81,-7,-2,-12,-18,-26]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """, JsonOptions)!;
        var snapshot = new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-14T00:00:00Z",
            Completeness = "complete",
            State = state
        };

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.True(candidate.Available);
        Assert.Contains("bin_current_count_of_item=3", candidate.ExpectedEffect);
    }

    [Fact]
    public void ShippingBinContentsEmptyWhenContentsFieldMissing()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = FullShipmentProgressField("available", 1,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false));
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var candidate = FindShipCandidate(availability, "24");
        Assert.NotNull(candidate);
        Assert.Contains("bin_current_count_of_item=0", candidate.ExpectedEffect);
    }

    // === Static source-guard tests (adapter) ===

    [Fact]
    public void AdapterUsesStaticIsPotentialBasicShippedAndNoItemRegistryCreateOrInstanceCall()
    {
        var adapterPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "StardewAI.TransparentBridge", "Adapters", "ProgressReadAdapter.cs"));
        var source = File.ReadAllText(adapterPath);

        Assert.Contains("Object.isPotentialBasicShipped(itemId, category, objectType)", source);

        Assert.DoesNotContain("ItemRegistry.Create", source);
        Assert.DoesNotContain(".isPotentialBasicShipped()", source);
        Assert.DoesNotContain("Utility.getFarmerItemsShippedPercent", source);
        Assert.DoesNotContain("GetAllData", source);
    }

    [Fact]
    public void AdapterScopeUsesUseSeparateWalletsNotReferenceEquals()
    {
        var source = FarmReadAdapterSources.All;

        Assert.Contains("useSeparateWallets", source);
        Assert.DoesNotContain("ReferenceEquals", source);
        Assert.Contains("\"personal\"", source);
        Assert.Contains("\"shared\"", source);
    }

    [Fact]
    public void AdapterEmitsStandTilesArrayAndContentsWithSignature()
    {
        var source = FarmReadAdapterSources.All;

        Assert.Contains("stand_tiles", source);
        Assert.Contains("contents_signature", source);
        Assert.Contains("contents_total_count", source);
        Assert.Contains("contents_distinct_item_count", source);
        Assert.Contains("contents_truncated", source);
        Assert.Contains("SHA256", source);
        Assert.Contains("ComputeContentsSignature", source);
    }

    // === Catalog and binding tests ===

    [Fact]
    public void FullShipmentDirectionBindsOnlyExactContributingCandidate()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_full_shipment",
            RankedCandidates = new[]
            {
                new PolicyEventCandidatePrediction
                {
                    CandidateId = "ship:parsnip:0",
                    OptionId = "economy.ship_items",
                    Kind = "ship_inventory_item_to_bin",
                    Available = true,
                    AllowedNow = true,
                    AllowedToday = true,
                    TimelineStatus = "ready_now",
                    CanShip = true,
                    FullShipmentKnown = true,
                    FullShipmentEligible = true,
                    FullShipmentCurrentShippedCount = 0,
                    FullShipmentAlreadyShipped = false,
                    FullShipmentContributes = true
                }
            }
        }, snapshot);

        Assert.Equal("ready", result.BindingStatus);
        Assert.Equal("ready", result.BindingCoverageStatus);
        var bound = Assert.Single(result.BoundCandidates);
        Assert.Equal("ship:parsnip:0", bound.CandidateId);
        Assert.Contains(bound.Parameters,
            p => p.Name == "grandpa_direction_id" && p.Value == "complete_full_shipment");
        Assert.Empty(result.MissingTransparentFields);
        Assert.Empty(result.MissingCapabilities);
        Assert.Contains("world_progress.shipping_collection", result.CoveredTransparentFields);
        Assert.Contains("world_progress.full_shipment_progress", result.CoveredTransparentFields);
    }

    [Fact]
    public void FullShipmentDirectionRejectsAlreadyShippedCandidateEvenWhenContributionFlagConflicts()
    {
        var snapshot = GrandpaSnapshot();
        var result = new GrandpaDirectionDailyCandidateBinding().Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_full_shipment",
            RankedCandidates = new[]
            {
                new PolicyEventCandidatePrediction
                {
                    CandidateId = "ship:parsnip:already-shipped",
                    OptionId = "economy.ship_items",
                    Kind = "ship_inventory_item_to_bin",
                    Available = true,
                    AllowedNow = true,
                    AllowedToday = true,
                    TimelineStatus = "ready_now",
                    CanShip = true,
                    FullShipmentKnown = true,
                    FullShipmentEligible = true,
                    FullShipmentCurrentShippedCount = 1,
                    FullShipmentAlreadyShipped = true,
                    FullShipmentContributes = true
                }
            }
        }, snapshot);

        Assert.Equal("blocked", result.BindingStatus);
        Assert.Empty(result.BoundCandidates);
        Assert.Contains(result.BlockReasons,
            reason => reason.Contains("full_shipment_current_shipped_count_not_zero", StringComparison.Ordinal));
    }

    [Fact]
    public void CatalogCompleteFullShipmentEntryIsDirectAndKeepsTransparentCoverage()
    {
        var entry = GrandpaDirectionCatalog.Entries
            .First(e => e.DirectionId == "complete_full_shipment");

        Assert.True(entry.DirectBindingEnabled);
        Assert.Equal("grandpa.direct.complete_full_shipment", entry.BindingRuleId);
        Assert.Equal(new[] { "economy.ship_items" }, entry.PermittedOptionIds);
        Assert.Equal(new[] { "ship_inventory_item_to_bin" }, entry.PermittedCandidateKinds);
        Assert.Empty(entry.RequiredTransparentFields);
        Assert.Contains("world_progress.shipping_collection", entry.CoveredTransparentFields);
        Assert.Contains("world_progress.full_shipment_progress", entry.CoveredTransparentFields);
        Assert.Equal(2, entry.CoveredTransparentFields.Length);
        Assert.Empty(entry.RequiredCapabilities);
        Assert.Contains("exact transparent contribution evidence", entry.BlockReasonTemplate);
    }

    [Fact]
    public void CatalogHas12EntriesAndFullShipmentIsDirect()
    {
        var entries = GrandpaDirectionCatalog.Entries;
        Assert.Equal(12, entries.Length);

        var fullShipment = entries.Single(e => e.DirectionId == "complete_full_shipment");
        Assert.True(fullShipment.DirectBindingEnabled);
        Assert.Empty(fullShipment.RequiredTransparentFields);
        Assert.NotEmpty(fullShipment.CoveredTransparentFields);
        Assert.Empty(fullShipment.RequiredCapabilities);
    }

    [Fact]
    public void CoveredTransparentFieldsResultIsNotAliasedToCatalog()
    {
        var snapshot = GrandpaSnapshot();
        var binding = new GrandpaDirectionDailyCandidateBinding();
        var result = binding.Bind(new GrandpaDirectionBindingRequest
        {
            StateHash = snapshot.StateHash,
            DirectionId = "complete_full_shipment"
        }, snapshot);

        var catalogEntry = GrandpaDirectionCatalog.Entries
            .First(e => e.DirectionId == "complete_full_shipment");

        var catalogFields = catalogEntry.CoveredTransparentFields;
        var resultFields = result.CoveredTransparentFields;

        Assert.Same(catalogFields, catalogEntry.CoveredTransparentFields);
        Assert.NotSame(catalogFields, resultFields);

        resultFields[0] = "mutated";
        Assert.NotEqual("mutated", catalogFields[0]);
    }

    // === Contract arithmetic / DTO sorting tests ===

    [Fact]
    public void FullShipmentItemsSortedByItemIdThenQualifiedItemId()
    {
        var progress = CreateProgress(
            eligible: new[]
            {
                ("(O)80", "80", "Quartz", -75, "Basic", 0),
                ("(O)60", "60", "Emerald", -75, "Basic", 0),
                ("(O)24", "24", "Parsnip", -75, "Basic", 1),
                ("(O)80", "80", "Quartz", -75, "Basic", 0)
            },
            shippedItemIds: new[] { "24" });

        var ids = progress.Items.Select(i => i.QualifiedItemId).ToArray();
        Assert.Equal(new[] { "(O)24", "(O)60", "(O)80", "(O)80" }, ids);

        var itemIds = progress.Items.Select(i => i.ItemId).ToArray();
        Assert.Equal(new[] { "24", "60", "80", "80" }, itemIds);
    }

    [Fact]
    public void FullShipmentProgressCompleteWhenAllEligibleItemsShipped()
    {
        var progress = CreateProgress(
            eligible: new[] { ("(O)24", "24", "Parsnip", -75, "Basic", 1), ("(O)80", "80", "Quartz", -75, "Basic", 1) },
            shippedItemIds: new[] { "24", "80" });

        Assert.Equal(2, progress.EligibleItemCount);
        Assert.Equal(2, progress.ShippedEligibleItemCount);
        Assert.Equal(0, progress.MissingItemCount);
        Assert.Equal(1.0, progress.CompletionRatio);
        Assert.True(progress.Complete);
        Assert.Empty(progress.MissingItemIds);
    }

    [Fact]
    public void FullShipmentProgressEmptyWhenNoEligibleItems()
    {
        var progress = CreateProgress(
            eligible: Array.Empty<(string, string, string, int, string, int)>(),
            shippedItemIds: new[] { "24" });

        Assert.Equal(0, progress.EligibleItemCount);
        Assert.Equal(0, progress.ShippedEligibleItemCount);
        Assert.Equal(0, progress.MissingItemCount);
        Assert.Equal(0, progress.CompletionRatio);
        Assert.False(progress.Complete);
    }

    [Fact]
    public void MissingItemIdsSortedOrdinally()
    {
        var progress = CreateProgress(
            eligible: new[]
            {
                ("(O)80", "80", "Quartz", -75, "Basic", 0),
                ("(O)60", "60", "Emerald", -75, "Basic", 0),
                ("(O)24", "24", "Parsnip", -75, "Basic", 1)
            },
            shippedItemIds: new[] { "24" });

        var missingIds = progress.MissingItemIds;
        Assert.Equal(new[] { "60", "80" }, missingIds);
    }

    // === E2E pipeline test ===

    [Fact]
    public void EndToEndShipCandidateThroughCompilerToQueueItem()
    {
        var inventory = Inventory(
            InventoryItem("0", "24", "(O)24", "Parsnip", 5, 17, 17, true, -75));
        var fsProgress = FullShipmentProgressField("available", 1,
            FsItem("24", "(O)24", "Parsnip", -75, "Basic", 0, false));
        var snapshot = BuildSnapshot(inventory, fsProgress);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);

        var shipOption = availability.Options.Single(o => o.OptionId == "economy.ship_items");
        Assert.NotEmpty(shipOption.EventCandidates);

        var report = new BaselineTrainingReport
        {
            OptionScores = new[]
            {
                new BaselineOptionScore { OptionId = "economy.ship_items", AverageTotalReward = 0.1 }
            }
        };

        var ranked = new EventCandidateRanker().Rank(report, availability);

        var shipCandidates = ranked.Where(c => c.Kind == "ship_inventory_item_to_bin").ToArray();
        Assert.NotEmpty(shipCandidates);

        var first = shipCandidates[0];

        Assert.Equal("Farm", first.LocationId);
        Assert.NotNull(first.TileX);
        Assert.NotNull(first.TileY);

        Assert.Equal(1, first.Quantity);
        Assert.Equal("24", first.ItemId);
        Assert.Equal("(O)24", first.QualifiedItemId);
        Assert.Equal(0, first.SlotIndex);
        Assert.True(first.FullShipmentKnown);
        Assert.True(first.FullShipmentEligible);
        Assert.Equal(0, first.FullShipmentCurrentShippedCount);
        Assert.False(first.FullShipmentAlreadyShipped);
        Assert.True(first.FullShipmentContributes);

        var planCompiler = new DailyPlanCompiler();
        var plan = planCompiler.Compile(
            new[] { first },
            snapshot.StateHash,
            "test_e2e",
            "training_singleplayer",
            maxCandidates: 1);

        Assert.NotEmpty(plan.Steps);
        var shipStep = plan.Steps.FirstOrDefault(s => s.Kind == "ship_inventory_item_to_bin");
        Assert.NotNull(shipStep);
        Assert.Equal("Farm", shipStep.TargetLocation);

        var queueCompiler = new ActionQueueCompiler();
        var queue = queueCompiler.Compile(plan, snapshot);

        var diagMessages = string.Join("; ", queue.CompilerDiagnostics);
        var blockingReasons = queue.Items.SelectMany(i => i.BlockingReasons).Distinct().ToArray();
        var missingState = queue.Items.SelectMany(i => i.MissingStateFactors).Distinct().ToArray();
        var reasonsStr = "Diagnostics: " + diagMessages + " | Blocking: " + string.Join("; ", blockingReasons) + " | MissingState: " + string.Join("; ", missingState);

        Assert.True(queue.Status == "pending", reasonsStr);

        Assert.Equal("pending", queue.Status);
        var queueItem = queue.Items.FirstOrDefault(i => i.OptionId == "executor.ship_inventory_item_to_bin");
        Assert.NotNull(queueItem);

        Assert.Equal("pending", queueItem.Status);
        Assert.Equal(OptionBehaviorCategories.Mechanical, queueItem.BehaviorCategory);
        Assert.Equal(CompilerResponsibilities.FullActionExpansion, queueItem.CompilerResponsibility);
        Assert.Equal(TrainingRoles.ExecutorCalibration, queueItem.TrainingRole);

        Assert.Contains(queueItem.NormalizedCommand.Parameters,
            p => p.Name == "slot_index" && p.Value == "0");
        Assert.Contains(queueItem.NormalizedCommand.Parameters,
            p => p.Name == "qualified_item_id" && p.Value == "(O)24");
        Assert.Contains(queueItem.NormalizedCommand.Parameters,
            p => p.Name == "quantity" && p.Value == "1");
        Assert.Contains(queueItem.NormalizedCommand.Parameters,
            p => p.Name == "stand_tile_x");
        Assert.Contains(queueItem.NormalizedCommand.Parameters,
            p => p.Name == "stand_tile_y");
    }

    // === Helper methods ===

    private static FullShipmentProgressRef CreateProgress(
        (string qualifiedItemId, string itemId, string displayName, int category, string objectType, int shippedCount)[] eligible,
        string[] shippedItemIds)
    {
        var items = eligible
            .Select(e => new FullShipmentItemProgressRef
            {
                QualifiedItemId = e.qualifiedItemId,
                ItemId = e.itemId,
                DisplayName = e.displayName,
                Category = e.category,
                ObjectType = e.objectType,
                CurrentShippedCount = e.shippedCount,
                Shipped = e.shippedCount > 0
            })
            .OrderBy(i => i.ItemId, StringComparer.Ordinal)
            .ThenBy(i => i.QualifiedItemId, StringComparer.Ordinal)
            .ToArray();

        var shippedCount = items.Count(i => i.Shipped);
        var totalCount = items.Length;
        var missing = items.Where(i => !i.Shipped).Select(i => i.ItemId).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        return new FullShipmentProgressRef
        {
            EligibleItemCount = totalCount,
            ShippedEligibleItemCount = shippedCount,
            MissingItemCount = totalCount - shippedCount,
            CompletionRatio = totalCount > 0 ? (double)shippedCount / totalCount : 0,
            Complete = shippedCount == totalCount && totalCount > 0,
            Items = items,
            MissingItemIds = missing
        };
    }

    private static SnapshotEnvelope GrandpaSnapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "time": {
            "year": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "day": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sunny","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "total_money_earned": {"value":10000,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "level": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_skull_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_rusty_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "married_or_roommate": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "farmhouse_upgrade_level": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "shipping_bins": {"value":[{"days_of_construction_left":0,"player_within_shipping_range":true}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "grandpa_score": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "community_center": {"value":{"location_accessible":false,"completed":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "joja_membership": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": {
            "friendships": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":null,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-14T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    [Fact]
    public void ExecutorShipInventoryItemToBinIsRegistered()
    {
        var option = new StardewAI.Core.OptionRegistry.OptionRegistry().GetRequired("executor.ship_inventory_item_to_bin");
        Assert.NotNull(option);
        Assert.Equal("economy", option.Domain);
        Assert.Contains("inventory item deposited into shipping bin", option.EstimatedEffects);
        Assert.Contains("never_ship_protected_items", option.SafetyConstraints);
    }

    [Fact]
    public void ShipCandidateKindIsCorrect()
    {
        var snapshot = BuildSnapshot(
            Inventory(
                InventoryItem("0", "388", "(O)388", "Coal", 5, 15, 15, true, -15)),
            FullShipmentProgressField("available", 1, FsItem("388", "(O)388", "Coal", -15, "Basic", 0, false)));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);
        var shipCandidate = FindShipCandidate(availability, "388");
        Assert.NotNull(shipCandidate);
        Assert.Equal("ship_inventory_item_to_bin", shipCandidate.Kind);
        Assert.Contains("executor_kind=ship_inventory_item_to_bin", shipCandidate.ExpectedEffect);
        Assert.Contains("slot_index=0", shipCandidate.ExpectedEffect);
    }

    [Fact]
    public void ShipCandidateHasSlotIndexParameter()
    {
        var snapshot = BuildSnapshot(
            Inventory(
                InventoryItem("2", "388", "(O)388", "Coal", 3, 15, 15, true, -15)),
            FullShipmentProgressField("available", 1, FsItem("388", "(O)388", "Coal", -15, "Basic", 0, false)));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);
        var shipCandidate = FindShipCandidate(availability, "388");
        Assert.NotNull(shipCandidate);
        var slotParam = shipCandidate.Parameters.FirstOrDefault(p => p.Name == "slot_index");
        Assert.NotNull(slotParam);
        Assert.Equal("2", slotParam.Value);
    }

    [Fact]
    public void TrainingExecutionRequestSlotIndexSerializes()
    {
        var req = new TrainingExecutionRequest
        {
            OptionId = "executor.ship_inventory_item_to_bin",
            SlotIndex = 3,
            QualifiedItemId = "(O)388",
            Quantity = 1
        };
        var json = JsonSerializer.Serialize(req, JsonOptions);
        Assert.Contains("\"slot_index\":3", json);
        Assert.Contains("\"option_id\":\"executor.ship_inventory_item_to_bin\"", json);
    }

    [Fact]
    public void TrainingExecutionResultShipFieldsSerialize()
    {
        var result = new TrainingExecutionResult
        {
            OptionId = "executor.ship_inventory_item_to_bin",
            PrimitiveKind = "ship_inventory_item_to_bin",
            ShipSlotIndex = 3,
            ShipQualifiedItemId = "(O)388",
            ShipInventoryCountBefore = 5,
            ShipInventoryCountAfter = 4,
            ShipBinCountBefore = 0,
            ShipBinCountAfter = 1,
            ShipBinTotalCountBefore = 10,
            ShipBinTotalCountAfter = 11,
            ShipBinDistinctCountBefore = 3,
            ShipBinDistinctCountAfter = 4,
            ShipBinSignatureBefore = "before_sig",
            ShipBinSignatureAfter = "after_sig",
            ShipBasicShippedCountBefore = 0,
            ShipSourceDate = "12345"
        };
        var json = JsonSerializer.Serialize(result, JsonOptions);
        Assert.Contains("\"ship_slot_index\":3", json);
        Assert.Contains("\"ship_qualified_item_id\":\"(O)388\"", json);
        Assert.Contains("\"ship_inventory_count_before\":5", json);
        Assert.Contains("\"ship_inventory_count_after\":4", json);
        Assert.Contains("\"ship_bin_count_before\":0", json);
        Assert.Contains("\"ship_bin_count_after\":1", json);
        Assert.Contains("\"primitive_kind\":\"ship_inventory_item_to_bin\"", json);
        Assert.Contains("\"ship_source_date\":\"12345\"", json);
    }

    [Fact]
    public void ShipCandidateShowsRuntimeVerified()
    {
        var snapshot = BuildSnapshot(
            Inventory(
                InventoryItem("0", "388", "(O)388", "Coal", 5, 15, 15, true, -15)),
            FullShipmentProgressField("available", 1, FsItem("388", "(O)388", "Coal", -15, "Basic", 0, false)));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "economy.ship_items" }, includeExecutorCalibrationOptions: true);
        var shipCandidate = FindShipCandidate(availability, "388");
        Assert.NotNull(shipCandidate);
        Assert.Contains("shipping_executor_status=runtime_verified", shipCandidate.ExpectedEffect);
        var statusParam = shipCandidate.Parameters.FirstOrDefault(p => p.Name == "shipping_executor_available");
        Assert.NotNull(statusParam);
        Assert.Equal("runtime_verified", statusParam.Value);
    }
}
