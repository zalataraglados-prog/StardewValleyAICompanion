using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

public static class MuseumDonationCustodySettlementRequestBuilder
{
    public static MuseumDonationCustodySettlementBuildResult Build(ActionQueueItem item, QueueExecutionStepReceipt receipt,
        SnapshotEnvelope before, SnapshotEnvelope after, StrategyCommitmentLedger ledger)
    {
        var errors = new List<string>();
        var id = Parameter(item, "museum_output_custody_reservation_id");
        var row = ledger.MaterialReservations.SingleOrDefault(value => value.ReservationId == id);
        var goalId = item.NormalizedCommand.GoalId;
        if (row is null || string.IsNullOrWhiteSpace(goalId) ||
            !MaterialOutputCustodyContract.IsAuthorized(row, ledger, MaterialOutputCustodyContract.MuseumConsumer, goalId) ||
            IntParameter(item, "museum_output_custody_reservation_revision") != row.Revision ||
            IntParameter(item, "inventory_slot_index") != row.SlotIndex ||
            Parameter(item, "qualified_item_id") != row.QualifiedItemId ||
            Parameter(item, "material_reservation_ledger_id") != ledger.LedgerId)
            errors.Add("museum_custody_settlement_authorization_invalid");
        if (item.OptionId != MaterialOutputCustodyContract.MuseumConsumer || receipt.OptionId != item.OptionId ||
            receipt.QueueItemId != item.QueueItemId || receipt.Status != "applied" ||
            receipt.PrimitiveVerificationStatus != "verified" || !receipt.AfterSnapshotFresh ||
            receipt.SourceStateHash != before.StateHash || receipt.AfterStateHash != after.StateHash ||
            item.NormalizedCommand.StateHash != before.StateHash || string.IsNullOrWhiteSpace(before.StateHash) ||
            string.IsNullOrWhiteSpace(after.StateHash) || before.StateHash == after.StateHash)
            errors.Add("museum_custody_settlement_native_receipt_invalid");
        foreach (var snapshot in new[] { before, after })
            if (ReadStateFieldString(snapshot, "identity", "save_id") != ledger.SaveId ||
                ReadStateFieldString(snapshot, "identity", "player_id") != ledger.PlayerId)
                errors.Add("museum_custody_settlement_actor_identity_invalid");
        var qualified = Parameter(item, "qualified_item_id");
        var beforeCount = IntParameter(item, "expected_donated_count_before");
        var afterCount = IntParameter(item, "expected_donated_count_after");
        var beforeMuseum = ReadStateFieldValue(before, "world_progress", "museum");
        var afterMuseum = ReadStateFieldValue(after, "world_progress", "museum");
        if (!MaterialOutputCustodyContract.TryMuseumDonated(before, qualified, out var donatedBefore) || donatedBefore ||
            !MaterialOutputCustodyContract.TryMuseumDonated(after, qualified, out var donatedAfter) || !donatedAfter ||
            !beforeCount.HasValue || afterCount != beforeCount + 1 ||
            beforeMuseum is not { ValueKind: JsonValueKind.Object } || afterMuseum is not { ValueKind: JsonValueKind.Object } ||
            ReadInt(beforeMuseum.Value, "donated_count", -1) != beforeCount || ReadInt(afterMuseum.Value, "donated_count", -1) != afterCount)
            errors.Add("museum_custody_settlement_native_progress_invalid");
        var slot = IntParameter(item, "inventory_slot_index");
        var stackBefore = IntParameter(item, "expected_stack_before");
        var stackAfter = IntParameter(item, "expected_stack_after");
        if (!slot.HasValue || !stackBefore.HasValue || stackBefore < 1 || stackAfter != stackBefore - 1 ||
            !InventoryMatches(before, slot.Value, qualified, stackBefore.Value) ||
            !InventoryMatches(after, slot.Value, qualified, stackAfter ?? -1))
            errors.Add("museum_custody_settlement_inventory_transition_invalid");
        if (errors.Count > 0 || row is null)
            return new(null, errors.Distinct(StringComparer.Ordinal).ToArray());
        var evidence = JsonSerializer.Serialize(new
        {
            Schema = "museum_output_custody_consumption.v1", Item = item,
            Receipt = new
            {
                receipt.QueueId, receipt.QueueItemId, receipt.OptionId, receipt.Status,
                receipt.PrimitiveVerificationStatus, receipt.AfterSnapshotFresh,
                receipt.SourceStateHash, receipt.AfterStateHash
            },
            BeforeStateHash = before.StateHash, AfterStateHash = after.StateHash
        });
        using var sha = SHA256.Create();
        var hash = string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(evidence)).Select(value => value.ToString("x2")));
        return new(new ReservationPortfolioSupportingTransitionSettlementRequest
        {
            StateHash = after.StateHash, ExpectedLedgerRevision = ledger.Revision,
            PortfolioId = row.SourceDecisionId, RouteSourceDecisionId = row.SourceDecisionId, GoalId = row.GoalId,
            ConsumerOptionId = MaterialOutputCustodyContract.MuseumConsumer, SupportingTransitionReceiptSha256 = hash,
            MaterialConsumptions = new[] { new ReservationPortfolioMaterialConsumption
            {
                MaterialReservationId = row.ReservationId, NodeId = row.NodeId, SlotIndex = row.SlotIndex,
                QualifiedItemId = row.QualifiedItemId, ConsumedQuantity = 1
            } }, Reason = "native_museum_donation_consumed_retained_output"
        }, Array.Empty<string>());
    }

    private static bool InventoryMatches(SnapshotEnvelope snapshot, int slot, string qualified, int stack)
    {
        var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "player", "inventory")) || inventory is not { ValueKind: JsonValueKind.Array }) return false;
        var rows = inventory.Value.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object && ReadInt(row, "slot_index", -1) == slot).ToArray();
        return stack == 0 ? rows.Length == 0 || rows.Length == 1 && ReadInt(rows[0], "stack", 0) == 0 :
            rows.Length == 1 && ReadString(rows[0], "qualified_item_id") == qualified && ReadInt(rows[0], "stack", -1) == stack;
    }

    private static string Parameter(ActionQueueItem item, string name)
    {
        var values = item.NormalizedCommand.Parameters.Where(value => value.Name == name).ToArray();
        return values.Length == 1 ? values[0].Value : string.Empty;
    }
    private static int? IntParameter(ActionQueueItem item, string name) => int.TryParse(Parameter(item, name), out var value) ? value : null;
}

public sealed record MuseumDonationCustodySettlementBuildResult(
    ReservationPortfolioSupportingTransitionSettlementRequest? Request, string[] Errors);
