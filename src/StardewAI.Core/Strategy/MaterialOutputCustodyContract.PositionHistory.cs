using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public static partial class MaterialOutputCustodyContract
{
    public static string ComputePositionTransitionSha256(StrategyCommitmentLedger ledger,
        StrategyCommitmentHistoryEntry entry, MaterialOutputCustodyPositionTransition value) => CustodyTextHash(JsonSerializer.Serialize(new
    {
        ledger.LedgerId, ledger.SaveId, ledger.PlayerId, entry.CommitmentId, entry.CommitmentRevision,
        entry.LedgerRevision, entry.SourceDecisionId, entry.SourceStateHash, entry.Operation, entry.Reason,
        value.SchemaVersion, value.InitialPositionSha256, value.PreviousPositionSha256,
        value.BeforeNodeId, value.BeforeSlotIndex, value.BeforeStateHash, value.BeforeReservationRevision,
        value.AfterNodeId, value.AfterSlotIndex, value.AfterStateHash, value.AfterReservationRevision,
        value.PreparedPortfolioId, value.PreparedRouteSourceDecisionId, value.PreparedLedgerRevision,
        value.PreparedSourceStateHash, value.PreparedTransferSha256, value.SupportingTransitionReceiptSha256
    }));

    private static bool PositionMatches(MaterialReservation row, StrategyCommitmentLedger ledger,
        StrategyCommitmentHistoryEntry creation, out string binding)
    {
        binding = string.Empty;
        var initial = row.OutputCustody!.InitialPosition;
        // Legacy consumer authority does not manufacture a retained location origin.
        if (initial is null) return creation.OutputCustodyInitialPositionSha256 is null &&
            row.NodeId == "player:" + ledger.PlayerId && row.SlotIndex >= 0 && row.Revision == creation.CommitmentRevision &&
            !string.IsNullOrWhiteSpace(creation.SourceStateHash) && row.SourceStateHash == creation.SourceStateHash &&
            !ledger.History.Any(entry => entry.CommitmentId == row.ReservationId && entry.OutputCustodyPositionTransition is not null);
        if (!InitialPositionBound(row, ledger, creation)) return false;
        if (ledger.History.Any(entry => entry.CommitmentId == row.ReservationId &&
            entry.OutputCustodyPositionTransition is not null && entry.LedgerRevision <= creation.LedgerRevision)) return false;
        var node = initial.NodeId;
        var slot = initial.SlotIndex;
        var stateHash = initial.SourceStateHash;
        var revision = initial.ReservationRevision;
        var ledgerRevision = creation.LedgerRevision;
        var previous = initial.BindingSha256;
        var links = ledger.History.Where(value => value.CommitmentId == row.ReservationId &&
            value.LedgerRevision > creation.LedgerRevision).OrderBy(value => value.LedgerRevision).ToArray();
        var markers = links.Length == 0 ? null : PositionMarkers(ledger);
        foreach (var entry in links)
        {
            var value = entry.OutputCustodyPositionTransition;
            if (value is null || value.SchemaVersion != "material_output_custody_position_transition.v1" ||
                entry.LedgerRevision <= ledgerRevision || entry.LedgerRevision > ledger.Revision ||
                entry.SourceDecisionId != row.SourceDecisionId || value.InitialPositionSha256 != initial.BindingSha256 ||
                value.PreviousPositionSha256 != previous || value.BeforeNodeId != node || value.BeforeSlotIndex != slot ||
                value.BeforeStateHash != stateHash || value.BeforeReservationRevision != revision ||
                (long)value.AfterReservationRevision != (long)revision + 1 || entry.CommitmentRevision != value.AfterReservationRevision ||
                value.AfterSlotIndex < 0 || string.IsNullOrWhiteSpace(value.AfterStateHash) || entry.SourceStateHash != value.AfterStateHash ||
                value.AfterStateHash == value.PreparedSourceStateHash ||
                !PositionOperationMatches(entry.Operation, node, value.AfterNodeId, ledger.PlayerId) ||
                !IsLowerSha256(value.BindingSha256) || value.BindingSha256 != ComputePositionTransitionSha256(ledger, entry, value) ||
                !PositionMarkersMatch(markers!, entry, value)) return false;
            node = value.AfterNodeId;
            slot = value.AfterSlotIndex;
            stateHash = value.AfterStateHash;
            revision = value.AfterReservationRevision;
            ledgerRevision = entry.LedgerRevision;
            previous = value.BindingSha256;
        }
        if (row.NodeId != node || row.SlotIndex != slot || row.SourceStateHash != stateHash || row.Revision != revision) return false;
        binding = previous;
        return true;
    }

    private static bool PositionOperationMatches(string operation, string before, string? after, string playerId)
    {
        var player = "player:" + playerId;
        bool Chest(string? node) => node is not null && node.StartsWith("chest:", StringComparison.Ordinal) && node.Length > 6;
        return operation == "material_reservation_relocated"
            ? before == player && Chest(after) || Chest(before) && after == player
            : operation == "material_reservation_rebound_after_native_transfer" && before == after && (before == player || Chest(before));
    }

    private static ILookup<(string Id, string Source, string Operation, int Revision, string State, string Reason), StrategyCommitmentHistoryEntry>
        PositionMarkers(StrategyCommitmentLedger ledger) => ledger.History.Where(marker =>
            marker.CommitmentRevision == marker.LedgerRevision && marker.Operation is
                "material_transfer_prepared" or "material_transfer_preparation_completed" or "reservation_portfolio_supporting_transition_complete")
            .ToLookup(marker => (marker.CommitmentId, marker.SourceDecisionId, marker.Operation,
                marker.LedgerRevision, marker.SourceStateHash, marker.Reason));

    private static bool PositionMarkersMatch(
        ILookup<(string Id, string Source, string Operation, int Revision, string State, string Reason), StrategyCommitmentHistoryEntry> markers,
        StrategyCommitmentHistoryEntry entry,
        MaterialOutputCustodyPositionTransition value) =>
        !string.IsNullOrWhiteSpace(value.PreparedPortfolioId) && !string.IsNullOrWhiteSpace(value.PreparedRouteSourceDecisionId) &&
        !string.IsNullOrWhiteSpace(value.PreparedSourceStateHash) && value.PreparedLedgerRevision > 0 &&
        (long)value.PreparedLedgerRevision + 1 == entry.LedgerRevision &&
        IsLowerSha256(value.PreparedTransferSha256) && IsLowerSha256(value.SupportingTransitionReceiptSha256) &&
        markers[(value.PreparedPortfolioId, value.PreparedRouteSourceDecisionId, "material_transfer_prepared",
            value.PreparedLedgerRevision, value.PreparedSourceStateHash, value.PreparedTransferSha256)].Count() == 1 &&
        markers[(value.PreparedPortfolioId, value.PreparedRouteSourceDecisionId, "material_transfer_preparation_completed",
            entry.LedgerRevision, value.AfterStateHash, value.PreparedTransferSha256)].Count() == 1 &&
        markers[(value.PreparedPortfolioId, value.PreparedRouteSourceDecisionId, "reservation_portfolio_supporting_transition_complete",
            entry.LedgerRevision, value.AfterStateHash, value.SupportingTransitionReceiptSha256)].Count() == 1;
}
