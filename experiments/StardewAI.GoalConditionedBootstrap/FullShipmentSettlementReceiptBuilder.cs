using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class FullShipmentSettlementReceiptBuilder
{
    public static FullShipmentSettlementReceipt Build(
        string requirementInventoryPath,
        string acquisitionLoweringPath,
        string queuePath,
        string beforeSnapshotPath,
        string executionReceiptPath,
        string afterSnapshotPath,
        string runId,
        string executorVersion,
        string selectedCandidateId,
        string expectedQualifiedItemId)
    {
        var inventoryFullPath = Path.GetFullPath(requirementInventoryPath);
        var loweringFullPath = Path.GetFullPath(acquisitionLoweringPath);
        var queueFullPath = Path.GetFullPath(queuePath);
        var beforeFullPath = Path.GetFullPath(beforeSnapshotPath);
        var receiptFullPath = Path.GetFullPath(executionReceiptPath);
        var afterFullPath = Path.GetFullPath(afterSnapshotPath);
        var inventory = CurrentTeacherFrontierSupport.Read<
            AuthoritativeRequirementInventoryReport>(
            inventoryFullPath,
            "Full Shipment authoritative requirement inventory");
        var lowering = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteOptionLoweringReport>(
            loweringFullPath,
            "Full Shipment authoritative acquisition lowering");
        CurrentTeacherFrontierSupport.ValidateAuthority(
            inventoryFullPath,
            inventory,
            lowering,
            "Full Shipment settlement");
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
            queueFullPath,
            "Full Shipment settlement queue");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            beforeFullPath,
            "Full Shipment settlement before snapshot");
        var receipt = CurrentTeacherFrontierSupport.Read<
            QueueExecutionReceiptEnvelope>(
            receiptFullPath,
            "Full Shipment settlement execution receipt");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterFullPath,
            "Full Shipment settlement after snapshot");
        var requiredIds = FullShipmentSettlementSupport
            .RequiredQualifiedItemIds(inventory);
        var result = new FullShipmentSettlementReceipt
        {
            RequirementInventorySha256 = CurrentTeacherFrontierSupport.HashFile(
                inventoryFullPath),
            AcquisitionLoweringSha256 = CurrentTeacherFrontierSupport.HashFile(
                loweringFullPath),
            QueueSha256 = CurrentTeacherFrontierSupport.HashFile(queueFullPath),
            BeforeSnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(
                beforeFullPath),
            ExecutionReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(
                receiptFullPath),
            AfterSnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(
                afterFullPath)
        };
        var reasons = QueueExecutionReceiptValidator.Validate(
                queue,
                before,
                receipt,
                after,
                runId,
                executorVersion,
                selectedCandidateId,
                queue.StateHash,
                requireTeacherPreferenceStateRebound: false)
            .ToList();
        if (!string.Equals(
                inventory.GameVersion,
                before.GameVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                inventory.GameVersion,
                after.GameVersion,
                StringComparison.Ordinal))
        {
            reasons.Add("full_shipment_settlement_game_version_authority_mismatch");
        }
        if (!FullShipmentSettlementSupport.IsSingleNativeSleepQueue(queue))
        {
            reasons.Add("full_shipment_settlement_queue_not_single_native_sleep");
        }
        var transition = FullShipmentSettlementVerifier.Verify(
            requiredIds,
            before,
            after);
        reasons.AddRange(transition.BlockingReasons);
        if (string.IsNullOrWhiteSpace(expectedQualifiedItemId) ||
            !requiredIds.Contains(
                expectedQualifiedItemId,
                StringComparer.Ordinal) ||
            !string.Equals(
                transition.SettledQualifiedItemId,
                expectedQualifiedItemId,
                StringComparison.Ordinal))
        {
            reasons.Add("full_shipment_settlement_expected_item_mismatch");
        }
        if (transition.TerminalTransition)
        {
            reasons.Add(
                "full_shipment_settlement_terminal_requires_dedicated_receipt");
        }

        var distinct = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        result.TransitionEvidence = transition;
        result.BlockingReasons = distinct;
        result.RecurrenceEvidenceEligible = distinct.Length == 0;
        result.Status = result.RecurrenceEvidenceEligible
            ? "ready"
            : "blocked_fresh_native_settlement_missing";
        return result;
    }
}
