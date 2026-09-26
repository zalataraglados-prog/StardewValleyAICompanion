using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class FullShipmentTerminalSettlementReceiptBuilder
{
    public static FullShipmentTerminalSettlementReceipt Build(
        string requirementInventoryPath,
        string acquisitionLoweringPath,
        string queuePath,
        string beforeSnapshotPath,
        string executionReceiptPath,
        string afterSnapshotPath,
        string runId,
        string executorVersion,
        string selectedCandidateId)
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
            "Full Shipment terminal settlement");
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
            queueFullPath,
            "Full Shipment terminal settlement queue");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            beforeFullPath,
            "Full Shipment terminal settlement before snapshot");
        var receipt = CurrentTeacherFrontierSupport.Read<
            QueueExecutionReceiptEnvelope>(
            receiptFullPath,
            "Full Shipment terminal settlement execution receipt");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterFullPath,
            "Full Shipment terminal settlement after snapshot");
        var requiredIds = FullShipmentSettlementSupport
            .RequiredQualifiedItemIds(inventory);
        var result = new FullShipmentTerminalSettlementReceipt
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
            reasons.Add(
                "full_shipment_terminal_game_version_authority_mismatch");
        }
        if (!FullShipmentSettlementSupport.IsSingleNativeSleepQueue(queue))
        {
            reasons.Add(
                "full_shipment_terminal_queue_not_single_native_sleep");
        }
        var transition = FullShipmentTerminalSettlementVerifier.Verify(
            requiredIds,
            before,
            after);
        reasons.AddRange(transition.BlockingReasons);
        var distinct = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        result.TransitionEvidence = transition;
        result.BlockingReasons = distinct;
        result.TrainingLabelEligible = distinct.Length == 0;
        result.Status = result.TrainingLabelEligible
            ? "ready"
            : "blocked_fresh_native_terminal_settlement_missing";
        return result;
    }

}
