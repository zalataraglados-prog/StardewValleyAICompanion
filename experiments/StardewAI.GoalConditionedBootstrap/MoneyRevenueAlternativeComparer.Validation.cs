using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class MoneyRevenueAlternativeComparer
{
    private static VerifiedAlternative[] ValidateDenominator(
        uint target,
        string sourceStateHash,
        int expectedLedgerRevision,
        IReadOnlyList<string> expectedAlternativeIds,
        IReadOnlyList<MoneyRevenueAlternativeCandidate> alternatives)
    {
        if (string.IsNullOrWhiteSpace(sourceStateHash) ||
            expectedLedgerRevision < 0 ||
            expectedAlternativeIds is null ||
            alternatives is null)
        {
            throw new InvalidDataException(
                "Money revenue comparison request identity is invalid.");
        }
        var expectedIds = expectedAlternativeIds.ToArray();
        var rows = alternatives.ToArray();
        Require(expectedIds.Length >= 2 &&
                expectedIds.All(value => !string.IsNullOrWhiteSpace(value)) &&
                expectedIds.Distinct(StringComparer.Ordinal).Count() ==
                    expectedIds.Length &&
                rows.Length == expectedIds.Length &&
                rows.All(value => value is not null) &&
                rows.Select(value => value.AlternativeId)
                    .Order(StringComparer.Ordinal)
                    .SequenceEqual(
                        expectedIds.Order(StringComparer.Ordinal),
                        StringComparer.Ordinal),
            "Money revenue comparison candidate denominator is incomplete.");

        var verified = rows.Select(candidate => ValidateCandidate(
                candidate,
                target,
                sourceStateHash,
                expectedLedgerRevision))
            .OrderBy(candidate => candidate.AlternativeId,
                StringComparer.Ordinal)
            .ToArray();
        var anchor = verified[0].Prefix;
        Require(verified.All(candidate =>
                candidate.Prefix.SaveId == anchor.SaveId &&
                candidate.Prefix.PlayerId == anchor.PlayerId &&
                candidate.Prefix.InitialStateHash == anchor.InitialStateHash &&
                candidate.Prefix.InitialTotalDay == anchor.InitialTotalDay &&
                candidate.Prefix.InitialMoney == anchor.InitialMoney &&
                candidate.Prefix.InitialTotalMoneyEarned ==
                    anchor.InitialTotalMoneyEarned),
            "Money revenue comparison alternatives do not share one source state.");
        return verified;
    }

    private static VerifiedAlternative ValidateCandidate(
        MoneyRevenueAlternativeCandidate candidate,
        uint target,
        string sourceStateHash,
        int expectedLedgerRevision)
    {
        if (string.IsNullOrWhiteSpace(candidate.AlternativeId) ||
            candidate.RevenuePrefix is null ||
            candidate.OpportunityCost is null)
        {
            throw new InvalidDataException(
                "Money revenue comparison candidate is incomplete.");
        }
        var opportunityCost = candidate.OpportunityCost;
        var prefix = MoneyRevenueRecurrenceVerifier.VerifyPrefix(
            candidate.RevenuePrefix.Transitions);
        Require(prefix.InitialStateHash == sourceStateHash,
            "Money revenue comparison prefix source state drifted.");
        ValidateOpportunityCost(opportunityCost);
        var reservationIds = ValidateReservation(
            candidate,
            sourceStateHash,
            expectedLedgerRevision);
        var before = Math.Min(prefix.InitialTotalMoneyEarned, target);
        var after = Math.Min(prefix.FinalTotalMoneyEarned, target);
        return new VerifiedAlternative(
            candidate.AlternativeId,
            prefix,
            opportunityCost,
            candidate.ReservationNotRequired,
            reservationIds,
            prefix.FinalTotalMoneyEarned >= target,
            checked(after - before));
    }

    private static void ValidateOpportunityCost(
        AcquisitionOpportunityCostVector vector)
    {
        var materials = vector.MaterialCosts ??
            Array.Empty<AcquisitionOpportunityMaterialCost>();
        var currencies = vector.CurrencyCosts ??
            Array.Empty<AcquisitionOpportunityCurrencyCost>();
        Require(vector.GuaranteedElapsedGameMinutes >= 0 &&
                double.IsFinite(vector.RequiredEnergy) &&
                vector.RequiredEnergy >= 0d &&
                vector.MaterialTotalSaleValue >= 0 &&
                vector.EvidencePaths is { Length: > 0 } &&
                vector.EvidencePaths.All(path =>
                    !string.IsNullOrWhiteSpace(path)) &&
                materials.All(row =>
                    !string.IsNullOrWhiteSpace(row.QualifiedItemId) &&
                    row.Quality >= 0 &&
                    row.UnitSalePrice >= 0 &&
                    row.Quantity > 0 &&
                    row.TotalSaleValue == checked(
                        row.UnitSalePrice * row.Quantity)) &&
                materials.Select(row => new
                    {
                        row.QualifiedItemId,
                        row.Quality,
                        row.UnitSalePrice
                    }).Distinct().Count() == materials.Length &&
                currencies.All(row =>
                    NativeShopCurrencies.TryGetKey(
                        row.CurrencyId,
                        out var currencyKey) &&
                    row.CurrencyKey == currencyKey &&
                    row.Amount > 0) &&
                currencies.Select(row => row.CurrencyId)
                    .Distinct().Count() == currencies.Length &&
                vector.MaterialTotalSaleValue == checked(
                    materials.Sum(row => row.TotalSaleValue)),
            "Money revenue comparison opportunity-cost vector is invalid.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record VerifiedAlternative(
        string AlternativeId,
        MoneyRevenueRecurrencePrefixEvidence Prefix,
        AcquisitionOpportunityCostVector OpportunityCost,
        bool ReservationNotRequired,
        string[] ReservationIds,
        bool TargetSatisfied,
        uint ProgressTowardTarget);
}
