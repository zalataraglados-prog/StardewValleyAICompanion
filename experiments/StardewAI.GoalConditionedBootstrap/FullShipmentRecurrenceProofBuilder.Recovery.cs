using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class FullShipmentRecurrenceProofBuilder
{
    private const int MaxSettlementRecoveryTransitions = 8;

    private static VerifiedRecoveryChain VerifyRecoveryTransitions(
        FullShipmentRecurrenceSettlementProof settlement,
        SnapshotEnvelope depositAfter,
        IReadOnlyCollection<string> requiredIds,
        string qualifiedItemId)
    {
        var transitions = settlement.RecoveryTransitions ??
            throw new InvalidDataException(
                "Full Shipment recovery transitions are null.");
        Require(
            transitions.Length <= MaxSettlementRecoveryTransitions,
            "Full Shipment recovery transition count is unbounded.");

        var expectedBefore = depositAfter;
        var evidence = new List<FullShipmentRecoveryTransitionEvidence>();
        var queuePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var receiptPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var afterPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in transitions)
        {
            var transition = input ?? throw new InvalidDataException(
                "Full Shipment recovery transition is null.");
            var queuePath = Path.GetFullPath(transition.QueuePath);
            var beforePath = Path.GetFullPath(transition.BeforeSnapshotPath);
            var receiptPath = Path.GetFullPath(
                transition.ExecutionReceiptPath);
            var afterPath = Path.GetFullPath(transition.AfterSnapshotPath);
            Require(
                queuePaths.Add(queuePath) &&
                receiptPaths.Add(receiptPath) &&
                afterPaths.Add(afterPath),
                "Full Shipment recovery transition artifacts are duplicated.");
            var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
                queuePath,
                "Full Shipment recovery queue");
            var before = ReadSnapshot(
                beforePath,
                "Full Shipment recovery before snapshot");
            var receipt = CurrentTeacherFrontierSupport.Read<
                QueueExecutionReceiptEnvelope>(
                receiptPath,
                "Full Shipment recovery execution receipt");
            var after = ReadSnapshot(
                afterPath,
                "Full Shipment recovery after snapshot");
            var reasons = ValidateRecoveryTransition(
                queue,
                before,
                receipt,
                after,
                expectedBefore,
                requiredIds,
                qualifiedItemId,
                transition.RunId,
                transition.ExecutorVersion,
                transition.SelectedCandidateId);
            Require(
                reasons.Length == 0,
                "Full Shipment recovery transition is invalid: " +
                string.Join(",", reasons));
            evidence.Add(new FullShipmentRecoveryTransitionEvidence(
                CurrentTeacherFrontierSupport.HashFile(queuePath),
                CurrentTeacherFrontierSupport.HashFile(beforePath),
                CurrentTeacherFrontierSupport.HashFile(receiptPath),
                CurrentTeacherFrontierSupport.HashFile(afterPath),
                transition.RunId,
                transition.ExecutorVersion,
                transition.SelectedCandidateId));
            expectedBefore = after;
        }

        return new VerifiedRecoveryChain(
            expectedBefore,
            evidence.ToArray());
    }

    internal static string[] ValidateRecoveryTransition(
        ActionQueueEnvelope queue,
        SnapshotEnvelope before,
        QueueExecutionReceiptEnvelope receipt,
        SnapshotEnvelope after,
        SnapshotEnvelope expectedBefore,
        IReadOnlyCollection<string> requiredIds,
        string qualifiedItemId,
        string runId,
        string executorVersion,
        string selectedCandidateId)
    {
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
        if (!IsIntermediateRecoveryQueue(queue, selectedCandidateId))
            reasons.Add("full_shipment_recovery_queue_not_native_stabilization");
        var exactStateHashChain =
            !string.IsNullOrWhiteSpace(expectedBefore.StateHash) &&
            expectedBefore.StateHash == before.StateHash;
        var verifiedSemanticStateChain =
            SnapshotHash.MatchesStateHash(
                expectedBefore.State,
                expectedBefore.StateHash) &&
            SnapshotHash.MatchesStateHash(before.State, before.StateHash) &&
            SnapshotHash.ComputeStateHash(expectedBefore.State) ==
                SnapshotHash.ComputeStateHash(before.State);
        if (!exactStateHashChain && !verifiedSemanticStateChain)
        {
            reasons.Add("full_shipment_recovery_state_hash_chain_broken");
        }
        if (!SameActor(expectedBefore, before) ||
            !SameActor(before, after))
        {
            reasons.Add("full_shipment_recovery_actor_or_game_identity_drifted");
        }

        var expectedProgress = FullShipmentSettlementVerifier.Project(
            requiredIds,
            expectedBefore);
        var beforeProgress = FullShipmentSettlementVerifier.Project(
            requiredIds,
            before);
        var afterProgress = FullShipmentSettlementVerifier.Project(
            requiredIds,
            after);
        if (!EquivalentProgress(expectedProgress, beforeProgress) ||
            !EquivalentProgress(beforeProgress, afterProgress) ||
            expectedProgress.TotalDay != beforeProgress.TotalDay ||
            beforeProgress.TotalDay != afterProgress.TotalDay)
        {
            reasons.Add("full_shipment_recovery_progress_or_day_drifted");
        }
        if (FullShipmentSettlementVerifier.ProjectUniformBinCount(
                expectedBefore,
                qualifiedItemId) != 1 ||
            FullShipmentSettlementVerifier.ProjectUniformBinCount(
                before,
                qualifiedItemId) != 1 ||
            FullShipmentSettlementVerifier.ProjectUniformBinCount(
                after,
                qualifiedItemId) != 1)
        {
            reasons.Add("full_shipment_recovery_pending_bin_item_drifted");
        }
        return reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsIntermediateRecoveryQueue(
        ActionQueueEnvelope queue,
        string selectedCandidateId)
    {
        var items = queue.Items ?? Array.Empty<ActionQueueItem>();
        if (items.Length != 1)
            return false;
        return (selectedCandidateId, items[0].OptionId) switch
        {
            ("recovery:native_save_boundary",
                "executor.traverse_connector") => true,
            ("recovery:return_home",
                "executor.traverse_connector") => true,
            ("recovery:close_blocking_menu",
                "executor.close_menu") => true,
            ("recovery:refresh_plan_after_stabilization",
                "executor.wait_ticks") => true,
            _ => false
        };
    }

    private static bool SameActor(
        SnapshotEnvelope left,
        SnapshotEnvelope right) =>
        !string.IsNullOrWhiteSpace(left.SaveId.Value) &&
        left.SaveId.Value == right.SaveId.Value &&
        !string.IsNullOrWhiteSpace(left.PlayerId.Value) &&
        left.PlayerId.Value == right.PlayerId.Value &&
        left.GameVersion == right.GameVersion;

    private sealed record VerifiedRecoveryChain(
        SnapshotEnvelope AfterSnapshot,
        FullShipmentRecoveryTransitionEvidence[] Evidence);
}
