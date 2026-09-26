using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class FullShipmentRecurrenceProofBuilder
{
    private static VerifiedDeposit VerifyDeposit(
        FullShipmentRecurrenceIterationProof proof,
        string inventoryPath,
        string loweringPath,
        SnapshotEnvelope acquisitionAfter,
        IReadOnlyCollection<string> requiredIds)
    {
        var input = proof.Deposit ?? throw new InvalidDataException(
            "Full Shipment recurrence deposit proof is missing.");
        var beforePath = Path.GetFullPath(input.BeforeSnapshotPath);
        var afterPath = Path.GetFullPath(input.AfterSnapshotPath);
        var before = ReadSnapshot(beforePath, "deposit before snapshot");
        var after = ReadSnapshot(afterPath, "deposit after snapshot");
        RequireSameActor(acquisitionAfter, before);
        RequireSameActor(before, after);
        var acquired = FullShipmentSettlementVerifier.Project(
            requiredIds,
            acquisitionAfter);
        var depositBefore = FullShipmentSettlementVerifier.Project(
            requiredIds,
            before);
        var depositAfter = FullShipmentSettlementVerifier.Project(
            requiredIds,
            after);
        Require(
            !string.IsNullOrWhiteSpace(acquisitionAfter.StateHash) &&
            acquisitionAfter.StateHash == before.StateHash &&
            EquivalentProgress(acquired, depositBefore) &&
            acquired.TotalDay == depositBefore.TotalDay &&
            EquivalentProgress(depositBefore, depositAfter) &&
            depositBefore.TotalDay == depositAfter.TotalDay &&
            ExactInventoryReceiptVerifier.InventoryCount(
                before,
                proof.QualifiedItemId,
                0) >= 1 &&
            FullShipmentSettlementVerifier.ProjectUniformBinCount(
                after,
                proof.QualifiedItemId) == 1,
            "Full Shipment deposit boundary changed state, progress, day, reserve, or bin unexpectedly.");
        var expected = CurrentStageOneCollectionTeacherReceiptBuilder.Build(
            inventoryPath,
            loweringPath,
            input.RankingPath,
            beforePath,
            input.MasterAnglerTargetDateIntentsPath,
            input.PreferencePath,
            input.ExecutionReceiptPath,
            afterPath,
            input.TrajectoryId,
            input.RunId,
            input.KnowledgeDictionaryVersion,
            input.ExecutorVersion);
        var actualPath = Path.GetFullPath(input.TeacherReceiptPath);
        var actual = CurrentTeacherFrontierSupport.Read<
            CurrentStageOneCollectionTeacherReceiptAdmission>(
            actualPath,
            "Full Shipment deposit Teacher receipt");
        var transitions = actual.VerifiedRequirementTransitions.Where(row =>
                row.RequirementSetId == "full_shipment" &&
                row.RequirementId == proof.RequirementId &&
                row.QualifiedItemId == proof.QualifiedItemId &&
                row.BindingKind == "native_full_shipment_completion" &&
                row.TransitionKind ==
                    "exact_pending_native_shipment_increased" &&
                row.Verified)
            .ToArray();
        Require(
            EqualJson(actual, expected) &&
            actual.Status == "ready" &&
            actual.TeacherTrainingRowEligible &&
            !actual.FormalTrainingAuthorized &&
            actual.Preference?.SelectedCandidate?.OptionId ==
                "economy.ship_items" &&
            transitions.Length == 1,
            "Full Shipment deposit Teacher receipt is not exact and verified.");
        return new VerifiedDeposit(
            afterPath,
            after,
            CurrentTeacherFrontierSupport.HashFile(actualPath));
    }
}
