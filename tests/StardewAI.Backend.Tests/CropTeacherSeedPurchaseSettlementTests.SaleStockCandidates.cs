using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(5, 0)]
    [InlineData(3, 2)]
    public void SaleStockIsExcludedUpstreamWhileUnreservedUnitsRemainAvailable(int reserved, int free)
    {
        using var f = new NativeVariantTerminalFixture();
        var (snapshot, ledger) = SaleStockCandidateSource(f, reserved);
        var sourceBytes = JsonSerializer.Serialize(snapshot, JsonOptions);
        var ledgerBytes = JsonSerializer.Serialize(ledger, JsonOptions);
        var evaluator = new CandidateOptionAvailabilityEvaluator();
        var sale = Assert.Single(evaluator.Evaluate(snapshot, ["economy.sell_items"], commitmentLedger: ledger).Options);
        var normal = Assert.Single(sale.EconomicCandidates.Where(row => row.SlotIndex == f.NormalSlot));
        Assert.Equal(free, normal.Quantity); Assert.Equal(normal.UnitPrice * free, normal.TotalValue);
        Assert.Equal(free > 0, normal.Available);
        if (free == 0) Assert.Contains("inventory_item_reserved_for_crop_sale", normal.BlockReasons);
        else Assert.DoesNotContain("inventory_item_reserved_for_crop_sale", normal.BlockReasons);
        var silver = Assert.Single(sale.EconomicCandidates.Where(row => row.SlotIndex == f.SilverSlot));
        Assert.True(silver.Available); Assert.Equal(1, silver.Quantity);
        var old = Assert.Single(evaluator.Evaluate(snapshot, ["economy.sell_items"]).Options);
        Assert.Equal(5, Assert.Single(old.EconomicCandidates.Where(row => row.SlotIndex == f.NormalSlot)).Quantity);
        Assert.Equal(sourceBytes, JsonSerializer.Serialize(snapshot, JsonOptions));
        Assert.Equal(ledgerBytes, JsonSerializer.Serialize(ledger, JsonOptions));

        // The public shipping path consumes one physical unit, never the whole stack.
        ChangeState(snapshot, "menus", node => node["active_menu"] = JsonSerializer.SerializeToNode(new
            { status = "available", value = new { is_open = false, type = "none" } }));
        ledger.SourceStateHash = snapshot.StateHash;
        var shipping = Assert.Single(evaluator.Evaluate(snapshot, ["economy.ship_items"], commitmentLedger: ledger).Options);
        Assert.True(shipping.EventCandidates.Length > 0, "missing=" + string.Join(",", shipping.MissingStateFactors) +
            "; blocked=" + string.Join(",", shipping.BlockingReasons));
        var shipNormal = Assert.Single(shipping.EventCandidates.Where(row => row.SlotIndex == f.NormalSlot));
        if (free == 0) Assert.Contains("inventory_item_reserved_for_crop_sale", shipNormal.BlockReasons);
        else Assert.DoesNotContain("inventory_item_reserved_for_crop_sale", shipNormal.BlockReasons);
        Assert.Contains("available_stack=" + free, shipNormal.ExpectedEffect);
    }

    [Fact]
    public void SaleStockCannotUseAStaleMaterialGraphOrCarryTheOriginalFullStackContinuation()
    {
        using var f = new NativeVariantTerminalFixture();
        var (snapshot, ledger) = SaleStockCandidateSource(f, 3);
        ledger.SourceStateHash = new string('0', 64);
        var evaluator = new CandidateOptionAvailabilityEvaluator();
        var option = Assert.Single(evaluator.Evaluate(snapshot, ["economy.sell_items"], commitmentLedger: ledger).Options);
        Assert.All(option.EconomicCandidates, row =>
        { Assert.False(row.Available); Assert.Contains("crop_sale_stock_material_supply_unavailable", row.BlockReasons); });
        ledger.SourceStateHash = snapshot.StateHash;
        var locked = new OptionAvailabilityCandidate
        {
            OptionId = "economy.sell_items", Parameters = [new SmallModelActionParameter
                { Name = "continuation.quantity", Value = "5" }]
        };
        var refreshed = Assert.Single(evaluator.Evaluate(snapshot, [locked], commitmentLedger: ledger).Options);
        Assert.DoesNotContain(refreshed.EconomicCandidates, row => row.SlotIndex == f.NormalSlot);
    }

    [Fact]
    public void SaleStockPreviewCarriesOnlyTheSameFreeQuantityBeforeRoutingToTheShop()
    {
        using var f = new NativeVariantTerminalFixture();
        var (snapshot, ledger) = SaleStockCandidateSource(f, 3);
        ChangeState(snapshot, "locations", node => node["shops"] = JsonSerializer.SerializeToNode(SaleStockCandidateField(snapshot,
            new { shops = new[] { new { shop_id = "SeedShop", sale_preview = new { executor_sale_preview_enabled = true,
                default_sell_percentage = 1d, executor_block_reasons = Array.Empty<string>(), tag_groups_to_sell = new[] { new[] { "declared_crop" } } } } } })));
        ledger.SourceStateHash = snapshot.StateHash;
        var type = typeof(CandidateOptionAvailabilityEvaluator);
        var budget = type.GetMethod("ReadSaleStockBudget", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [snapshot, ledger]);
        var rows = (EconomicCandidate[])type.GetMethod("SellCandidatesFromShopPreview", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [snapshot, budget])!;
        var normal = Assert.Single(rows.Where(row => row.SlotIndex == f.NormalSlot));
        Assert.True(normal.Available, string.Join(",", normal.BlockReasons));
        Assert.Equal(2, normal.Quantity); Assert.Equal(normal.UnitPrice * 2, normal.TotalValue);
    }

    private static (SnapshotEnvelope, StrategyCommitmentLedger) SaleStockCandidateSource(NativeVariantTerminalFixture f, int reserved)
    {
        // Declared candidate-budget fixture; no native outcome or whole-production claim.
        var snapshot = NativeVariantTerminalFixture.Copy(f.Shared.Source.Before);
        ChangeState(snapshot, "player", node =>
        {
            foreach (var row in node["inventory"]!["value"]!.AsArray().OfType<JsonObject>())
            { row["category"] = -75; row["can_be_shipped"] = true; row["context_tags"] = new JsonArray("declared_crop"); }
        });
        var inventory = JsonNode.Parse(snapshot.State["player"].GetRawText())!["inventory"]!["value"]!;
        ChangeState(snapshot, "farm", node => node["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"] = inventory.DeepClone());
        snapshot.State["menus"] = JsonSerializer.SerializeToElement(new
        {
            active_menu = new { status = "available", value = new { is_open = true, type = "ShopMenu" } },
            sell_context = new { status = "available", value = new { shop_id = "SeedShop", currency = 0, read_only = false,
                safety_timer = 0, held_item_present = false, storage_shop = false, sell_percentage = 1d,
                custom_on_sell_present = false, categories_to_sell = new[] { -75 }, tag_groups_to_sell = Array.Empty<string[]>() } }
        });
        ChangeState(snapshot, "locations", node =>
        {
            node["route_graph"] = JsonSerializer.SerializeToNode(SaleStockCandidateField(snapshot,
                new { nodes = Array.Empty<object>(), edges = Array.Empty<object>() }));
            node["route_connectors"] = JsonSerializer.SerializeToNode(SaleStockCandidateField(snapshot, Array.Empty<object>()));
        });
        snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(new
            { shipping_collection = SaleStockCandidateField(snapshot, Array.Empty<object>()) });
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(JsonSerializer.Serialize(f.Shared.Source.Ledger, JsonOptions), JsonOptions)!;
        ledger.SourceStateHash = snapshot.StateHash;
        var item = snapshot.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray()
            .Single(row => row.GetProperty("slot_index").GetInt32() == f.NormalSlot);
        const string producer = "declared.candidate-stock";
        ledger.MaterialReservations = [new()
        {
            ReservationId = CropSaleStockReservationContract.ReservationId(producer, f.NormalSlot), SourceDecisionId = producer,
            GoalId = "earn_money", NodeId = "player:" + ledger.PlayerId, OwnerPlayerId = long.Parse(ledger.PlayerId),
            SlotIndex = f.NormalSlot, QualifiedItemId = f.Target.QualifiedItemId, Quantity = reserved,
            Status = StrategyCommitmentStatuses.Active,
            Purpose = CropSaleStockReservationContract.Purpose(Id, "declared.candidate-wave", MoneyRevenueCropRetentionAccount.InventoryIdentity(item))
        }];
        return (snapshot, ledger);
    }

    private static object SaleStockCandidateField<T>(SnapshotEnvelope snapshot, T value) => new
    {
        status = "available", confidence = 1d, read_at_tick = snapshot.GameTick,
        source = new { kind = "test", path = "declared.sale-stock-candidate-budget" }, adapter = "test", value
    };
}
