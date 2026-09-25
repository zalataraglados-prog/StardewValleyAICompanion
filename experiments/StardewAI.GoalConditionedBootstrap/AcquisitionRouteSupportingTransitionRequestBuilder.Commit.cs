using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionRequestBuilder
{
    private static ReservationPortfolioCommitRequest BuildCommitRequest(
        string goalId,
        string supportRequestId,
        string stateHash,
        StrategyCommitmentLedger ledger,
        AcquisitionRouteTargetDateReservation reservation)
    {
        var claims = reservation.ClaimSet ?? throw new InvalidDataException(
            "Support reservation claim set is missing.");
        var includeClaims = reservation.ClaimDisposition is
            "claim_proposed" or "claim_replacement_required";
        var material = includeClaims
            ? claims.MaterialClaims.OrderBy(value => value.ReservationId,
                StringComparer.Ordinal).ToArray()
            : Array.Empty<MaterialReservationUpsertRequest>();
        var currency = includeClaims
            ? claims.CurrencyClaims.OrderBy(value => value.ReservationId,
                StringComparer.Ordinal).ToArray()
            : Array.Empty<CurrencyReservationUpsertRequest>();
        var desired = claims.MaterialClaims.Select(value => value.ReservationId)
            .Concat(claims.CurrencyClaims.Select(value => value.ReservationId))
            .ToHashSet(StringComparer.Ordinal);
        var releases = reservation.ClaimDisposition ==
                "claim_replacement_required"
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
            CurrencyClaims = currency
        };
    }

    private static string SupportRequestId(
        string routeOccurrenceId,
        string stateHash) =>
        "acquisition-support:" + routeOccurrenceId + ":" + stateHash;
}
