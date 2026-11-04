using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Strategy;

public static class MuseumOutputCustodyTransferSettlementRequestBuilder
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public static string[] Verify(ActionQueueEnvelope queue, ActionQueueItem item, QueueExecutionStepReceipt receipt,
        SnapshotEnvelope before, SnapshotEnvelope after, StrategyCommitmentLedger ledger,
        ReservationPortfolioSupportingTransitionSettlementResult actual)
    {
        var built = Build(queue, item, receipt, before, after, ledger);
        if (built.Request is null) return built.Errors;
        if (!actual.Accepted || actual.Ledger is null)
            return new[] { "museum_custody_transfer_settlement_not_accepted" };
        var expected = new ReservationPortfolioLedgerService().SettleSupportingTransition(ledger, after, built.Request, actual.Ledger.UpdatedAt);
        return expected.Accepted && JsonSerializer.Serialize(expected) == JsonSerializer.Serialize(actual)
            ? Array.Empty<string>() : new[] { "museum_custody_transfer_settlement_replay_drifted" };
    }

    public static MuseumOutputCustodyTransferSettlementBuildResult Build(ActionQueueEnvelope queue, ActionQueueItem item,
        QueueExecutionStepReceipt receipt, SnapshotEnvelope before, SnapshotEnvelope after, StrategyCommitmentLedger ledger)
    {
        var errors = new List<string>();
        if (queue.Items.Count(row => row.QueueItemId == item.QueueItemId && MatchesReboundItem(row, item)) != 1 ||
            !MuseumOutputCustodyTransferBinding.IsBound(item, ledger))
            errors.Add("museum_custody_transfer_settlement_authorization_invalid");
        if (!MatchesRecordedItem(item, receipt.EffectiveQueueItem))
            errors.Add("museum_custody_transfer_settlement_recorded_command_mismatch");
        errors.AddRange(new ActionQueueDispatchReadinessService().Evaluate(queue, item, ledger, before.StateHash).BlockingReasons);
        if (receipt.QueueId != queue.QueueId || receipt.QueueItemId != item.QueueItemId || receipt.OptionId != item.OptionId ||
            receipt.Status != "applied" || receipt.PrimitiveVerificationStatus != "verified" || !receipt.AfterSnapshotFresh ||
            receipt.SourceStateHash != before.StateHash || receipt.AfterStateHash != after.StateHash ||
            before.StateHash == after.StateHash || after.GameTick < before.GameTick)
            errors.Add("museum_custody_transfer_settlement_native_receipt_invalid");
        foreach (var snapshot in new[] { before, after })
            if (snapshot.SaveId.Status != FieldStatus.Available || snapshot.SaveId.Value != ledger.SaveId ||
                snapshot.PlayerId.Status != FieldStatus.Available || snapshot.PlayerId.Value != ledger.PlayerId ||
                string.IsNullOrWhiteSpace(snapshot.StateHash))
                errors.Add("museum_custody_transfer_settlement_snapshot_identity_invalid");
        var prepared = ledger.PreparedMaterialTransfer;
        if (prepared is not null)
        {
            var physical = new MaterialTransferProjector().Project(prepared.BeforeInventoryGraph, prepared.Intent);
            if (before.GameTick < prepared.SourceGameTick || !MaterialInventoryGraphReader.TryRead(before, out var beforeGraph))
                errors.Add("museum_custody_transfer_settlement_before_graph_unavailable");
            else errors.AddRange(MaterialTransferProjector.VerifyNativeBeforeSlots(prepared.BeforeInventoryGraph, beforeGraph!, physical)
                .Select(reason => "before:" + reason));
            if (!MaterialInventoryGraphReader.TryRead(after, out var afterGraph))
                errors.Add("museum_custody_transfer_settlement_after_graph_unavailable");
            else errors.AddRange(MaterialTransferProjector.VerifyNativeSlots(prepared.BeforeInventoryGraph, afterGraph!, physical));
        }
        if (errors.Count > 0 || prepared is null)
            return new(null, errors.Distinct(StringComparer.Ordinal).ToArray());
        var receiptHash = MuseumOutputCustodyTransferRequestBuilder.Hash(new
        {
            Kind = MuseumOutputCustodyTransferBinding.Kind, prepared.BindingSha256,
            queue.QueueId, item.QueueItemId, item.NormalizedCommand,
            receipt.OptionId, receipt.Status, receipt.PrimitiveVerificationStatus, receipt.AfterSnapshotFresh,
            BeforeStateHash = before.StateHash, AfterStateHash = after.StateHash
        });
        return new(new ReservationPortfolioSupportingTransitionSettlementRequest
        {
            StateHash = after.StateHash, ExpectedLedgerRevision = ledger.Revision,
            PortfolioId = prepared.PortfolioId, GoalId = prepared.GoalId, RouteSourceDecisionId = prepared.RouteSourceDecisionId,
            PreparedMaterialTransferSha256 = prepared.BindingSha256, SupportingTransitionReceiptSha256 = receiptHash,
            MaterialRelocations = prepared.MaterialRelocations.Select(row => new ReservationPortfolioMaterialRelocation
            {
                MaterialReservationId = row.MaterialReservationId, SourceNodeId = row.SourceNodeId, SourceSlotIndex = row.SourceSlotIndex,
                DestinationNodeId = row.DestinationNodeId, DestinationSlotIndex = row.DestinationSlotIndex,
                QualifiedItemId = row.QualifiedItemId, Quantity = row.Quantity
            }).ToArray(),
            Reason = "verified_output_custody_transfer"
        }, Array.Empty<string>());
    }

    private static bool MatchesReboundItem(ActionQueueItem original, ActionQueueItem actual)
    {
        // Live may rebind only the state hash after normal movement; all other queue authority stays pinned.
        var expected = JsonSerializer.Deserialize<ActionQueueItem>(JsonSerializer.Serialize(original))!;
        expected.NormalizedCommand.StateHash = actual.NormalizedCommand.StateHash;
        return JsonSerializer.Serialize(expected) == JsonSerializer.Serialize(actual);
    }

    private static bool MatchesRecordedItem(ActionQueueItem item, JsonElement? recorded)
    {
        if (recorded?.ValueKind != JsonValueKind.Object) return false;
        try
        {
            var actual = recorded.Value.Deserialize<ActionQueueItem>(WebJson);
            return actual is not null && JsonSerializer.Serialize(item) == JsonSerializer.Serialize(actual);
        }
        catch (JsonException) { return false; }
        catch (NotSupportedException) { return false; }
    }
}

public sealed record MuseumOutputCustodyTransferSettlementBuildResult(
    ReservationPortfolioSupportingTransitionSettlementRequest? Request, string[] Errors);
