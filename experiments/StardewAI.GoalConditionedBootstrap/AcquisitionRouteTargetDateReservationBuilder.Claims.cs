using System.Security.Cryptography;
using System.Text;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateReservationBuilder
{
    private static ReservationClaimBuildResult BuildClaims(
        AcquisitionRouteTargetDateCurrency route,
        string goalId,
        string stateHash,
        string decisionId,
        AcquisitionStrategyLedgerState ledgerState,
        AcquisitionResourceInputSnapshotState resources,
        AcquisitionShopQuoteSnapshotState currencies,
        AcquisitionResourceInputEvaluation[] positiveInputs,
        int requiredCurrency)
    {
        var material = BuildMaterialClaims(
            route,
            goalId,
            stateHash,
            decisionId,
            ledgerState,
            resources,
            positiveInputs);
        if (material.BlockingReasons.Length > 0 ||
            material.NonMatchingReasons.Length > 0)
        {
            return material;
        }
        var currency = BuildCurrencyClaims(
            route,
            goalId,
            stateHash,
            decisionId,
            ledgerState,
            currencies,
            requiredCurrency);
        return currency.BlockingReasons.Length > 0 ||
               currency.NonMatchingReasons.Length > 0
            ? currency
            : new ReservationClaimBuildResult(
                material.MaterialClaims,
                currency.CurrencyClaims,
                Array.Empty<string>(),
                Array.Empty<string>());
    }

    private static AcquisitionRouteReservationClaimSet ClaimSet(
        string stateHash,
        int ledgerRevision,
        string decisionId,
        MaterialReservation[] existingMaterial,
        CurrencyReservation[] existingCurrency,
        MaterialReservationUpsertRequest[] materialClaims,
        CurrencyReservationUpsertRequest[] currencyClaims,
        bool replacementRequired) => new(
            decisionId,
            stateHash,
            ledgerRevision,
            true,
            replacementRequired,
            existingMaterial.Select(row => row.ReservationId)
                .Concat(existingCurrency.Select(row => row.ReservationId))
                .Order(StringComparer.Ordinal)
                .ToArray(),
            materialClaims,
            currencyClaims);

    private static bool ExactMaterialClaims(
        MaterialReservation[] existing,
        MaterialReservationUpsertRequest[] claims)
    {
        if (existing.Length != claims.Length)
            return false;
        var byId = claims.ToDictionary(
            row => row.ReservationId,
            StringComparer.Ordinal);
        return existing.All(row =>
            byId.TryGetValue(row.ReservationId, out var claim) &&
            row.SourceDecisionId == claim.SourceDecisionId &&
            row.GoalId == claim.GoalId &&
            row.NodeId == claim.NodeId &&
            row.SlotIndex == claim.SlotIndex &&
            row.QualifiedItemId == claim.QualifiedItemId &&
            row.Quantity == claim.Quantity &&
            row.Purpose == claim.Purpose);
    }

    private static bool ExactCurrencyClaims(
        CurrencyReservation[] existing,
        CurrencyReservationUpsertRequest[] claims)
    {
        if (existing.Length != claims.Length)
            return false;
        var byId = claims.ToDictionary(
            row => row.ReservationId,
            StringComparer.Ordinal);
        return existing.All(row =>
            byId.TryGetValue(row.ReservationId, out var claim) &&
            row.SourceDecisionId == claim.SourceDecisionId &&
            row.GoalId == claim.GoalId &&
            row.CurrencyId == claim.CurrencyId &&
            row.Amount == claim.Amount &&
            row.Purpose == claim.Purpose);
    }

    private static string ReservationId(
        string routeOccurrenceId,
        string kind,
        int inputIndex,
        int claimIndex)
    {
        var hash = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(routeOccurrenceId)))
            .ToLowerInvariant();
        return "acquisition-route:" + hash + ":" + kind + ":" +
            inputIndex + ":" + claimIndex;
    }

    private static string SlotKey(string nodeId, int slotIndex) =>
        nodeId + "#" + slotIndex;

    private sealed record ReservationClaimBuildResult(
        MaterialReservationUpsertRequest[] MaterialClaims,
        CurrencyReservationUpsertRequest[] CurrencyClaims,
        string[] NonMatchingReasons,
        string[] BlockingReasons)
    {
        public static ReservationClaimBuildResult Empty { get; } = new(
            Array.Empty<MaterialReservationUpsertRequest>(),
            Array.Empty<CurrencyReservationUpsertRequest>(),
            Array.Empty<string>(),
            Array.Empty<string>());

        public static ReservationClaimBuildResult Conflict(string reason) => new(
            Array.Empty<MaterialReservationUpsertRequest>(),
            Array.Empty<CurrencyReservationUpsertRequest>(),
            new[] { reason },
            Array.Empty<string>());

        public static ReservationClaimBuildResult Blocked(
            params string[] reasons) => new(
            Array.Empty<MaterialReservationUpsertRequest>(),
            Array.Empty<CurrencyReservationUpsertRequest>(),
            Array.Empty<string>(),
            reasons.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }
}
