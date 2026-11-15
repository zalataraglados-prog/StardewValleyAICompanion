using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed record CropTeacherSaleStockObservation(MoneyRevenueCropRetainedVariant[] Preserved,
    MoneyRevenueCropRetainedVariant[] Stored);

// A read-only view of original claim positions, not another ledger head or mutation service.
internal sealed class CropTeacherTransferredSaleStock(string commitmentId, string waveKey, string itemId)
{
    private StrategyCommitmentLedger? ledger;
    private readonly Dictionary<string, int> qualities = new(StringComparer.Ordinal);

    internal void Observe(MoneyRevenueCropRetentionAccount account, SnapshotEnvelope before, SnapshotEnvelope after, bool ownedHarvest)
    {
        if (ledger is null) account.Observe(before, after, ownedHarvest);
        else account.ObserveTransferredStock(before, after, ownedHarvest, Read(before, ledger), Read(after, ledger));
    }

    internal int ObserveShared(MoneyRevenueCropRetentionAccount account, MoneyRevenueCropNativeLifecycleTransitionRequest request)
        => ObserveSharedCore(account, request, inventoryEffects: false);

    internal int ObserveSharedInventoryEffects(MoneyRevenueCropRetentionAccount account, MoneyRevenueCropNativeLifecycleTransitionRequest request)
        => ObserveSharedCore(account, request, inventoryEffects: true);

    private int ObserveSharedCore(MoneyRevenueCropRetentionAccount account, MoneyRevenueCropNativeLifecycleTransitionRequest request,
        bool inventoryEffects)
    {
        var scope = inventoryEffects ? MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithSharedInventoryEffects(request) :
            MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithTransferredSaleStock(request);
        Require(scope.TransferSettlement is not { Preparation: null },
            "Transferred sale stock requires its original preparation and settlement.");
        var native = scope.NativeTransition;
        Require(scope.SourceLedger.SourceStateHash == native.Before.StateHash,
            "Transferred sale stock has another original source.");
        if (ledger is not null) _ = Read(native.Before, ledger);
        ledger = scope.SourceLedger;
        foreach (var frame in native.Actions)
        {
            var stockBefore = Read(frame.Before, ledger);
            if (frame.Item.OptionId == "executor.transfer_material")
            {
                var transfer = scope.PreparedTransfer;
                Require(scope.TransferSettlement is not null && transfer is not null,
                    "Transferred sale stock has no original settled position projection.");
                var beforeReadable = MaterialInventoryGraphReader.TryRead(frame.Before, out var beforeGraph);
                var afterReadable = MaterialInventoryGraphReader.TryRead(frame.After, out var graph);
                Require(beforeReadable && afterReadable, "Transferred sale stock native frame graphs are unavailable.");
                var physical = new MaterialTransferProjector().Project(transfer!.BeforeInventoryGraph, transfer.Intent);
                Require(MaterialTransferProjector.VerifyNativeBeforeSlots(transfer.BeforeInventoryGraph, beforeGraph!, physical).Length == 0 &&
                    MaterialTransferProjector.VerifyNativeSlots(transfer.BeforeInventoryGraph, graph!, physical).Length == 0,
                    "Transferred sale stock positions changed outside their original native transfer frame.");
                // The original service already replays moved AND passive claim reindexing.
                ledger = scope.SettledLedger;
            }
            else if (frame.Item.OptionId == "executor.cook_recipe" && scope.ConsumptionSettlement is not null)
            {
                // The shared source verifier validates the prepared one-action cook and its physical before/after.
                ledger = scope.SettledLedger;
            }
            account.ObserveTransferredStock(frame.Before, frame.After, false, stockBefore, Read(frame.After, ledger));
        }
        scope.ValidateInputsUnchanged();
        if (scope.CompletedLedger is { } completed)
        {
            _ = Read(native.After, completed);
            ledger = completed;
        }
        return native.Actions.Length;
    }

    internal CropTeacherSaleStockObservation Read(SnapshotEnvelope source, StrategyCommitmentLedger current)
    {
        var readable = MaterialInventoryGraphReader.TryRead(source, out var graph);
        Require(source.SaveId.Status == FieldStatus.Available && source.PlayerId.Status == FieldStatus.Available &&
            source.SaveId.Value == current.SaveId && source.PlayerId.Value == current.PlayerId &&
            source.StateHash == SnapshotHash.ComputeStateHash(source.State) &&
            readable, "Transferred sale-stock material source is unavailable.");
        Require(graph!.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture) == current.PlayerId &&
            new MaterialSupplyProjection().Project(graph, current.MaterialReservations).Status == "available",
            "Transferred sale-stock original reservation projection is unavailable.");
        var claims = current.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
            row.OwnerPlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture) == current.PlayerId &&
            row.QualifiedItemId == itemId && CropSaleStockReservationContract.TryRead(row, out var purpose) &&
            purpose!.CommitmentId == commitmentId && purpose.OutputRetentionKey == waveKey).ToArray();
        var playerNode = "player:" + current.PlayerId;
        var player = source.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray().ToArray();
        var rows = claims.OrderBy(claim => claim.NodeId == playerNode ? 0 : 1).Select(claim =>
        {
            CropSaleStockReservationContract.TryRead(claim, out var purpose);
            var slot = graph.InventoryNodes.Single(node => node.NodeId == claim.NodeId).Slots.Single(row => row.SlotIndex == claim.SlotIndex);
            var key = purpose!.InventoryIdentitySha256;
            if (claim.NodeId == playerNode)
            {
                var row = player.SingleOrDefault(row => row.GetProperty("slot_index").GetInt32() == claim.SlotIndex);
                Require(row.ValueKind == JsonValueKind.Object && row.GetProperty("stack").GetInt32() == slot.Stack &&
                    row.GetProperty("quality").GetInt32() == slot.Quality &&
                    MoneyRevenueCropRetentionAccount.InventoryIdentity(row) == key,
                    "Transferred sale stock changed its original full player variant.");
                Require(!qualities.TryGetValue(key, out var oldQuality) || oldQuality == slot.Quality,
                    "Transferred sale stock changed its original variant quality.");
                qualities[key] = slot.Quality;
            }
            else Require(CropSaleStockReservationContract.IsTransferable(purpose) &&
                claim.NodeId.StartsWith("chest:", StringComparison.Ordinal) &&
                qualities.TryGetValue(key, out var quality) && quality == slot.Quality,
                "Stored sale stock lacks its observed original player variant/quality.");
            return (Claim: claim, Key: key);
        }).ToArray();
        MoneyRevenueCropRetainedVariant[] Sum(bool storedOnly) => rows.Where(row => !storedOnly || row.Claim.NodeId != playerNode)
            .GroupBy(row => row.Key, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new MoneyRevenueCropRetainedVariant(group.Key, checked(group.Sum(row => row.Claim.Quantity)))).ToArray();
        return new(Sum(false), Sum(true));
    }

    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidDataException(reason); }
}
