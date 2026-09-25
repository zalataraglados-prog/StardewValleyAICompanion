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
        var beforePath = Path.GetFullPath(input.BeforeSnapshotPath);
        var afterPath = Path.GetFullPath(input.AfterSnapshotPath);
        var before = ReadSnapshot(beforePath, "settlement before snapshot");
        var after = ReadSnapshot(afterPath, "settlement after snapshot");
        RequireSameActor(depositAfter, before);
        RequireSameActor(before, after);
        var deposited = FullShipmentSettlementVerifier.Project(
            requiredIds,
            depositAfter);
        var settlementBefore = FullShipmentSettlementVerifier.Project(
            requiredIds,
            before);
        Require(
            !string.IsNullOrWhiteSpace(depositAfter.StateHash) &&
            depositAfter.StateHash == before.StateHash &&
            EquivalentProgress(deposited, settlementBefore) &&
            deposited.TotalDay == settlementBefore.TotalDay &&
            FullShipmentSettlementVerifier.ProjectUniformBinCount(
                depositAfter,
                proof.QualifiedItemId) == 1 &&
            FullShipmentSettlementVerifier.ProjectUniformBinCount(
                before,
                proof.QualifiedItemId) == 1,
            "Full Shipment sleep boundary changed state, progress, day, or pending bin state.");

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
            terminal);
    }
}
