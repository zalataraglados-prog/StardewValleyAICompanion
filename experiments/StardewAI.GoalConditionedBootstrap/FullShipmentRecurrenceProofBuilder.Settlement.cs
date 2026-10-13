using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class FullShipmentRecurrenceProofBuilder
{
    private static VerifiedSettlement VerifySettlement(
        FullShipmentRecurrenceIterationProof proof,
        string inventoryPath,
        string loweringPath,
        SnapshotEnvelope depositAfter,
        IReadOnlyCollection<string> requiredIds,
        bool requireTerminal)
    {
        var input = proof.Settlement ?? throw new InvalidDataException(
            "Full Shipment recurrence settlement proof is missing.");
        var recovery = VerifyRecoveryTransitions(
            input,
            depositAfter,
            requiredIds,
            proof.QualifiedItemId);
        var recoveryAfter = recovery.AfterSnapshot;
        var beforePath = Path.GetFullPath(input.BeforeSnapshotPath);
        var afterPath = Path.GetFullPath(input.AfterSnapshotPath);
        var before = ReadSnapshot(beforePath, "settlement before snapshot");
        var after = ReadSnapshot(afterPath, "settlement after snapshot");
        RequireSameActor(recoveryAfter, before);
        RequireSameActor(before, after);
        var boundaryReasons = ValidateRecoveryToSleepBoundary(
            recoveryAfter,
            before,
            requiredIds,
            proof.QualifiedItemId);
        Require(
            boundaryReasons.Length == 0,
            "Full Shipment recovery-to-sleep boundary is invalid: " +
            string.Join(",", boundaryReasons));

        var actualPath = Path.GetFullPath(input.SettlementReceiptPath);
        int beforeCount;
        int afterCount;
        int startDay;
        int endDay;
        bool terminal;
        if (requireTerminal)
        {
            Require(
                input.SettlementKind ==
                    FullShipmentRecurrenceSettlementKinds.Terminal,
                "Final Full Shipment iteration does not use the terminal receipt.");
            var expected = FullShipmentTerminalSettlementReceiptBuilder.Build(
                inventoryPath,
                loweringPath,
                input.QueuePath,
                beforePath,
                input.ExecutionReceiptPath,
                afterPath,
                input.RunId,
                input.ExecutorVersion,
                input.SelectedCandidateId);
            var actual = CurrentTeacherFrontierSupport.Read<
                FullShipmentTerminalSettlementReceipt>(
                actualPath,
                "Full Shipment terminal settlement receipt");
            Require(
                EqualJson(actual, expected) &&
                actual.Status == "ready" &&
                actual.TrainingLabelEligible &&
                actual.TransitionEvidence.TerminalQualifiedItemId ==
                    proof.QualifiedItemId,
                "Full Shipment terminal settlement receipt is not exact and verified.");
            beforeCount = actual.TransitionEvidence.BeforeShippedItemCount ?? -1;
            afterCount = actual.TransitionEvidence.AfterShippedItemCount ?? -1;
            startDay = actual.TransitionEvidence.BeforeTotalDay ?? -1;
            endDay = actual.TransitionEvidence.AfterTotalDay ?? -1;
            terminal = true;
        }
        else
        {
            Require(
                input.SettlementKind ==
                    FullShipmentRecurrenceSettlementKinds.Ordinary,
                "A nonterminal Full Shipment iteration uses the wrong receipt kind.");
            var expected = FullShipmentSettlementReceiptBuilder.Build(
                inventoryPath,
                loweringPath,
                input.QueuePath,
                beforePath,
                input.ExecutionReceiptPath,
                afterPath,
                input.RunId,
                input.ExecutorVersion,
                input.SelectedCandidateId,
                proof.QualifiedItemId);
            var actual = CurrentTeacherFrontierSupport.Read<
                FullShipmentSettlementReceipt>(
                actualPath,
                "Full Shipment ordinary settlement receipt");
            Require(
                EqualJson(actual, expected) &&
                actual.Status == "ready" &&
                actual.RecurrenceEvidenceEligible &&
                actual.TransitionEvidence.SettledQualifiedItemId ==
                    proof.QualifiedItemId &&
                !actual.TransitionEvidence.TerminalTransition,
                "Full Shipment ordinary settlement receipt is not exact and verified.");
            beforeCount = actual.TransitionEvidence.BeforeShippedItemCount ?? -1;
            afterCount = actual.TransitionEvidence.AfterShippedItemCount ?? -1;
            startDay = actual.TransitionEvidence.BeforeTotalDay ?? -1;
            endDay = actual.TransitionEvidence.AfterTotalDay ?? -1;
            terminal = false;
        }
        return new VerifiedSettlement(
            afterPath,
            after,
            CurrentTeacherFrontierSupport.HashFile(actualPath),
            beforeCount,
            afterCount,
            startDay,
            endDay,
            terminal,
            recovery.Evidence);
    }

    internal static string[] ValidateRecoveryToSleepBoundary(
        SnapshotEnvelope recoveryAfter,
        SnapshotEnvelope settlementBefore,
        IReadOnlyCollection<string> requiredIds,
        string qualifiedItemId)
    {
        var reasons = new List<string>();
        if (!SnapshotHash.MatchesStateHash(
                recoveryAfter.State,
                recoveryAfter.StateHash) ||
            !SnapshotHash.MatchesStateHash(
                settlementBefore.State,
                settlementBefore.StateHash))
        {
            reasons.Add(
                "full_shipment_recovery_sleep_snapshot_hash_invalid");
        }
        if (settlementBefore.GameTick < recoveryAfter.GameTick)
        {
            reasons.Add(
                "full_shipment_recovery_sleep_game_tick_reversed");
        }
        if (!SameActor(recoveryAfter, settlementBefore))
        {
            reasons.Add(
                "full_shipment_recovery_sleep_actor_identity_drifted");
        }
        if (!EquivalentSnapshotStateValue(
                recoveryAfter,
                settlementBefore,
                "time",
                "time") ||
            recoveryAfter.InGameTime.Value is null ||
            settlementBefore.InGameTime.Value is null ||
            recoveryAfter.InGameTime.Value != settlementBefore.InGameTime.Value)
        {
            reasons.Add(
                "full_shipment_recovery_sleep_clock_drifted");
        }
        if (!EquivalentSnapshotStateValue(
                recoveryAfter,
                settlementBefore,
                "player",
                "location_id") ||
            !EquivalentSnapshotStateValue(
                recoveryAfter,
                settlementBefore,
                "player",
                "tile_x") ||
            !EquivalentSnapshotStateValue(
                recoveryAfter,
                settlementBefore,
                "player",
                "tile_y") ||
            !EquivalentSnapshotStateValue(
                recoveryAfter,
                settlementBefore,
                "current_location",
                "identity"))
        {
            reasons.Add(
                "full_shipment_recovery_sleep_actor_position_drifted");
        }

        var recoveryProgress = FullShipmentSettlementVerifier.Project(
            requiredIds,
            recoveryAfter);
        var settlementProgress = FullShipmentSettlementVerifier.Project(
            requiredIds,
            settlementBefore);
        if (!EquivalentProgress(recoveryProgress, settlementProgress) ||
            recoveryProgress.TotalDay != settlementProgress.TotalDay)
        {
            reasons.Add(
                "full_shipment_recovery_sleep_progress_or_day_drifted");
        }
        if (FullShipmentSettlementVerifier.ProjectUniformBinCount(
                recoveryAfter,
                qualifiedItemId) != 1 ||
            FullShipmentSettlementVerifier.ProjectUniformBinCount(
                settlementBefore,
                qualifiedItemId) != 1)
        {
            reasons.Add(
                "full_shipment_recovery_sleep_pending_bin_item_drifted");
        }
        return reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
