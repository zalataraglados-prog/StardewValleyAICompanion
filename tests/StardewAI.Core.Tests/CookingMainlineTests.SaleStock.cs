using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class CookingMainlineTests
{
    [Theory]
    [InlineData("player")]
    [InlineData("built")]
    [InlineData("mini")]
    public void SaleStockCookingCandidateCannotConsumeReservedNativeIngredient(string storage)
    {
        var (snapshot, ledger) = CookingSaleStockSource(storage);
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, [Intent()], true, ledger).Options);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("crop_sale_stock_cooking_material_reserved", candidate.BlockReasons);
    }

    [Theory]
    [InlineData("player")]
    [InlineData("built")]
    [InlineData("mini")]
    public void SaleStockCookingFreshCompilerUsesOriginalNativeSourceInsteadOfOldCandidate(string storage)
    {
        var (snapshot, ledger) = CookingSaleStockSource(storage);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, [Intent()], true);
        var ranked = new EventCandidateRanker().Rank(new(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal("pending", Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items).Status);
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot, ledger).Items);
        Assert.Equal("blocked", item.Status);
        Assert.Contains("crop_sale_stock_cooking_material_reserved", item.BlockingReasons);
    }

    [Theory]
    [InlineData("player")]
    [InlineData("built")]
    [InlineData("mini")]
    public void SaleStockCookingFreeNativeUnitsKeepTheOriginalRecipeAndInputs(string storage)
    {
        var (snapshot, ledger) = CookingSaleStockSource(storage, stack: 2);
        var before = JsonSerializer.Serialize(snapshot); var original = JsonSerializer.Serialize(ledger);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, [Intent()], true, ledger);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(",", candidate.BlockReasons));
        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new(), availability), snapshot.StateHash);
        Assert.Equal("pending", Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot, ledger).Items).Status);
        Assert.Equal(before, JsonSerializer.Serialize(snapshot)); Assert.Equal(original, JsonSerializer.Serialize(ledger));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void SaleStockCookingAlsoProtectsNativeQiSeasoning(int stack, bool available)
    {
        var (snapshot, ledger) = CookingSaleStockSource("built", stack, seasoningStock: true);
        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, [Intent()], true, ledger).Options).EventCandidates);
        Assert.Equal(available, candidate.Available);
        Assert.Equal(!available, candidate.BlockReasons.Contains("crop_sale_stock_cooking_material_reserved"));
    }

    [Fact]
    public void SaleStockCookingGroupsIngredientAndSeasoningAgainstTheSamePhysicalBudget()
    {
        // Declared overlapping consumption projection, not a claim about a vanilla recipe's ingredients.
        var (snapshot, ledger) = CookingSaleStockSource("built", 2, seasoningStock: true);
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        var row = player["cooking"]!["value"]!["rows"]![0]!;
        var ingredient = row["ingredient_rows"]![0]!;
        ingredient["requirement_id_or_category"] = "917";
        ingredient["native_consumption_plan"]![0]!["source_id"] = "player:1";
        ingredient["native_consumption_plan"]![0]!["slot_index"] = 1;
        ingredient["native_consumption_plan"]![0]!["qualified_item_id"] = "(O)917";
        row["ingredient_rows_json"] = row["ingredient_rows"]!.ToJsonString();
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        ReanchorCooking(snapshot, ledger);
        Assert.Contains("crop_sale_stock_cooking_material_reserved", CropSaleStockCookingPreflight.Evaluate(snapshot, ledger, CookingRow(snapshot)));
    }

    [Theory]
    [InlineData("missing_access")]
    [InlineData("duplicate_access")]
    [InlineData("access_authority")]
    [InlineData("node_authority")]
    [InlineData("locked")]
    [InlineData("location")]
    [InlineData("missing_container")]
    [InlineData("incomplete_plan")]
    [InlineData("stale_ledger")]
    [InlineData("foreign_save")]
    public void SaleStockCookingDoesNotGuessMissingOrForeignNativeMaterialSources(string fault)
    {
        var (snapshot, ledger) = CookingSaleStockSource("mini", 2);
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!;
        var graph = farm["material_inventory_graph"]!["value"]!;
        var access = graph["access_points"]!.AsArray();
        if (fault == "missing_access") access.Clear();
        if (fault == "duplicate_access") access.Add(access[0]!.DeepClone());
        if (fault == "access_authority") access[0]!["actor_use_authorized"] = false;
        if (fault == "node_authority") graph["inventory_nodes"]![1]!["actor_use_authorized"] = false;
        if (fault == "locked") access[0]!["locked_by_other_player"] = true;
        if (fault == "location") access[0]!["location_id"] = "foreign-home";
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
        if (fault is "missing_container" or "incomplete_plan")
        {
            var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
            var row = player["cooking"]!["value"]!["rows"]![0]!;
            if (fault == "missing_container") row["material_container_ids"] = new JsonArray();
            else row["ingredient_rows"]![0]!["native_consumption_plan"]![0]!["amount"] = 0;
            snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        }
        ReanchorCooking(snapshot, ledger);
        if (fault == "stale_ledger") ledger.SourceStateHash = "stale";
        if (fault == "foreign_save") snapshot.SaveId.Value = "foreign";
        Assert.NotEmpty(CropSaleStockCookingPreflight.Evaluate(snapshot, ledger, CookingRow(snapshot)));
        Assert.Empty(CropSaleStockCookingPreflight.Evaluate(snapshot, null, CookingRow(snapshot)));
    }

    [Fact]
    public void SaleStockCookingCannotReplaceTheOriginalNativePlanWithCallerParameters()
    {
        var (snapshot, ledger) = CookingSaleStockSource("built");
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, [Intent()], true);
        var plan = new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        step.Parameters = step.Parameters.Select(value => value.Name == "ingredient_rows_json" ? P(value.Name, "[]") : value).ToArray();
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot, ledger).Items);
        Assert.Contains("cook_recipe_projection_drifted", item.BlockingReasons);
        Assert.Contains("crop_sale_stock_cooking_material_reserved", item.BlockingReasons);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void SaleStockCookingChecksTheOriginalRecipeBeforeRoutingToAnotherMap(int stack, bool available)
    {
        var (snapshot, ledger) = CookingSaleStockSource("built", stack);
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        player["location_id"]!["value"] = "Town";
        player["energy"] = JsonSerializer.SerializeToNode(new { status = "available", value = 270 });
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        snapshot.State["time"] = JsonSerializer.SerializeToElement(new { time = new { status = "available", value = 900 } });
        var locations = JsonNode.Parse(snapshot.State["locations"].GetRawText())!;
        locations["collision_grid"]!["value"]!["location_id"] = "Town";
        locations["route_action_branch_coverage"] = JsonSerializer.SerializeToNode(new { status = "available", value = new { rows = Array.Empty<object>() } });
        locations["route_graph"]!["value"] = JsonNode.Parse("""
            {"edges":[{"kind":"warp","from_location":"Town","from_x":6,"from_y":5,"target_location":"FarmHouse","target_x":4,"target_y":5,"resolved":true}]}
            """);
        locations["route_connectors"] = JsonSerializer.SerializeToNode(new { status = "available", value = JsonSerializer.Deserialize<JsonElement>("""
            {"location_id":"Town","connectors":[{"kind":"warp","tile_x":6,"tile_y":5,"target_location":"FarmHouse","target_x":4,"target_y":5,"resolved":true}]}
            """) });
        snapshot.State["locations"] = JsonSerializer.SerializeToElement(locations);
        ReanchorCooking(snapshot, ledger);
        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, [Intent()], true, ledger).Options).EventCandidates);
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.True(!available || candidate.Available, string.Join(",", candidate.BlockReasons));
        Assert.Equal(available, candidate.Available);
        Assert.Equal(!available, candidate.BlockReasons.Contains("crop_sale_stock_cooking_material_reserved"));
    }

    private static JsonElement CookingRow(SnapshotEnvelope snapshot) => snapshot.State["player"].GetProperty("cooking").GetProperty("value").GetProperty("rows")[0];
    private static void ReanchorCooking(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger)
    { snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State); ledger.SourceStateHash = snapshot.StateHash; }

    private static (SnapshotEnvelope Snapshot, StrategyCommitmentLedger Ledger) CookingSaleStockSource(string storage, int stack = 1,
        bool seasoningStock = false)
    {
        var snapshot = Snapshot();
        snapshot.SaveId.Value = "declared-cooking-stock"; snapshot.SaveId.Status = FieldStatus.Available;
        snapshot.PlayerId.Value = "1"; snapshot.PlayerId.Status = FieldStatus.Available;
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        var row = player["cooking"]!["value"]!["rows"]![0]!;
        var ingredient = row["ingredient_rows"]![0]!;
        var alias = storage == "player" ? "player:1" : storage == "built" ? "kitchen-fridge:FarmHouse" : "mini-fridge:FarmHouse:8,7";
        ingredient["native_consumption_plan"]![0]!["source_id"] = alias;
        ingredient["available_count_before_this_ingredient"] = stack;
        row["ingredient_rows_json"] = row["ingredient_rows"]!.ToJsonString();
        if (storage == "player") player["inventory"]!["value"]!.AsArray().Add(JsonNode.Parse(
            "{\"slot_index\":0,\"item_id\":\"176\",\"qualified_item_id\":\"(O)176\",\"stack\":" + stack + ",\"quality\":0,\"is_empty\":false}"));
        if (seasoningStock)
        {
            player["inventory"]!["value"]![0]!["stack"] = stack;
            row["seasoning_rows"]![0]!["available_count_before_seasoning"] = stack;
            row["seasoning_rows_json"] = row["seasoning_rows"]!.ToJsonString();
        }
        var container = storage == "mini" ? alias : "kitchen-fridge:FarmHouse";
        row["material_container_ids"] = new JsonArray(container);
        row["material_container_topology_json"] = row["material_container_ids"]!.ToJsonString();
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        var playerSlots = snapshot.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray().Select(item => new MaterialInventorySlot
        { SlotIndex = item.GetProperty("slot_index").GetInt32(), QualifiedItemId = item.GetProperty("qualified_item_id").GetString()!,
            Stack = item.GetProperty("stack").GetInt32(), Quality = 0, RuntimeType = "StardewValley.Object", MaximumStackSize = 999 }).ToArray();
        var fridgeId = storage == "mini" ? "chest:FarmHouse:8,7" : "chest:FarmHouse:9,8";
        var graph = new MaterialInventoryGraph { PlayerId = 1, PhysicalInventoryCount = 2, InventoryNodes = [new()
        { NodeId = "player:1", InventoryKind = "player_inventory", LocationId = "FarmHouse", OwnerPlayerId = 1,
            ActorUseAuthorized = true, SupplyState = "available", Slots = playerSlots }, new()
        { NodeId = fridgeId, InventoryKind = "fridge", LocationId = "FarmHouse", TileX = storage == "mini" ? 8 : 9,
            TileY = storage == "mini" ? 7 : 8, OwnerPlayerId = 1, ActorUseAuthorized = true, SupplyState = "available",
            Slots = [new() { SlotIndex = 0, QualifiedItemId = "(O)176", Stack = stack, Quality = 0,
                RuntimeType = "StardewValley.Object", MaximumStackSize = 999 }] }], AccessPoints = [new()
        { AccessPointId = "declared-native-fridge-access", NodeId = fridgeId, AccessKind = storage == "mini" ? "placed_chest" : "built_in_fridge",
            LocationId = "FarmHouse", TileX = storage == "mini" ? 8 : 9, TileY = storage == "mini" ? 7 : 8,
            IsFridge = true, IsPlayerChest = true, ActorUseAuthorized = true }] };
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(new { material_inventory_graph = new { status = "available", value = graph } });
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        const string producer = "declared-cooking-stock-producer";
        var ledger = new StrategyCommitmentLedger { LedgerId = "declared-cooking-stock", SaveId = snapshot.SaveId.Value, PlayerId = "1",
            SourceStateHash = snapshot.StateHash, MaterialReservations = [new()
            { ReservationId = CropSaleStockReservationContract.ReservationId(producer, 0), SourceDecisionId = producer,
                GoalId = "earn_money", OwnerPlayerId = 1, NodeId = seasoningStock || storage == "player" ? "player:1" : fridgeId,
                SlotIndex = seasoningStock ? 1 : 0, QualifiedItemId = seasoningStock ? "(O)917" : "(O)176", Quantity = 1,
                Status = StrategyCommitmentStatuses.Active,
                Purpose = CropSaleStockReservationContract.TransferablePurpose("declared-crop", "declared-wave",
                    MoneyRevenueCropRetentionAccount.InventoryIdentity(JsonSerializer.SerializeToElement(new
                        { qualified_item_id = seasoningStock ? "(O)917" : "(O)176", quality = 0 })), 0) }] };
        Assert.True(CropSaleStockReservationContract.TryRead(ledger.MaterialReservations[0], out _));
        return (snapshot, ledger);
    }
}
