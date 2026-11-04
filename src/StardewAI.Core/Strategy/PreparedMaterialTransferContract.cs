using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public static class PreparedMaterialTransferContract
{
    public const string BindingParameter = "prepared_material_transfer_sha256";

    public static string ComputeBindingSha256(PreparedMaterialTransfer value) => TextHash(JsonSerializer.Serialize(new
    {
        value.SchemaVersion, value.PortfolioId, value.GoalId, value.RouteSourceDecisionId,
        value.SourceStateHash, value.SourceGameTick, value.ExpectedLedgerRevision, value.MaterialReservationsSha256,
        value.Intent, value.MaterialRelocations, value.BeforeInventoryGraph, value.ReservationProjection
    }));

    public static string ComputeMaterialReservationsSha256(StrategyCommitmentLedger ledger) =>
        TextHash(JsonSerializer.Serialize(ledger.MaterialReservations));

    public static bool IsBound(StrategyCommitmentLedger ledger, string? bindingSha256,
        string? portfolioId, string? goalId, string? routeSourceDecisionId)
    {
        var value = ledger.PreparedMaterialTransfer;
        return bindingSha256 is { Length: 64 } && bindingSha256.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') &&
            value is not null && value.SchemaVersion == "prepared_material_transfer.v1" &&
            value.Intent is not null && value.BeforeInventoryGraph is not null && value.ReservationProjection is not null &&
            value.MaterialRelocations is { Length: > 0 } &&
            value.ReservationProjection.Status == "projected" && value.ReservationProjection.LedgerId == ledger.LedgerId &&
            value.ReservationProjection.ExpectedLedgerRevision == ledger.Revision &&
            value.ReservationProjection.ReservationTransitions is { Length: > 0 } &&
            !string.IsNullOrWhiteSpace(portfolioId) && !string.IsNullOrWhiteSpace(goalId) &&
            !string.IsNullOrWhiteSpace(routeSourceDecisionId) &&
            value.PortfolioId == portfolioId && value.GoalId == goalId && value.RouteSourceDecisionId == routeSourceDecisionId &&
            value.BindingSha256 == bindingSha256 && value.BindingSha256 == ComputeBindingSha256(value) &&
            value.ExpectedLedgerRevision == ledger.Revision && value.SourceStateHash == ledger.SourceStateHash &&
            value.MaterialReservationsSha256 == ComputeMaterialReservationsSha256(ledger) &&
            (ledger.History ?? Array.Empty<StrategyCommitmentHistoryEntry>()).Count(row =>
                row.CommitmentId == value.PortfolioId && row.SourceDecisionId == value.RouteSourceDecisionId &&
                row.Operation == "material_transfer_prepared" && row.LedgerRevision == value.ExpectedLedgerRevision &&
                row.CommitmentRevision == value.ExpectedLedgerRevision && row.Reason == bindingSha256) == 1;
    }

    public static bool ContainsCustody(StrategyCommitmentLedger ledger, MaterialTransferReservationProjection projection) =>
        ledger.MaterialReservations.Any(row => row.OutputCustody is not null &&
            projection.ReservationTransitions.Any(change => change.ReservationId == row.ReservationId));

    public static bool CustodyPositionsBound(StrategyCommitmentLedger ledger, MaterialTransferReservationProjection projection)
    {
        var affected = projection.ReservationTransitions.Select(change => change.ReservationId).ToHashSet(StringComparer.Ordinal);
        return ledger.MaterialReservations.Where(row => row.OutputCustody is not null && affected.Contains(row.ReservationId))
            .All(row => MaterialOutputCustodyContract.TryGetPositionBinding(row, ledger, out _));
    }

    private static string TextHash(string json)
    {
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(json)).Select(item => item.ToString("x2")));
    }
}
