using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueAlternativeComparisonProofBuilder
{
    public static MoneyRevenueAlternativeComparisonProofReceipt Build(
        string manifestPath)
    {
        var fullManifestPath = RequiredFile(
            manifestPath,
            "Money revenue alternative comparison proof manifest");
        var manifest = Read<MoneyRevenueAlternativeComparisonProofManifest>(
            fullManifestPath,
            "Money revenue alternative comparison proof manifest");
        ValidateManifest(manifest);
        var alternatives = manifest.Alternatives
            .Select((proof, index) => VerifyAlternative(proof, index))
            .ToArray();
        var anchor = alternatives[0];
        Require(alternatives.All(row =>
                row.SourceStateHash == anchor.SourceStateHash &&
                row.SourceSnapshotSha256 == anchor.SourceSnapshotSha256 &&
                row.SourceStrategyLedgerSha256 ==
                    anchor.SourceStrategyLedgerSha256 &&
                row.SourceLedgerRevision == anchor.SourceLedgerRevision),
            "Money revenue alternatives do not share one exact source checkpoint and ledger.");

        var expectedIds = manifest.Alternatives
            .Select(row => row.AlternativeId)
            .ToArray();
        var comparison = MoneyRevenueAlternativeComparer
            .CompareVerifiedArtifacts(
                manifest.CriterionId,
                anchor.SourceStateHash,
                anchor.SourceLedgerRevision,
                expectedIds,
                alternatives.Select(row =>
                    new MoneyRevenueVerifiedArtifactAlternative(
                        row.AlternativeId,
                        row.RevenuePrefix,
                        row.AggregateOpportunityCost,
                        row.RouteTransitions.All(route =>
                            route.ReservationNotRequired),
                        row.RouteTransitions
                            .SelectMany(route => route.ReservationIds)
                            .Distinct(StringComparer.Ordinal)
                            .Order(StringComparer.Ordinal)
                            .ToArray()))
                    .ToArray());
        return new MoneyRevenueAlternativeComparisonProofReceipt
        {
            Status =
                "verified_non_authoritative_revenue_alternative_comparison",
            ManifestSha256 = Hash(fullManifestPath),
            SourceStateHash = anchor.SourceStateHash,
            SourceSnapshotSha256 = anchor.SourceSnapshotSha256,
            SourceStrategyLedgerSha256 =
                anchor.SourceStrategyLedgerSha256,
            SourceLedgerRevision = anchor.SourceLedgerRevision,
            CandidateDenominatorAuthoritative = false,
            ArtifactProofVerified = true,
            ProductionTeacherPreferenceLabelEligible = false,
            Comparison = comparison,
            Alternatives = alternatives,
            FormalProductTrainingAuthorized = false
        };
    }

    private static MoneyRevenueAlternativeArtifactEvidence VerifyAlternative(
        MoneyRevenueAlternativeArtifactProof proof,
        int index)
    {
        if (proof is null)
        {
            throw new InvalidDataException(
                "Money revenue alternative proof is null at index " +
                index + ".");
        }
        Require(!string.IsNullOrWhiteSpace(proof.AlternativeId),
            "Money revenue alternative proof identity is invalid at index " +
            index + ".");
        var rolloutManifestPath = RequiredFile(
            proof.AcquisitionRolloutProofManifestPath,
            "Acquisition rollout proof manifest");
        var rolloutReceiptPath = RequiredFile(
            proof.AcquisitionRolloutProofReceiptPath,
            "Acquisition rollout proof receipt");
        var rolloutManifest = Read<
            AcquisitionRoutePortfolioRolloutProofManifest>(
            rolloutManifestPath,
            "Acquisition rollout proof manifest");
        var rebuiltRollout = AcquisitionRoutePortfolioRolloutProofBuilder
            .BuildVerifiedReceipt(rolloutManifestPath);
        var rolloutReceipt = Read<
            AcquisitionRoutePortfolioRolloutProofReceipt>(
            rolloutReceiptPath,
            "Acquisition rollout proof receipt");
        Require(EqualJson(rolloutReceipt, rebuiltRollout.Receipt) &&
                rolloutReceipt.ProofChainVerified &&
                rolloutReceipt.PortfolioCompletionVerified &&
                !rolloutReceipt.FormalTrainingAuthorized,
            "Money revenue acquisition rollout receipt is not complete and exact.");

        var sources = Sources(rolloutManifest).ToArray();
        Require(sources.Length == rolloutReceipt.TransitionCount &&
                sources.Length > 0,
            "Money revenue acquisition rollout transition count drifted.");
        var sourceSnapshotPath = RequiredFile(
            sources[0].Inputs.BeforeSnapshotPath,
            "Money revenue acquisition source snapshot");
        var sourceSnapshot = Read<SnapshotEnvelope>(
            sourceSnapshotPath,
            "Money revenue acquisition source snapshot");
        var sourceCheckpoint = MoneyRevenueProgressionVerifier.Project(
            sourceSnapshot);
        var sourceLedgerPath = RequiredFile(
            sources[0].Inputs.StrategyLedgerPath,
            "Money revenue acquisition source strategy ledger");
        var sourceLedger = Read<StrategyCommitmentLedger>(
            sourceLedgerPath,
            "Money revenue acquisition source strategy ledger");

        var routeEvidence = sources.Select((source, transitionIndex) =>
                VerifyRouteTransition(
                    source,
                    transitionIndex,
                    rolloutReceipt.GoalId))
            .ToArray();
        Require(routeEvidence[0].SourceStateHash == sourceSnapshot.StateHash &&
                routeEvidence[0].BaseLedgerRevision == sourceLedger.Revision,
            "Money revenue acquisition root commit drifted from its source checkpoint.");

        var latestSnapshotPath = RequiredFile(
            sources[^1].AfterSnapshotPath,
            "Money revenue acquisition terminal snapshot");
        var latestSnapshot = Read<SnapshotEnvelope>(
            latestSnapshotPath,
            "Money revenue acquisition terminal snapshot");
        Require(latestSnapshot.StateHash == rolloutReceipt.LatestStateHash,
            "Money revenue acquisition rollout terminal state drifted.");

        var revenueManifestPath = RequiredFile(
            proof.RevenueRecurrenceProofManifestPath,
            "Money revenue recurrence proof manifest");
        var revenueReceiptPath = RequiredFile(
            proof.RevenueRecurrenceProofReceiptPath,
            "Money revenue recurrence proof receipt");
        var rebuiltRevenue = MoneyRevenueRecurrenceProofBuilder.Build(
            revenueManifestPath);
        var revenueReceipt = Read<MoneyRevenueRecurrenceProofReceipt>(
            revenueReceiptPath,
            "Money revenue recurrence proof receipt");
        Require(EqualJson(revenueReceipt, rebuiltRevenue) &&
                revenueReceipt.ProofVerified &&
                !revenueReceipt.FormalProductTrainingAuthorized &&
                revenueReceipt.Prefix is not null,
            "Money revenue recurrence receipt is not complete and exact.");
        var revenuePrefix = revenueReceipt.Prefix!;
        var latestCheckpoint = MoneyRevenueProgressionVerifier.Project(
            latestSnapshot);
        Require(latestCheckpoint == revenuePrefix.Transitions[0].Before &&
                latestCheckpoint.StateHash == revenuePrefix.InitialStateHash &&
                sourceCheckpoint.SaveId == revenuePrefix.SaveId &&
                sourceCheckpoint.PlayerId == revenuePrefix.PlayerId &&
                sourceCheckpoint.TotalMoneyEarned ==
                    revenuePrefix.InitialTotalMoneyEarned,
            "Money revenue acquisition rollout does not join the Revenue prefix without drift.");

        var aggregate = AcquisitionOpportunityCostVectorAggregator.Aggregate(
            routeEvidence.Select(row => row.OpportunityCost),
            routeEvidence.Select(row =>
                row.OpportunityCostReportPath + "#" +
                row.RouteOccurrenceId));
        MoneyRevenueAlternativeComparer.ValidateOpportunityCost(aggregate);
        return new MoneyRevenueAlternativeArtifactEvidence(
            proof.AlternativeId,
            rolloutReceipt.GoalId,
            rolloutReceipt.RolloutId,
            sourceSnapshot.StateHash,
            Hash(sourceSnapshotPath),
            Hash(sourceLedgerPath),
            sourceLedger.Revision,
            revenuePrefix.InitialStateHash,
            Hash(rolloutManifestPath),
            Hash(rolloutReceiptPath),
            Hash(revenueManifestPath),
            Hash(revenueReceiptPath),
            routeEvidence,
            aggregate,
            revenuePrefix);
    }

}
