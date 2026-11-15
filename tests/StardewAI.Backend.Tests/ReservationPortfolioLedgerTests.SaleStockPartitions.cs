using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaleStockPartitionsKeepOriginalCreditThroughAtomicPreparationCompilationAndSettlement(bool deposit)
    {
        var (before, ledger, f) = PartitionStockBasis(deposit);
        var original = JsonSerializer.Serialize(ledger);
        var parameters = MaterialTransferIntentBinder.ToParameters(f.PreparationRequest.MaterialTransferPreparation!.Intent);
        var preview = CropSaleStockTransferPreparation.Create(before, ledger, parameters, out var reasons);
        Assert.NotNull(preview); Assert.Empty(reasons); Assert.Equal(original, JsonSerializer.Serialize(ledger));
        Assert.Null(ledger.PreparedMaterialTransfer);
        Assert.Equal(new[] { 1, 1 }, preview!.Request.MaterialClaims.Select(row => row.Quantity));
        Assert.Equal(new[] { 1, 1 }, preview.Request.MaterialTransferPreparation!.MaterialRelocations.Select(row => row.Quantity));
        var root = ledger.MaterialReservations.Single(row => CropSaleStockReservationContract.TryRead(row, out _));
        Assert.Equal(root.ReservationId, preview.Request.MaterialClaims[0].ReservationId);
        var commit = f.Service.Commit(ledger, before, preview.Request, "partition-prepared");
        Assert.True(commit.Accepted, string.Join(",", commit.Errors));
        Assert.Equal(ledger.Revision + 1, commit.Ledger!.Revision);
        var prepared = commit.Ledger.PreparedMaterialTransfer!;
        var claims = commit.Ledger.MaterialReservations.Where(row => CropSaleStockReservationContract.TryRead(row, out _)).ToArray();
        Assert.Equal(2, claims.Length); Assert.Equal(root.Quantity, claims.Sum(row => row.Quantity));
        var child = claims.Single(row => row.ReservationId != root.ReservationId);
        Assert.True(CropSaleStockReservationContract.TryRead(child, out var purpose));
        Assert.Equal(CropSaleStockReservationContract.PartitionedSchema, purpose!.SchemaVersion);
        Assert.Equal(root.ReservationId, purpose.PartitionParentId);
        Assert.True(CropSaleStockReservationContract.TryRead(root, out var originalPurpose));
        Assert.Equal(originalPurpose!.InventoryIdentitySha256, purpose.InventoryIdentitySha256);
        Assert.Equal(originalPurpose.OutputRetentionKey, purpose.OutputRetentionKey);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(before,
            [new OptionAvailabilityCandidate { OptionId = "inventory.transfer_item",
                Parameters = CropSaleStockMaterialTransferBinding.ToParameters(commit.Ledger) }], commitmentLedger: commit.Ledger);
        Assert.True(Assert.Single(availability.Options).Available, string.Join(",", availability.Options.SelectMany(row => row.BlockingReasons)));
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, "earn_money");
        var plan = new DailyPlanCompiler().Compile(ranked, before.StateHash, goalId: "earn_money", availableMinutes: 600, energyBudget: 270);
        var queue = new ActionQueueCompiler().Compile(plan, before, commit.Ledger);
        var transfer = Assert.Single(queue.Items.Where(row => row.OptionId == "executor.transfer_material"));
        Assert.True(CropSaleStockMaterialTransferBinding.IsBound(transfer, commit.Ledger));
        Assert.True(new ActionQueueDispatchReadinessService().Evaluate(queue, transfer, commit.Ledger, before.StateHash).Ready);
        var after = ProjectPartitionAfter(before, prepared);
        var settled = SettleStock(f, commit.Ledger, after);
        Assert.Null(settled.PreparedMaterialTransfer);
        var moved = settled.MaterialReservations.Where(row => CropSaleStockReservationContract.TryRead(row, out _)).ToArray();
        Assert.Equal(2, moved.Sum(row => row.Quantity)); Assert.Equal(2, moved.Select(row => row.SlotIndex).Distinct().Count());
        foreach (var other in ledger.MaterialReservations.Where(row => !CropSaleStockReservationContract.TryRead(row, out _)))
        {
            var current = settled.MaterialReservations.Single(row => row.ReservationId == other.ReservationId);
            Assert.Equal(other.Quantity, current.Quantity); Assert.Equal(other.SourceDecisionId, current.SourceDecisionId);
        }
        if (!deposit)
            Assert.Equal(2, CropTeacherSaleStockInventory.ReadVerified(after, settled, "declared-transfer-crop", "declared-wave", "(O)388").Sum(row => row.Quantity));
        else
        {
            var start = StockFrame(before, 0, 0); start.GameTick = before.GameTick - 1;
            var account = StockAccount(start); StockObserve(account, start, before, true);
            var stock = StockTrace();
            StockObserveTransfer(account, before, after, StockRead(stock, before, ledger), StockRead(stock, after, settled));
            Assert.Equal(2, StockQuantity(account));
        }
        Assert.Equal(original, JsonSerializer.Serialize(ledger));
    }

    [Fact]
    public void SaleStockPartitionsRebindMechanicalAllocationWithoutChangingOriginalSelectionOwnership()
    {
        var (before, ledger, f) = PartitionStockBasis(false);
        var parameters = MaterialTransferIntentBinder.ToParameters(f.PreparationRequest.MaterialTransferPreparation!.Intent);
        var old = CropSaleStockTransferPreparation.Create(before, ledger, parameters, out _)!;
        var next = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before))!; next.GameTick++;
        MaterialInventoryGraphReader.TryRead(next, out var graph);
        graph!.InventoryNodes.Single(row => row.NodeId == "player:123").Slots[0].Stack = 997;
        SetPartitionPlayer(next, graph); StockGraph(next, graph);
        var rebound = f.Service.Commit(ledger, next, new ReservationPortfolioCommitRequest
        { StateHash = next.StateHash, ExpectedLedgerRevision = ledger.Revision, PortfolioId = "partition-rebound",
            SourceDecisionId = "partition-rebound", GoalId = "earn_money" }, "rebound");
        Assert.True(rebound.Accepted, string.Join(",", rebound.Errors));
        var current = CropSaleStockTransferPreparation.Create(next, rebound.Ledger!, parameters, out var reasons);
        Assert.NotNull(current); Assert.Empty(reasons);
        Assert.Empty(current!.Request.MaterialClaims); // Both units now fit in the existing destination stack.
        var method = typeof(CropTeacherManagedSeedOwner).GetMethod("SelectedTransferIdentity", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.True(JsonNode.DeepEquals((JsonNode)method.Invoke(null, [old.Request, ledger])!,
            (JsonNode)method.Invoke(null, [current.Request, rebound.Ledger!])!));
    }

    [Theory]
    [InlineData("legacy_child")]
    [InlineData("parent")]
    [InlineData("step")]
    [InlineData("index")]
    [InlineData("id")]
    public void SaleStockPartitionsDoNotRelaxLegacyIdentityOrAcceptUnboundChildren(string fault)
    {
        var (_, ledger, _) = PartitionStockBasis(true);
        var parent = ledger.MaterialReservations.Single();
        var child = JsonSerializer.Deserialize<MaterialReservation>(JsonSerializer.Serialize(parent))!;
        var step = new string('a', 64);
        child.Purpose = CropSaleStockReservationContract.PartitionedPurpose(parent, step, 1);
        child.ReservationId = CropSaleStockReservationContract.PartitionReservationId(parent.ReservationId, step, 1);
        Assert.True(CropSaleStockReservationContract.TryRead(child, out _));
        if (fault == "legacy_child") child.Purpose = parent.Purpose;
        if (fault == "id") child.ReservationId += "unbound";
        if (fault is "parent" or "step" or "index")
        {
            var purpose = JsonNode.Parse(child.Purpose)!;
            purpose[fault == "parent" ? "partition_parent_id" : fault == "step" ? "partition_step_sha256" : "partition_index"] =
                fault == "index" ? JsonValue.Create(0) : JsonValue.Create("unbound");
            child.Purpose = purpose.ToJsonString();
        }
        Assert.False(CropSaleStockReservationContract.TryRead(child, out _));
        Assert.DoesNotContain("partition_", parent.Purpose);
    }

    [Fact]
    public void SaleStockPartitionsKeepNestedParentIdentityAndRejectPreviouslyUsedChildIds()
    {
        var (before, ledger, f) = PartitionStockBasis(true);
        var parameters = MaterialTransferIntentBinder.ToParameters(f.PreparationRequest.MaterialTransferPreparation!.Intent);
        var preview = CropSaleStockTransferPreparation.Create(before, ledger, parameters, out _)!;
        var child = preview.PreviewLedger.MaterialReservations.Single(row =>
            CropSaleStockReservationContract.TryRead(row, out var purpose) && purpose!.SchemaVersion == CropSaleStockReservationContract.PartitionedSchema);
        var nested = JsonSerializer.Deserialize<MaterialReservation>(JsonSerializer.Serialize(child))!;
        var step = new string('b', 64);
        nested.ReservationId = CropSaleStockReservationContract.PartitionReservationId(child.ReservationId, step, 1);
        nested.Purpose = CropSaleStockReservationContract.PartitionedPurpose(child, step, 1);
        Assert.True(CropSaleStockReservationContract.TryRead(nested, out var purpose));
        Assert.Equal(child.ReservationId, purpose!.PartitionParentId);
        Assert.True(string.CompareOrdinal(child.ReservationId, nested.ReservationId) < 0);
        var current = JsonSerializer.Deserialize<StrategyCommitmentLedger>(JsonSerializer.Serialize(ledger))!;
        current.History = current.History.Append(new() { CommitmentId = child.ReservationId }).ToArray();
        Assert.Null(CropSaleStockTransferPreparation.Create(before, current, parameters, out var reasons));
        Assert.Contains("crop_sale_stock_prepare_partition_identity_exists", reasons);
        Assert.Null(current.PreparedMaterialTransfer); Assert.Equal(ledger.MaterialReservations.Length, current.MaterialReservations.Length);
    }

    private static (SnapshotEnvelope Before, StrategyCommitmentLedger Ledger, PreparedMaterialTransferTestFixture Fixture)
        PartitionStockBasis(bool deposit)
    {
        var (before, ledger, f) = SaleStockPreparationBasis(deposit);
        MaterialInventoryGraphReader.TryRead(before, out var graph);
        var target = graph!.InventoryNodes.Single(row => row.NodeId == f.PreparationRequest.MaterialTransferPreparation!.Intent.DestinationNodeId);
        if (deposit) target.Slots.Single(row => row.SlotIndex == 0).Stack = 998;
        else target.Slots = [new() { SlotIndex = 0, QualifiedItemId = "(O)388", Stack = 998,
            Quality = 0, MaximumStackSize = 999, RuntimeType = "StardewValley.Object" }];
        SetPartitionPlayer(before, graph); StockGraph(before, graph);
        var updated = f.Service.Commit(ledger, before, new ReservationPortfolioCommitRequest
        { StateHash = before.StateHash, ExpectedLedgerRevision = ledger.Revision, PortfolioId = "partition-source",
            SourceDecisionId = "partition-source", GoalId = "earn_money" }, "source");
        Assert.True(updated.Accepted, string.Join(",", updated.Errors));
        return (before, updated.Ledger!, f);
    }

    private static SnapshotEnvelope ProjectPartitionAfter(SnapshotEnvelope before, PreparedMaterialTransfer prepared)
    {
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before))!; after.GameTick++;
        MaterialInventoryGraphReader.TryRead(after, out var graph);
        var projection = new MaterialTransferProjector().Project(prepared.BeforeInventoryGraph, prepared.Intent);
        foreach (var group in projection.NativeSlotTransitions!.GroupBy(row => row.NodeId))
        {
            var node = graph!.InventoryNodes.Single(row => row.NodeId == group.Key);
            var old = node.Slots;
            node.Slots = group.Where(row => row.AfterSlotIndex.HasValue).Select(row =>
            {
                var slot = row.BeforeSlotIndex.HasValue ? old.Single(slot => slot.SlotIndex == row.BeforeSlotIndex) :
                    prepared.BeforeInventoryGraph.InventoryNodes.Single(value => value.NodeId == prepared.Intent.SourceNodeId)
                        .Slots.Single(value => value.SlotIndex == prepared.Intent.SourceSlotIndex);
                var copy = JsonSerializer.Deserialize<MaterialInventorySlot>(JsonSerializer.Serialize(slot))!;
                copy.SlotIndex = row.AfterSlotIndex!.Value; copy.Stack = row.StackAfter; return copy;
            }).ToArray();
        }
        SetPartitionPlayer(after, graph!); StockGraph(after, graph!); return after;
    }

    private static void SetPartitionPlayer(SnapshotEnvelope snapshot, MaterialInventoryGraph graph) =>
        AddPlayerInventory(snapshot, graph.InventoryNodes.Single(row => row.NodeId == "player:123").Slots.Select(slot =>
            JsonSerializer.SerializeToElement(new { slot_index = slot.SlotIndex, qualified_item_id = slot.QualifiedItemId,
                item_id = slot.QualifiedItemId.Substring(3), stack = slot.Stack, quality = slot.Quality, is_empty = false,
                can_be_shipped = true, sell_to_store_price = 2 })).ToArray());
}
