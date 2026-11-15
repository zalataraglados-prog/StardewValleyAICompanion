using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
{
    [Fact]
    public void SaleStockGiftIsBlockedBeforeRankingWhenNoUnreservedUnitRemains()
    {
        var (snapshot, ledger) = SaleStockConsumptionSource(1, 1);
        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, ["social.gift_npc"], commitmentLedger: ledger).Options).SocialCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("inventory_item_reserved_for_crop_sale", candidate.BlockReasons);
    }

    [Fact]
    public void SaleStockGiftFreshCompilationRechecksTheCurrentLedger()
    {
        var (snapshot, ledger) = SaleStockConsumptionSource(1, 1);
        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, ["social.gift_npc"]).Options).SocialCandidates);
        var plan = new DailyPlanCompiler().Compile([ToPrediction(candidate, "social.gift_npc")], snapshot.StateHash);
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot, ledger).Items
            .Where(row => row.OptionId == "executor.social_interact"));
        Assert.Equal("blocked", item.Status);
        Assert.Contains("inventory_item_reserved_for_crop_sale", item.BlockingReasons);
    }

    [Fact]
    public void SaleStockGiftKeepsFreeUnitsAndTalkWhileProtectingOtherRootReservations()
    {
        var (snapshot, ledger) = SaleStockConsumptionSource(2, 1);
        var source = JsonSerializer.Serialize(snapshot, JsonOptions); var original = JsonSerializer.Serialize(ledger, JsonOptions);
        var evaluator = new CandidateOptionAvailabilityEvaluator();
        var gift = Assert.Single(Assert.Single(evaluator.Evaluate(snapshot, ["social.gift_npc"], commitmentLedger: ledger).Options).SocialCandidates);
        Assert.True(gift.Available, string.Join(",", gift.BlockReasons));
        Assert.Contains(gift.Parameters, row => row.Name == "item_stack_before" && row.Value == "2");
        var plan = new DailyPlanCompiler().Compile([ToPrediction(gift, "social.gift_npc")], snapshot.StateHash);
        Assert.Equal("pending", Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot, ledger).Items
            .Where(row => row.OptionId == "executor.social_interact")).Status);
        Assert.Equal(source, JsonSerializer.Serialize(snapshot, JsonOptions)); Assert.Equal(original, JsonSerializer.Serialize(ledger, JsonOptions));
        ledger.MaterialReservations = ledger.MaterialReservations.Append(new MaterialReservation
        { ReservationId = "other-root", SourceDecisionId = "other-root", GoalId = "other-root", OwnerPlayerId = 1,
            NodeId = "player:1", SlotIndex = 0, QualifiedItemId = "(O)66", Quantity = 1, Status = StrategyCommitmentStatuses.Active,
            Purpose = "keep-for-other-root" }).ToArray();
        Assert.False(Assert.Single(Assert.Single(evaluator.Evaluate(snapshot, ["social.gift_npc"], commitmentLedger: ledger).Options).SocialCandidates).Available);
        Assert.True(Assert.Single(Assert.Single(evaluator.Evaluate(snapshot, ["social.talk_npc"], commitmentLedger: ledger).Options).SocialCandidates).Available);
        Assert.True(Assert.Single(Assert.Single(evaluator.Evaluate(snapshot, ["social.gift_npc"]).Options).SocialCandidates).Available);
    }

    [Theory]
    [InlineData("executor.social_interact", "gift", true)]
    [InlineData("executor.social_interact", "bouquet", true)]
    [InlineData("executor.social_interact", "propose_marriage", true)]
    [InlineData("executor.social_interact", "propose_roommate", true)]
    [InlineData("executor.social_interact", "talk", false)]
    [InlineData("executor.consume_food", "", true)]
    public void SaleStockSingleUsePreflightRespectsNativeConsumptionKinds(string option, string kind, bool consumes)
    {
        // Resource preflight, not a claim that the declared item passes native food/relationship legality.
        var (snapshot, ledger) = SaleStockConsumptionSource(1, 1);
        var parameters = new SmallModelActionParameter[] { new() { Name = "slot_index", Value = "0" },
            new() { Name = "qualified_item_id", Value = "(O)66" }, new() { Name = "social_action_kind", Value = kind } };
        Assert.Equal(consumes, CropSaleStockConsumptionPreflight.Evaluate(option, parameters, snapshot, ledger).Length > 0);
        Assert.Empty(CropSaleStockConsumptionPreflight.Evaluate(option, parameters, snapshot, null));
        var (freeSource, free) = SaleStockConsumptionSource(2, 1);
        Assert.Empty(CropSaleStockConsumptionPreflight.Evaluate(option, parameters, freeSource, free));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void SaleStockGiftReservationSurvivesCrossMapRouteGeneration(int stack, bool available)
    {
        var (snapshot, ledger) = SaleStockConsumptionSource(stack, 1);
        var npcs = JsonNode.Parse(snapshot.State["npcs"].GetRawText())!;
        npcs["social_interaction"]!["value"]![0]!["location_id"] = "SeedShop";
        snapshot.State["npcs"] = JsonSerializer.SerializeToElement(npcs, JsonOptions);
        var locations = JsonNode.Parse(snapshot.State["locations"].GetRawText())!;
        locations["route_connectors"]!["value"] = JsonNode.Parse("""
            {"location_id":"Town","connectors":[{"kind":"warp","tile_x":12,"tile_y":10,"target_location":"Farm","target_x":4,"target_y":12,"resolved":true}]}
            """);
        locations["route_graph"]!["value"] = JsonNode.Parse("""
            {"edges":[{"kind":"warp","from_location":"Town","from_x":12,"from_y":10,"target_location":"Farm","target_x":4,"target_y":12,"resolved":true},{"kind":"building_door","from_location":"Farm","from_x":6,"from_y":5,"target_location":"SeedShop","target_x":3,"target_y":9,"resolved":true}]}
            """);
        snapshot.State["locations"] = JsonSerializer.SerializeToElement(locations, JsonOptions);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State); ledger.SourceStateHash = snapshot.StateHash;
        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, ["social.gift_npc"], commitmentLedger: ledger).Options).SocialCandidates);
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.Equal(available, candidate.Available);
        Assert.Equal(!available, candidate.BlockReasons.Contains("inventory_item_reserved_for_crop_sale"));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void SaleStockFoodUsesTheSamePreflightInCandidateAndFreshCompiler(int stack, bool available)
    {
        var (snapshot, ledger) = SaleStockConsumptionSource(stack, 1);
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        var food = player["inventory"]!["value"]![0]!;
        food["item_id"] = "24"; food["qualified_item_id"] = "(O)24"; food["edibility"] = 10;
        player["health"] = JsonSerializer.SerializeToNode(new { status = "available", value = 50 });
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player, JsonOptions);
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!;
        var physical = farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"]![0]!;
        physical["item_id"] = "24"; physical["qualified_item_id"] = "(O)24";
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions);
        snapshot.State["mining"] = JsonSerializer.SerializeToElement(new
        { monsters = new { status = "available", value = Array.Empty<object>() },
            player_resources = new { status = "available", value = new { health = 50, max_health = 100 } } }, JsonOptions);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State); ledger.SourceStateHash = snapshot.StateHash;
        ledger.MaterialReservations[0].QualifiedItemId = "(O)24";
        ledger.MaterialReservations[0].Purpose = CropSaleStockReservationContract.TransferablePurpose("declared-crop", "declared-wave",
            MoneyRevenueCropRetentionAccount.InventoryIdentity(snapshot.State["player"].GetProperty("inventory").GetProperty("value")[0]), 0);
        var request = Request(snapshot.StateHash, "executor.consume_food");
        request.Actions[0].Parameters = [new() { Name = "slot_index", Value = "0" }, new() { Name = "qualified_item_id", Value = "(O)24" }];
        Assert.Equal("pending", Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items).Status);
        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot, ledger).Items);
        Assert.Equal(available ? "pending" : "blocked", item.Status);
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            [new StardewAI.Contracts.Options.OptionAvailabilityCandidate { OptionId = "executor.consume_food",
                Parameters = request.Actions[0].Parameters }], includeExecutorCalibrationOptions: true, commitmentLedger: ledger).Options);
        Assert.Equal(!available, candidate.BlockingReasons.Contains("inventory_item_reserved_for_crop_sale"));
    }

    [Theory]
    [InlineData("ledger")]
    [InlineData("save")]
    [InlineData("graph")]
    [InlineData("inventory")]
    [InlineData("duplicate")]
    public void SaleStockSingleUsePreflightRejectsStaleOrAmbiguousMaterialSources(string fault)
    {
        var (snapshot, ledger) = SaleStockConsumptionSource(2, 1);
        var parameters = new SmallModelActionParameter[] { new() { Name = "slot_index", Value = "0" },
            new() { Name = "qualified_item_id", Value = "(O)66" } };
        if (fault == "ledger") ledger.SourceStateHash = "stale";
        if (fault == "save") snapshot.SaveId.Value = "foreign";
        if (fault == "graph") snapshot.State.Remove("farm");
        if (fault == "inventory")
        {
            var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
            player["inventory"]!["value"]![0]!["stack"] = 3;
            snapshot.State["player"] = JsonSerializer.SerializeToElement(player, JsonOptions);
        }
        if (fault == "duplicate") parameters = parameters.Append(parameters[0]).ToArray();
        if (fault is "graph" or "inventory") { snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State); ledger.SourceStateHash = snapshot.StateHash; }
        Assert.NotEmpty(CropSaleStockConsumptionPreflight.Evaluate("executor.consume_food", parameters, snapshot, ledger));
    }

    private static (SnapshotEnvelope Snapshot, StrategyCommitmentLedger Ledger) SaleStockConsumptionSource(int stack, int reserved)
    {
        var snapshot = CompleteSocialSnapshot();
        snapshot.SaveId.Value = "declared-sale-stock-consumption"; snapshot.SaveId.Status = FieldStatus.Available;
        snapshot.PlayerId.Value = "1"; snapshot.PlayerId.Status = FieldStatus.Available;
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!.AsObject();
        player["inventory"]!["value"]![0]!["stack"] = stack;
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player, JsonOptions);
        var item = snapshot.State["player"].GetProperty("inventory").GetProperty("value")[0];
        var graph = new MaterialInventoryGraph { PlayerId = 1, PhysicalInventoryCount = 1, InventoryNodes = [new()
        {
            NodeId = "player:1", InventoryKind = "player_inventory", OwnerPlayerId = 1, ActorUseAuthorized = true,
            SupplyState = "available", LocationId = "Town", Slots = [new()
            { SlotIndex = 0, QualifiedItemId = "(O)66", ItemId = "66", Stack = stack, Quality = 0,
                RuntimeType = "StardewValley.Object", MaximumStackSize = 999 }]
        }] };
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(new { material_inventory_graph = new { status = "available", value = graph } }, JsonOptions);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        const string producer = "declared-sale-stock-source";
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "declared-sale-stock-consumption", SaveId = snapshot.SaveId.Value, PlayerId = "1", SourceStateHash = snapshot.StateHash,
            MaterialReservations = [new()
            {
                ReservationId = CropSaleStockReservationContract.ReservationId(producer, 0), SourceDecisionId = producer,
                GoalId = "earn_money", OwnerPlayerId = 1, NodeId = "player:1", SlotIndex = 0, QualifiedItemId = "(O)66",
                Quantity = reserved, Status = StrategyCommitmentStatuses.Active,
                Purpose = CropSaleStockReservationContract.TransferablePurpose("declared-crop", "declared-wave",
                    MoneyRevenueCropRetentionAccount.InventoryIdentity(item), 0)
            }]
        };
        return (snapshot, ledger);
    }
}
