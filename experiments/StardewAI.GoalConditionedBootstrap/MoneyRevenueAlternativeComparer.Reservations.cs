using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class MoneyRevenueAlternativeComparer
{
    private static string[] ValidateReservation(
        MoneyRevenueAlternativeCandidate candidate,
        string sourceStateHash,
        int expectedLedgerRevision) => ValidateReservation(
        candidate.OpportunityCost,
        candidate.ReservationNotRequired,
        candidate.ReservationClaimSet,
        sourceStateHash,
        expectedLedgerRevision,
        "goal.economy.earn_money");

    internal static string[] ValidateReservation(
        AcquisitionOpportunityCostVector vector,
        bool reservationNotRequired,
        AcquisitionRouteReservationClaimSet? reservationClaimSet,
        string sourceStateHash,
        int expectedLedgerRevision,
        string expectedGoalId)
    {
        var vectorMaterials = vector.MaterialCosts ??
            Array.Empty<AcquisitionOpportunityMaterialCost>();
        var vectorCurrencies = vector.CurrencyCosts ??
            Array.Empty<AcquisitionOpportunityCurrencyCost>();
        if (reservationNotRequired)
        {
            Require(reservationClaimSet is null &&
                    vectorMaterials.Length == 0 &&
                    vectorCurrencies.Length == 0,
                "Reservation-free revenue alternative has reserved costs.");
            return Array.Empty<string>();
        }

        var claims = reservationClaimSet ??
            throw new InvalidDataException(
                "Reserve-aware revenue alternative lacks a claim set.");
        var materials = claims.MaterialClaims ??
            Array.Empty<MaterialReservationUpsertRequest>();
        var currencies = claims.CurrencyClaims ??
            Array.Empty<CurrencyReservationUpsertRequest>();
        var activeIds = claims.ExistingActiveReservationIds ??
            Array.Empty<string>();
        Require(claims.AtomicCommitRequired &&
                !claims.ReplacementRequired &&
                claims.SourceStateHash == sourceStateHash &&
                claims.ExpectedLedgerRevision == expectedLedgerRevision &&
                !string.IsNullOrWhiteSpace(claims.SourceDecisionId) &&
                activeIds.All(id => !string.IsNullOrWhiteSpace(id)) &&
                activeIds.Distinct(StringComparer.Ordinal).Count() ==
                    activeIds.Length &&
                materials.Length + currencies.Length > 0 &&
                materials.All(claim =>
                    ValidClaimIdentity(
                        claim.StateHash,
                        claim.ExpectedLedgerRevision,
                        claim.SourceDecisionId,
                        claim.GoalId,
                        claim.ReservationId,
                        claim.Purpose,
                        claims,
                        sourceStateHash,
                        expectedLedgerRevision,
                        expectedGoalId) &&
                    !string.IsNullOrWhiteSpace(claim.NodeId) &&
                    claim.SlotIndex >= 0 &&
                    !string.IsNullOrWhiteSpace(claim.QualifiedItemId) &&
                    claim.Quantity > 0) &&
                currencies.All(claim =>
                    ValidClaimIdentity(
                        claim.StateHash,
                        claim.ExpectedLedgerRevision,
                        claim.SourceDecisionId,
                        claim.GoalId,
                        claim.ReservationId,
                        claim.Purpose,
                        claims,
                        sourceStateHash,
                        expectedLedgerRevision,
                        expectedGoalId) &&
                    NativeShopCurrencies.TryGetKey(
                        claim.CurrencyId,
                        out _) &&
                    claim.Amount > 0),
            "Money revenue comparison reservation claims are invalid.");
        var reservationIds = materials.Select(claim => claim.ReservationId)
            .Concat(currencies.Select(claim => claim.ReservationId))
            .ToArray();
        Require(reservationIds.Distinct(StringComparer.Ordinal).Count() ==
                    reservationIds.Length &&
                SameMaterialQuantities(materials, vectorMaterials) &&
                SameCurrencyAmounts(currencies, vectorCurrencies),
            "Money revenue comparison claims and cost vector disagree.");
        return reservationIds.Order(StringComparer.Ordinal).ToArray();
    }

    private static bool ValidClaimIdentity(
        string stateHash,
        int ledgerRevision,
        string sourceDecisionId,
        string goalId,
        string reservationId,
        string purpose,
        AcquisitionRouteReservationClaimSet set,
        string expectedStateHash,
        int expectedLedgerRevision,
        string expectedGoalId) =>
        stateHash == expectedStateHash &&
        ledgerRevision == expectedLedgerRevision &&
        sourceDecisionId == set.SourceDecisionId &&
        goalId == expectedGoalId &&
        !string.IsNullOrWhiteSpace(reservationId) &&
        !string.IsNullOrWhiteSpace(purpose);

    private static bool SameMaterialQuantities(
        MaterialReservationUpsertRequest[] claims,
        AcquisitionOpportunityMaterialCost[] costs) =>
        Group(claims.Select(row =>
                (row.QualifiedItemId, row.Quantity)))
            .OrderBy(row => row.Key, StringComparer.Ordinal)
            .SequenceEqual(Group(costs.Select(row =>
                    (row.QualifiedItemId, row.Quantity)))
                .OrderBy(row => row.Key, StringComparer.Ordinal));

    private static bool SameCurrencyAmounts(
        CurrencyReservationUpsertRequest[] claims,
        AcquisitionOpportunityCurrencyCost[] costs) =>
        claims.GroupBy(row => row.CurrencyId)
            .ToDictionary(group => group.Key, group =>
                checked(group.Sum(row => row.Amount)))
            .OrderBy(row => row.Key)
            .SequenceEqual(costs.ToDictionary(
                    row => row.CurrencyId,
                    row => row.Amount)
                .OrderBy(row => row.Key));

    private static Dictionary<string, int> Group(
        IEnumerable<(string Key, int Quantity)> values) =>
        values.GroupBy(value => value.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => checked(group.Sum(value => value.Quantity)),
                StringComparer.Ordinal);
}
