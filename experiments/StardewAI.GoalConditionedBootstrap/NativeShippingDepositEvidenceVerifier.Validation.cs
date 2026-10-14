using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeShippingDepositEvidenceVerifier
{
    private static void VerifyReceipt(
        ActionQueueItem[] queueItems,
        ActionQueueItem? queueItem,
        int? queueItemIndex,
        QueueExecutionReceiptEnvelope? receipt,
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string qualifiedItemId,
        int quantity,
        ICollection<string> reasons)
    {
        if (!string.Equals(before.SchemaVersion, "snapshot.v1",
                StringComparison.Ordinal) ||
            !string.Equals(after.SchemaVersion, "snapshot.v1",
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(before.GameVersion) ||
            !string.Equals(before.GameVersion, after.GameVersion,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(before.SaveId.Value) ||
            !string.Equals(before.SaveId.Value, after.SaveId.Value,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(before.PlayerId.Value) ||
            !string.Equals(before.PlayerId.Value, after.PlayerId.Value,
                StringComparison.Ordinal))
        {
            reasons.Add("shipping_deposit_snapshot_identity_invalid");
        }
        if (receipt is null)
        {
            reasons.Add("shipping_deposit_queue_receipt_missing");
            return;
        }
        if (!string.Equals(
                receipt.SourceStateHash,
                before.StateHash,
                StringComparison.Ordinal) ||
            !string.Equals(
                receipt.AfterStateHash,
                after.StateHash,
                StringComparison.Ordinal) ||
            !receipt.AfterSnapshotFresh)
        {
            reasons.Add("shipping_deposit_receipt_snapshot_lineage_invalid");
        }
        if (!string.Equals(receipt.SchemaVersion,
                "queue_execution_receipt.v1", StringComparison.Ordinal) ||
            !string.Equals(receipt.QueueExecutionMode,
                "sequential_queue_items", StringComparison.Ordinal) ||
            !string.Equals(receipt.Status, "applied", StringComparison.Ordinal) ||
            !receipt.Success ||
            !receipt.SelectedCandidateCompleted ||
            (receipt.BlockReasons?.Length ?? 0) != 0)
        {
            reasons.Add("shipping_deposit_queue_receipt_invalid");
        }
        if (receipt.BeforeGameTick != before.GameTick ||
            receipt.AfterGameTick != after.GameTick ||
            receipt.AfterGameTick <= receipt.BeforeGameTick)
        {
            reasons.Add("shipping_deposit_receipt_tick_boundary_invalid");
        }
        if (receipt.StepResults.Length != queueItems.Length ||
            receipt.PlannedItemCount != queueItems.Length ||
            receipt.ExecutedItemCount != queueItems.Length ||
            receipt.FinalPendingItemCount != 0)
        {
            reasons.Add("shipping_deposit_receipt_step_count_mismatch");
        }
        if (queueItem is null || !queueItemIndex.HasValue)
            return;

        var steps = receipt.StepResults.Where(step =>
                step.QueueItemIndex == queueItemIndex.Value &&
                string.Equals(
                    step.QueueItemId,
                    queueItem.QueueItemId,
                    StringComparison.Ordinal))
            .ToArray();
        if (steps.Length != 1)
        {
            reasons.Add("shipping_deposit_terminal_receipt_not_unique");
            return;
        }
        var terminal = steps[0];
        if (!terminal.SelectedQueueCandidateCompleted ||
            !string.Equals(terminal.Status, "applied", StringComparison.Ordinal) ||
            !string.Equals(terminal.OptionId, ShippingOptionId,
                StringComparison.Ordinal) ||
            !string.Equals(terminal.PrimitiveKind,
                "ship_inventory_item_to_bin", StringComparison.Ordinal) ||
            !string.Equals(terminal.PrimitiveVerificationStatus, "verified",
                StringComparison.Ordinal) ||
            (terminal.PrimitiveVerificationReasons?.Length ?? 0) == 0 ||
            (terminal.BlockReasons?.Length ?? 0) != 0 ||
            !string.IsNullOrWhiteSpace(terminal.FailureAttribution) ||
            !terminal.AfterSnapshotFresh ||
            !terminal.StateHashChanged ||
            !string.Equals(terminal.QueueId, receipt.QueueId,
                StringComparison.Ordinal) ||
            !string.Equals(terminal.AfterStateHash, after.StateHash,
                StringComparison.Ordinal))
        {
            reasons.Add("shipping_deposit_terminal_receipt_invalid");
        }

        var inventoryPath = "player.inventory." + qualifiedItemId + ".count";
        var shippingPath = "farm.shipping_bin." + qualifiedItemId + ".count";
        var inventory = ReadChangedFact(terminal.ChangedFacts, inventoryPath);
        var shipping = ReadChangedFact(terminal.ChangedFacts, shippingPath);
        if (inventory is null || shipping is null)
        {
            reasons.Add("shipping_deposit_changed_fact_missing");
            return;
        }
        if (inventory.Value.Before - inventory.Value.After != quantity ||
            shipping.Value.After - shipping.Value.Before != quantity)
        {
            reasons.Add("shipping_deposit_changed_fact_delta_mismatch");
        }
    }

    private static void VerifyDeltas(
        NativeShippingDepositInventoryRead? beforeInventory,
        NativeShippingDepositInventoryRead? afterInventory,
        NativeShippingDepositBinRead? beforeBin,
        NativeShippingDepositBinRead? afterBin,
        int quantity,
        int? unitPrice,
        ICollection<string> reasons)
    {
        if (beforeInventory is null || afterInventory is null ||
            beforeBin is null || afterBin is null || !unitPrice.HasValue)
        {
            reasons.Add("shipping_deposit_snapshot_evidence_incomplete");
            return;
        }
        if (beforeInventory.SlotQuantity - afterInventory.SlotQuantity !=
                quantity ||
            beforeInventory.ItemQuantity - afterInventory.ItemQuantity !=
                quantity)
        {
            reasons.Add("shipping_deposit_inventory_delta_mismatch");
        }
        if (afterBin.VariantQuantity - beforeBin.VariantQuantity != quantity ||
            afterBin.ItemQuantity - beforeBin.ItemQuantity != quantity)
        {
            reasons.Add("shipping_deposit_bin_delta_mismatch");
        }
        if (!string.Equals(beforeBin.Scope, afterBin.Scope,
                StringComparison.Ordinal) ||
            !string.Equals(beforeBin.PlayerId, afterBin.PlayerId,
                StringComparison.Ordinal))
        {
            reasons.Add("shipping_deposit_bin_scope_drifted");
        }
        var expectedValueDelta = checked((long)quantity * unitPrice.Value);
        if (afterBin.PendingSettlementValue -
                beforeBin.PendingSettlementValue != expectedValueDelta)
        {
            reasons.Add("shipping_deposit_pending_value_delta_mismatch");
        }
    }

    private static void VerifyChangedFactsAgainstSnapshots(
        QueueExecutionReceiptEnvelope? receipt,
        int? queueItemIndex,
        string qualifiedItemId,
        NativeShippingDepositInventoryRead? beforeInventory,
        NativeShippingDepositInventoryRead? afterInventory,
        NativeShippingDepositBinRead? beforeBin,
        NativeShippingDepositBinRead? afterBin,
        ICollection<string> reasons)
    {
        if (receipt is null || !queueItemIndex.HasValue ||
            beforeInventory is null || afterInventory is null ||
            beforeBin is null || afterBin is null)
        {
            return;
        }
        var terminals = receipt.StepResults.Where(step =>
                step.QueueItemIndex == queueItemIndex.Value)
            .ToArray();
        if (terminals.Length != 1)
            return;
        var terminal = terminals[0];
        var inventory = ReadChangedFact(
            terminal.ChangedFacts,
            "player.inventory." + qualifiedItemId + ".count");
        var shipping = ReadChangedFact(
            terminal.ChangedFacts,
            "farm.shipping_bin." + qualifiedItemId + ".count");
        if (inventory is null || shipping is null)
            return;
        if (inventory.Value.Before != beforeInventory.ItemQuantity ||
            inventory.Value.After != afterInventory.ItemQuantity ||
            shipping.Value.Before != beforeBin.ItemQuantity ||
            shipping.Value.After != afterBin.ItemQuantity)
        {
            reasons.Add("shipping_deposit_changed_fact_snapshot_mismatch");
        }
    }

    private static void VerifyUnchanged(
        long? before,
        long? after,
        string prefix,
        ICollection<string> reasons)
    {
        if (!before.HasValue || !after.HasValue)
            reasons.Add(prefix + "_unavailable");
        else if (before.Value != after.Value)
            reasons.Add(prefix + "_changed");
    }
}
