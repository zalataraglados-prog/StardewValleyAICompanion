using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioCommitReceiptBuilder
{
    private static List<string> ValidateAdmission(
        AcquisitionRoutePortfolioAdmission admission,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger baseLedger)
    {
        var reasons = new List<string>();
        if (admission.SchemaVersion !=
                "acquisition_route_portfolio_admission.v1" ||
            !admission.PortfolioAdmissionReady ||
            !admission.AtomicCommitPreflightPassed ||
            admission.FormalTrainingAuthorized ||
            admission.SelectedRouteOccurrenceIds is null ||
            admission.ReplacedRouteOccurrenceIds is null ||
            (admission.BlockingReasons?.Length ?? 0) != 0)
        {
            reasons.Add("portfolio_admission_not_receipt_eligible");
        }
        if (admission.SnapshotStateHash != snapshot.StateHash)
            reasons.Add("portfolio_receipt_snapshot_state_hash_mismatch");
        if (admission.StrategyLedgerRevision != baseLedger.Revision)
            reasons.Add("portfolio_receipt_base_ledger_revision_mismatch");
        if (admission.AtomicCommitRequired !=
            (admission.AtomicCommitRequest is not null))
        {
            reasons.Add("portfolio_admission_commit_requirement_invalid");
        }
        if (admission.PortfolioAdmissionReady &&
            (!admission.AtomicCommitRequired ||
                admission.AtomicCommitRequest is null))
        {
            reasons.Add("portfolio_ownership_commit_required");
        }
        return reasons;
    }

    private static bool ValidateExactClaims(
        AcquisitionRoutePortfolioAdmission admission,
        ExpectedPortfolioClaims expected,
        StrategyCommitmentLedger committed,
        ICollection<string> reasons)
    {
        var initialCount = reasons.Count;
        var activeMaterial = committed.MaterialReservations.Where(row =>
                row.Status == StrategyCommitmentStatuses.Active)
            .ToArray();
        var activeCurrency = committed.CurrencyReservations.Where(row =>
                row.Status == StrategyCommitmentStatuses.Active)
            .ToArray();
        foreach (var route in expected.Routes)
        {
            var actualIds = activeMaterial.Where(row =>
                    row.SourceDecisionId == route.SourceDecisionId)
                .Select(row => row.ReservationId)
                .Concat(activeCurrency.Where(row =>
                        row.SourceDecisionId == route.SourceDecisionId)
                    .Select(row => row.ReservationId))
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (!actualIds.SequenceEqual(route.ReservationIds,
                    StringComparer.Ordinal))
            {
                reasons.Add("selected_route_active_claim_set_mismatch:" +
                    route.SourceDecisionId[
                        AcquisitionRoutePortfolioBuilder.RouteDecisionPrefix
                            .Length..]);
            }
        }
        foreach (var routeId in admission.ReplacedRouteOccurrenceIds ??
            Array.Empty<string>())
        {
            var decisionId =
                AcquisitionRoutePortfolioBuilder.RouteDecisionPrefix +
                routeId;
            if (activeMaterial.Any(row =>
                    row.SourceDecisionId == decisionId) ||
                activeCurrency.Any(row =>
                    row.SourceDecisionId == decisionId))
            {
                reasons.Add("replaced_route_still_has_active_claims:" + routeId);
            }
        }
        foreach (var claim in expected.MaterialClaims)
        {
            var row = activeMaterial.SingleOrDefault(value =>
                value.ReservationId == claim.ReservationId);
            if (row is null ||
                !ReservationPortfolioCommitEvidenceVerifier.Exact(
                    row,
                    claim,
                    committed.PlayerId))
            {
                reasons.Add("material_claim_not_exactly_committed:" +
                    claim.ReservationId);
            }
        }
        foreach (var claim in expected.CurrencyClaims)
        {
            var row = activeCurrency.SingleOrDefault(value =>
                value.ReservationId == claim.ReservationId);
            if (row is null ||
                !ReservationPortfolioCommitEvidenceVerifier.Exact(
                    row,
                    claim,
                    committed.PlayerId))
            {
                reasons.Add("currency_claim_not_exactly_committed:" +
                    claim.ReservationId);
            }
        }
        if (admission.AtomicCommitRequest is not null)
        {
            ReservationPortfolioCommitEvidenceVerifier.ReleasesCancelled(
                admission.AtomicCommitRequest,
                committed,
                reasons);
        }
        return reasons.Count == initialCount;
    }
}
