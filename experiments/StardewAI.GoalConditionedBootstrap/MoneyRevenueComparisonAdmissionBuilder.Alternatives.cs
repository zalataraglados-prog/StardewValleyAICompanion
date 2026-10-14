using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueComparisonAdmissionBuilder
{
    internal static MoneyRevenueComparisonAdmissionAlternative
        VerifyAlternative(
            MoneyRevenueCounterfactualProofRequest request,
            MoneyRevenueTerminalAlternativeProof proof,
            MoneyRevenueTerminalExecutionEvidence terminal) =>
        VerifyAlternativeWithContext(request, proof, terminal).Alternative;

    internal static MoneyRevenueVerifiedComparisonAlternative
        VerifyAlternativeWithContext(
            MoneyRevenueCounterfactualProofRequest request,
            MoneyRevenueTerminalAlternativeProof proof,
            MoneyRevenueTerminalExecutionEvidence terminal)
    {
        Require(proof.RequestId == request.RequestId &&
                terminal.RequestId == request.RequestId &&
                terminal.ConcreteAlternativeId ==
                    request.ConcreteAlternativeId &&
                terminal.TerminalStartStateHash == request.TerminalStateHash &&
                terminal.ReservationSafetyVerified &&
                terminal.StrategyLedgerBoundaryArtifactsVerified &&
                terminal.ResourceNeutralRouteVerified &&
                terminal.NativeTerminalTransitionVerified &&
                terminal.RevenueRecurrenceVerified,
            "Money Revenue terminal evidence identity is invalid.");

        var recurrenceManifestPath = RequiredFile(
            proof.RevenueRecurrenceManifestPath,
            "Money Revenue comparison recurrence manifest");
        var recurrenceReceiptPath = RequiredFile(
            proof.RevenueRecurrenceReceiptPath,
            "Money Revenue comparison recurrence receipt");
        var expectedRecurrence = MoneyRevenueRecurrenceProofBuilder.Build(
            recurrenceManifestPath);
        var recurrence = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueRecurrenceProofReceipt>(
            recurrenceReceiptPath,
            "Money Revenue comparison recurrence receipt");
        Require(EqualJson(recurrence, expectedRecurrence) &&
                recurrence.ProofVerified &&
                recurrence.Prefix is not null &&
                terminal.RevenueRecurrenceManifestSha256 ==
                    Hash(recurrenceManifestPath) &&
                terminal.RevenueRecurrenceReceiptSha256 ==
                    Hash(recurrenceReceiptPath),
            "Money Revenue comparison recurrence is not exact.");
        var prefix = recurrence.Prefix!;
        var finalTransition = recurrence.Transitions.LastOrDefault() ??
            throw new InvalidDataException(
                "Money Revenue comparison recurrence is empty.");
        var finalSnapshot = finalTransition.Artifacts.SingleOrDefault(
            value => value.Role == "after_snapshot") ??
            throw new InvalidDataException(
                "Money Revenue comparison final snapshot is missing.");
        var finalSnapshotPath = RequiredFile(
            finalSnapshot.Path,
            "Money Revenue comparison final snapshot");
        Require(Hash(finalSnapshotPath) == finalSnapshot.Sha256,
            "Money Revenue comparison final snapshot hash is invalid.");
        var expectedRevenueStart = request.NativeShopSaleProofRequired
            ? terminal.TerminalActionBeforeStateHash
            : terminal.TerminalActionAfterStateHash;
        Require(prefix.InitialStateHash == expectedRevenueStart,
            "Money Revenue recurrence does not join its terminal execution.");

        var sourceSnapshotPath = RequiredFile(
            request.SourceSnapshotPath,
            "Money Revenue comparison source snapshot");
        var sourceSnapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            sourceSnapshotPath,
            "Money Revenue comparison source snapshot");
        var source = MoneyRevenueProgressionVerifier.Project(sourceSnapshot);
        Require(Hash(sourceSnapshotPath) == request.SourceSnapshotSha256 &&
                source.StateHash == request.SourceStateHash &&
                source.SaveId == prefix.SaveId &&
                source.PlayerId == prefix.PlayerId &&
                source.TotalMoneyEarned == prefix.InitialTotalMoneyEarned,
            "Money Revenue recurrence does not preserve its source checkpoint identity.");

        MoneyRevenueAlternativeArtifactEvidence? acquisition = null;
        AcquisitionOpportunityCostVector? acquisitionCost = null;
        var reservationNotRequired = true;
        var reservationIds = Array.Empty<string>();
        if (request.AcquisitionRolloutProofRequired)
        {
            acquisition = MoneyRevenueAlternativeComparisonProofBuilder
                .VerifyAlternative(
                    new MoneyRevenueAlternativeArtifactProof
                    {
                        AlternativeId = request.ConcreteAlternativeId,
                        AcquisitionRolloutProofManifestPath =
                            request.AcquisitionRolloutManifestPath,
                        AcquisitionRolloutProofReceiptPath =
                            request.AcquisitionRolloutReceiptPath,
                        RevenueRecurrenceProofManifestPath =
                            recurrenceManifestPath,
                        RevenueRecurrenceProofReceiptPath =
                            recurrenceReceiptPath
                    },
                    0);
            Require(acquisition.SourceStateHash == request.SourceStateHash &&
                    acquisition.SourceSnapshotSha256 ==
                        request.SourceSnapshotSha256 &&
                    acquisition.SourceStrategyLedgerSha256 ==
                        request.SourceStrategyLedgerSha256 &&
                    acquisition.SourceLedgerRevision ==
                        request.SourceStrategyLedgerRevision &&
                    acquisition.RevenueStartStateHash ==
                        prefix.InitialStateHash,
                "Money Revenue acquisition evidence drifted from its request.");
            acquisitionCost = acquisition.AggregateOpportunityCost;
            reservationNotRequired = acquisition.RouteTransitions.All(row =>
                row.ReservationNotRequired);
            reservationIds = acquisition.RouteTransitions
                .SelectMany(row => row.ReservationIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        else
        {
            Require(string.IsNullOrWhiteSpace(
                        request.AcquisitionRolloutManifestPath) &&
                    string.IsNullOrWhiteSpace(
                        request.AcquisitionRolloutReceiptPath) &&
                    request.TerminalStateHash == request.SourceStateHash,
                "Money Revenue direct alternative fabricated an acquisition rollout.");
        }

        var aggregate = acquisitionCost is null
            ? terminal.TerminalOpportunityCost
            : AcquisitionOpportunityCostVectorAggregator.Aggregate(
                new[]
                {
                    acquisitionCost,
                    terminal.TerminalOpportunityCost
                },
                acquisitionCost.EvidencePaths
                    .Concat(terminal.TerminalOpportunityCost.EvidencePaths));
        MoneyRevenueAlternativeComparer.ValidateOpportunityCost(aggregate);
        return new MoneyRevenueVerifiedComparisonAlternative(
            new MoneyRevenueComparisonAdmissionAlternative(
                request.RequestId,
                request.ConcreteAlternativeId,
                terminal.EvidenceSha256,
                terminal.NativeTerminalArtifacts,
                request.AcquisitionRolloutProofRequired,
                acquisition,
                prefix,
                acquisitionCost,
                terminal.TerminalOpportunityCost,
                aggregate,
                reservationNotRequired,
                reservationIds),
            finalSnapshotPath,
            finalSnapshot.Sha256,
            sourceSnapshotPath,
            request.SourceSnapshotSha256,
            source);
    }
}

internal sealed record MoneyRevenueVerifiedComparisonAlternative(
    MoneyRevenueComparisonAdmissionAlternative Alternative,
    string FinalSnapshotPath,
    string FinalSnapshotSha256,
    string SourceSnapshotPath,
    string SourceSnapshotSha256,
    MoneyRevenueCheckpoint SourceCheckpoint);
