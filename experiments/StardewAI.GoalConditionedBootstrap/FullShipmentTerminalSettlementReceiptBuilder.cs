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
        var requiredIds = RequiredQualifiedItemIds(inventory);
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
        if (!QueueIsSingleNativeSleep(queue))
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

    private static string[] RequiredQualifiedItemIds(
        AuthoritativeRequirementInventoryReport inventory)
    {
        if (inventory.SchemaVersion !=
                "authoritative_goal_requirement_inventory.v1" ||
            !inventory.DenominatorComplete)
        {
            throw new InvalidDataException(
                "Full Shipment authoritative requirement inventory is incomplete.");
        }
        var set = CurrentTeacherFrontierSupport.SingleSet(
            inventory.RequirementSets,
            value => value.RequirementSetId,
            "full_shipment",
            "requirement inventory");
        if (set.RequiredGroupCount != 154 ||
            set.Groups.Length != set.RequiredGroupCount ||
            set.Groups.Any(group =>
                group.SelectionRule != "all_required" ||
                group.RequiredAlternativeCount != 1 ||
                group.Alternatives.Length != 1 ||
                group.Alternatives[0].MatchKind != "item_id" ||
                group.Alternatives[0].Amount != 1 ||
                group.Alternatives[0].MinimumQuality != 0 ||
                group.Alternatives[0].QualifiedItemId !=
                    "(O)" + group.Alternatives[0].ItemId))
        {
            throw new InvalidDataException(
                "Full Shipment authoritative requirement denominator drifted.");
        }
        var result = set.Groups
            .Select(group => group.Alternatives[0].QualifiedItemId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (result.Distinct(StringComparer.Ordinal).Count() != result.Length)
        {
            throw new InvalidDataException(
                "Full Shipment authoritative requirement identities are duplicated.");
        }
        return result;
    }

    private static bool QueueIsSingleNativeSleep(ActionQueueEnvelope queue)
    {
        var items = queue.Items ?? Array.Empty<ActionQueueItem>();
        if (items.Length != 1)
            return false;
        var item = items[0];
        var parameters = item.NormalizedCommand?.Parameters ??
            Array.Empty<SmallModelActionParameter>();
        return item.OptionId == "executor.sleep" ||
            item.OptionId == "recovery.stabilize_day" &&
            parameters.Any(parameter =>
                parameter.Name == "execution_option_id" &&
                parameter.Value == "executor.sleep");
    }
}
