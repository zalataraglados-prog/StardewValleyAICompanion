using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioCommitReceiptBuilder
{
    public static AcquisitionRoutePortfolioCommitReceipt Build(
        AcquisitionRoutePortfolioInputs inputs,
        string admissionPath,
        string committedLedgerPath,
        string? commitResultPath)
    {
        var recomputed = AcquisitionRoutePortfolioBuilder.Build(inputs);
        return BuildVerifiedAdmission(
            inputs,
            admissionPath,
            committedLedgerPath,
            commitResultPath,
            recomputed);
    }

    private static AcquisitionRoutePortfolioCommitReceipt
        BuildVerifiedAdmission(
            AcquisitionRoutePortfolioInputs inputs,
            string admissionPath,
            string committedLedgerPath,
            string? commitResultPath,
            AcquisitionRoutePortfolioAdmission expectedAdmission)
    {
        var admissionFullPath = Path.GetFullPath(admissionPath);
        var baseLedgerPath = Path.GetFullPath(inputs.StrategyLedgerPath);
        var committedLedgerFullPath = Path.GetFullPath(committedLedgerPath);
        var snapshotPath = Path.GetFullPath(inputs.SnapshotPath);
        var admission = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioAdmission>(
            admissionFullPath,
            "Acquisition route portfolio admission");
        Require(EqualJson(admission, expectedAdmission),
            "Route portfolio admission drifted from deterministic source compilation.");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotPath,
            "Acquisition route portfolio snapshot");
        using var snapshotDocument = JsonDocument.Parse(
            File.ReadAllText(snapshotPath));
        var baseLedger = AcquisitionStrategyLedgerReader.Read(
            baseLedgerPath,
            snapshotDocument.RootElement).Ledger;
        var committedLedger = AcquisitionStrategyLedgerReader.Read(
            committedLedgerFullPath,
            snapshotDocument.RootElement).Ledger;
        var opportunity = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateOpportunityCostReport>(
            inputs.TargetDateOpportunityCostPath,
            "Acquisition route target-date opportunity cost");

        var reasons = ValidateAdmission(admission, snapshot, baseLedger);
        var expected = ExpectedClaims(
            opportunity,
            admission.SelectedRouteOccurrenceIds ?? Array.Empty<string>(),
            reasons);
        var exactClaims = ValidateExactClaims(
            admission,
            expected,
            committedLedger,
            reasons);
        var mutationObserved = admission.AtomicCommitRequired &&
            admission.AtomicCommitRequest is not null;
        var singleRevision = false;
        if (mutationObserved)
        {
            singleRevision = ValidateMutation(
                admission,
                baseLedger,
                committedLedger,
                snapshot,
                commitResultPath,
                reasons);
        }
        else
        {
            reasons.Add("portfolio_ownership_commit_required");
        }

        var blocking = reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var verified = blocking.Length == 0 && exactClaims && singleRevision;
        var request = admission.AtomicCommitRequest;
        return new AcquisitionRoutePortfolioCommitReceipt
        {
            Status = verified
                ? "verified_atomic_reservation_portfolio_commit"
                : "blocked_reservation_portfolio_commit_receipt",
            ProposalId = admission.ProposalId,
            PortfolioId = request?.PortfolioId ??
                "target-date-acquisition-portfolio:" + admission.ProposalId,
            GoalId = admission.GoalId,
            SnapshotStateHash = admission.SnapshotStateHash,
            CommunityCenterProvenance =
                AcquisitionRouteCommunityCenterProvenanceSupport.Clone(
                    admission.CommunityCenterProvenance),
            PriorRolloutCheckpointSha256 =
                admission.PriorRolloutCheckpointSha256,
            CompletedAlternatives = (admission.CompletedAlternatives ??
                    Array.Empty<
                        AcquisitionRoutePortfolioCompletedAlternatives>())
                .Where(row => row is not null)
                .Select(AcquisitionRoutePortfolioBuilder
                    .CloneCompletedAlternatives)
                .ToArray(),
            BaseLedgerRevision = baseLedger.Revision,
            CommittedLedgerRevision = committedLedger.Revision,
            PortfolioAdmissionSha256 =
                CurrentTeacherFrontierSupport.HashFile(admissionFullPath),
            BaseLedgerSha256 =
                CurrentTeacherFrontierSupport.HashFile(baseLedgerPath),
            CommittedLedgerSha256 =
                CurrentTeacherFrontierSupport.HashFile(
                    committedLedgerFullPath),
            CommitResultSha256 = string.IsNullOrWhiteSpace(commitResultPath)
                ? string.Empty
                : CurrentTeacherFrontierSupport.HashFile(
                    Path.GetFullPath(commitResultPath)),
            SelectedRouteOccurrenceIds =
                admission.SelectedRouteOccurrenceIds?.ToArray() ??
                Array.Empty<string>(),
            ActiveReservationIds = expected.MaterialClaims
                .Select(claim => claim.ReservationId)
                .Concat(expected.CurrencyClaims.Select(claim =>
                    claim.ReservationId))
                .Order(StringComparer.Ordinal)
                .ToArray(),
            ReleasedReservationIds = request?.ReleaseReservationIds
                .Order(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>(),
            AtomicMutationObserved = mutationObserved,
            ExactActiveClaimSetVerified = exactClaims,
            SingleRevisionCommitVerified = singleRevision,
            PortfolioCommitVerified = verified,
            FormalTrainingAuthorized = false,
            BlockingReasons = blocking
        };
    }

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Options),
        JsonSerializer.Serialize(right, JsonDefaults.Options),
        StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
