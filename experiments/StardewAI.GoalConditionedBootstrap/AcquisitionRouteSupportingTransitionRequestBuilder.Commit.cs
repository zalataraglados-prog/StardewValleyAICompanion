using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionRequestBuilder
{
    private static ReservationPortfolioCommitRequest BuildCommitRequest(
        string goalId,
        string supportRequestId,
        string stateHash,
        StrategyCommitmentLedger ledger,
        AcquisitionRouteTargetDateReservation reservation,
        MachineSupportIntentUpsertRequest? machineSupportIntent = null)
    {
        var claims = reservation.ClaimSet;
        var includeClaims = reservation.ClaimDisposition is
            "claim_proposed" or "claim_replacement_required";
        var material = includeClaims && claims is not null
            ? claims.MaterialClaims.OrderBy(value => value.ReservationId,
                StringComparer.Ordinal).ToArray()
            : Array.Empty<MaterialReservationUpsertRequest>();
        var currency = includeClaims && claims is not null
            ? claims.CurrencyClaims.OrderBy(value => value.ReservationId,
                StringComparer.Ordinal).ToArray()
            : Array.Empty<CurrencyReservationUpsertRequest>();
        var desired = (claims?.MaterialClaims ??
                Array.Empty<MaterialReservationUpsertRequest>())
            .Select(value => value.ReservationId)
            .Concat((claims?.CurrencyClaims ??
                Array.Empty<CurrencyReservationUpsertRequest>())
                .Select(value => value.ReservationId))
            .ToHashSet(StringComparer.Ordinal);
        var releases = reservation.ClaimDisposition ==
                "claim_replacement_required" && claims is not null
            ? claims.ExistingActiveReservationIds
                .Where(value => !desired.Contains(value))
                .Order(StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();
        return new ReservationPortfolioCommitRequest
        {
            StateHash = stateHash,
            ExpectedLedgerRevision = ledger.Revision,
            PortfolioId = supportRequestId,
            GoalId = goalId,
            SourceDecisionId = supportRequestId,
            ReleaseReservationIds = releases,
            MaterialClaims = material,
            CurrencyClaims = currency,
            MachineSupportIntent = machineSupportIntent
        };
    }

    private static string SupportRequestId(
        string routeOccurrenceId,
        string stateHash) =>
        "acquisition-support:" + routeOccurrenceId + ":" + stateHash;
}
