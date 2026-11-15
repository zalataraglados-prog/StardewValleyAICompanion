using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Fact]
    public void TransferredRetentionKeepsOwnedCreditAcrossStorageFreeSpendForeignGainAndOriginalWithdrawal()
    {
        var f = SaleStockDepositFixture();
        var settled = SettleStock(f, f.PreparedLedger, f.After);
        var start = StockFrame(f.Before, 0, 0);
        start.GameTick = f.Before.GameTick - 1;
        var account = StockAccount(start); StockObserve(account, start, f.Before, true);
        var stock = StockTrace(); var before = StockRead(stock, f.Before, f.BaseLedger);
        var stored = StockRead(stock, f.After, settled);
        StockObserveTransfer(account, f.Before, f.After, before, stored);
        Assert.Equal(2, StockQuantity(account));
        Assert.Empty(CropTeacherSaleStockInventory.ReadVerified(f.After, settled, "declared-crop", "declared-wave", "(O)388"));
        var freeSpent = StockFrame(f.After, 0, -1);
        StockObserveTransfer(account, f.After, freeSpent, stored, StockRead(stock, freeSpent, settled));
        var foreign = StockFrame(freeSpent, 5, 0);
        StockObserveTransfer(account, freeSpent, foreign, StockRead(stock, freeSpent, settled), StockRead(stock, foreign, settled));
        Assert.Equal(2, StockQuantity(account));
        var consumed = StockFrame(foreign, 0, 0);
        StockObserveTransfer(account, foreign, consumed, StockRead(stock, foreign, settled), StockRead(stock, consumed, settled));
        Assert.Equal(2, StockQuantity(account));

        // Original rebind, preparation and settlement services, not hand-written relocation semantics.
        var claim = Assert.Single(settled.MaterialReservations);
        var rebound = f.Service.Commit(settled, consumed, new ReservationPortfolioCommitRequest
        {
            StateHash = consumed.StateHash, ExpectedLedgerRevision = settled.Revision, PortfolioId = "stock-withdraw-source",
            SourceDecisionId = claim.SourceDecisionId, GoalId = claim.GoalId
        }, "rebound");
        Assert.True(rebound.Accepted, string.Join(",", rebound.Errors));
        MaterialInventoryGraphReader.TryRead(consumed, out var graph);
        var intent = new MaterialTransferIntent { SourceNodeId = claim.NodeId, SourceSlotIndex = claim.SlotIndex,
            DestinationNodeId = "player:123", QualifiedItemId = claim.QualifiedItemId, Quantity = 2,
            ExpectedSourceStack = graph!.InventoryNodes.Single(row => row.NodeId == claim.NodeId).Slots.Single(row => row.SlotIndex == claim.SlotIndex).Stack };
        var preview = CropSaleStockTransferPreparation.Create(consumed, rebound.Ledger!, MaterialTransferIntentBinder.ToParameters(intent), out var reasons);
        Assert.NotNull(preview); Assert.Empty(reasons);
        var prepared = f.Service.Commit(rebound.Ledger, consumed, preview!.Request, "withdraw-prepared");
        Assert.True(prepared.Accepted, string.Join(",", prepared.Errors));
        var withdrawn = StockFrame(consumed, 2, -2);
        var returned = SettleStock(f, prepared.Ledger!, withdrawn);
        StockObserveTransfer(account, consumed, withdrawn, StockRead(stock, consumed, rebound.Ledger!), StockRead(stock, withdrawn, returned));
        Assert.Equal(2, StockQuantity(account));
        Assert.Equal(claim.ReservationId, Assert.Single(CropTeacherSaleStockInventory.ReadVerified(withdrawn, returned,
            "declared-crop", "declared-wave", "(O)388")).ReservationId);
    }

    [Theory]
    [InlineData("quality")]
    [InlineData("quantity")]
    [InlineData("unauthorized")]
    [InlineData("withdraw_metadata")]
    [InlineData("unobserved_deposit")]
    public void TransferredRetentionRejectsMissingVariantsAndCannotBootstrapFromStoredStock(string fault)
    {
        var f = SaleStockDepositFixture(); var settled = SettleStock(f, f.PreparedLedger, f.After);
        var stock = StockTrace(); var before = StockRead(stock, f.Before, f.BaseLedger);
        var stored = StockRead(stock, f.After, settled);
        if (fault == "unobserved_deposit")
        {
            var start = StockFrame(f.Before, 0, 0);
            start.GameTick = f.Before.GameTick - 1;
            var account = StockAccount(start);
            StockObserve(account, start, f.Before, true);
            var next = StockFrame(f.After, 0, 0);
            AssertStockFailure(() => StockObserveTransfer(account, f.After, next, stored, StockRead(stock, next, settled)));
            return;
        }
        var changed = StockFrame(f.After, 0, 0);
        MaterialInventoryGraphReader.TryRead(changed, out var graph);
        var node = graph!.InventoryNodes.Single(row => row.NodeId == "chest:Farm:4,5");
        if (fault == "quality") node.Slots.Single(row => row.SlotIndex == 0).Quality = 4;
        if (fault == "quantity") node.Slots.Single(row => row.SlotIndex == 0).Stack = 1;
        if (fault == "unauthorized") node.ActorUseAuthorized = false;
        if (fault == "withdraw_metadata")
        {
            changed = StockFrame(f.Before, 2, 0);
            var player = JsonNode.Parse(changed.State["player"].GetRawText())!;
            player["inventory"]!["value"]![0]!["mod_data"] = new JsonObject { ["foreign"] = "yes" };
            changed.State["player"] = JsonSerializer.SerializeToElement(player);
            changed.StateHash = SnapshotHash.ComputeStateHash(changed.State);
            AssertStockFailure(() => StockRead(stock, changed, f.BaseLedger));
            return;
        }
        StockGraph(changed, graph);
        AssertStockFailure(() => StockRead(stock, changed, settled));
    }

    private static StrategyCommitmentLedger SettleStock(PreparedMaterialTransferTestFixture f, StrategyCommitmentLedger prepared,
        SnapshotEnvelope after)
    {
        var transfer = prepared.PreparedMaterialTransfer!;
        var result = f.Service.SettleSupportingTransition(prepared, after, new ReservationPortfolioSupportingTransitionSettlementRequest
        {
            StateHash = after.StateHash, ExpectedLedgerRevision = prepared.Revision, PortfolioId = transfer.PortfolioId,
            GoalId = transfer.GoalId, RouteSourceDecisionId = transfer.RouteSourceDecisionId,
            SupportingTransitionReceiptSha256 = new string('c', 64), PreparedMaterialTransferSha256 = transfer.BindingSha256,
            MaterialRelocations = transfer.MaterialRelocations, Reason = "declared_original_transfer_projection"
        }, "settled");
        Assert.True(result.Accepted, string.Join(",", result.Errors)); return result.Ledger!;
    }

    private static SnapshotEnvelope StockFrame(SnapshotEnvelope source, int playerCount, int chestDelta)
    {
        var result = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(source))!;
        result.GameTick++;
        MaterialInventoryGraphReader.TryRead(result, out var graph);
        var original = graph!.InventoryNodes.Single(row => row.NodeId == "chest:Farm:4,5").Slots.Single(row => row.SlotIndex == 0);
        var playerSlot = JsonSerializer.Deserialize<MaterialInventorySlot>(JsonSerializer.Serialize(original))!;
        playerSlot.SlotIndex = 0; playerSlot.Stack = playerCount;
        graph!.InventoryNodes.Single(row => row.NodeId == "player:123").Slots = playerCount == 0 ? [] :
            [playerSlot];
        graph.InventoryNodes.Single(row => row.NodeId == "chest:Farm:4,5").Slots.Single(row => row.SlotIndex == 0).Stack += chestDelta;
        AddPlayerInventory(result, playerCount == 0 ? [] : [JsonSerializer.SerializeToElement(new
            { slot_index = 0, qualified_item_id = "(O)388", item_id = "388", stack = playerCount, quality = 0,
                is_empty = false, can_be_shipped = true, sell_to_store_price = 2 })]);
        StockGraph(result, graph); return result;
    }

    private static void StockGraph(SnapshotEnvelope frame, MaterialInventoryGraph graph)
    {
        var farm = JsonNode.Parse(frame.State["farm"].GetRawText())!;
        farm["material_inventory_graph"]!["value"] = JsonSerializer.SerializeToNode(graph);
        frame.State["farm"] = JsonSerializer.SerializeToElement(farm); frame.StateHash = SnapshotHash.ComputeStateHash(frame.State);
    }
    private static object StockTrace() => Activator.CreateInstance(typeof(CropTeacherSaleStockInventory).Assembly
        .GetType("StardewAI.GoalConditionedBootstrap.CropTeacherTransferredSaleStock")!, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
        null, ["declared-crop", "declared-wave", "(O)388"], null)!;
    private static object StockRead(object trace, SnapshotEnvelope frame, StrategyCommitmentLedger ledger) =>
        trace.GetType().GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(trace, [frame, ledger])!;
    private static object StockAccount(SnapshotEnvelope frame) => Activator.CreateInstance(typeof(MoneyRevenueCropRetentionAccount),
        BindingFlags.NonPublic | BindingFlags.Instance, null, [frame, "(O)388"], null)!;
    private static void StockObserve(object account, SnapshotEnvelope before, SnapshotEnvelope after, bool owned) =>
        account.GetType().GetMethod("Observe", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(account, [before, after, owned]);
    private static void StockObserveTransfer(object account, SnapshotEnvelope before, SnapshotEnvelope after, object stockBefore, object stockAfter) =>
        account.GetType().GetMethod("ObserveTransferredStock", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(account, [before, after, false, stockBefore, stockAfter]);
    private static int StockQuantity(object account) => (int)account.GetType().GetProperty("Quantity", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(account)!;
    private static void AssertStockFailure(Action action) => Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(action).InnerException);
}
