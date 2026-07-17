using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class FullShipmentContributionTests
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

}
