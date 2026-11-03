using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public sealed partial class ReservationPortfolioLedgerService
{
    private static void ValidateOutputCustodyClaims(StrategyCommitmentLedger? current, SnapshotEnvelope snapshot,
        ReservationPortfolioRouteSettlementRequest request, ICollection<string> errors)
    {
        if (errors.Count > 0) return;
        var claims = request.OutputCustodyClaims;
        if (claims is null || claims.Length == 0) return;
        // The supported museum route produces one exact one-item obligation.
        if (claims.Length != 1 || claims.Any(claim => claim is null ||
            claim.ConsumerOptionId != MaterialOutputCustodyContract.MuseumConsumer ||
            claim.Quantity != 1 || claim.SlotIndex < 0 || string.IsNullOrWhiteSpace(claim.QualifiedItemId) ||
            !claim.QualifiedItemId.StartsWith("(O)", StringComparison.Ordinal) ||
            claim.NodeId != "player:" + current?.PlayerId))
        {
            errors.Add("output_custody_claim_invalid");
            return;
        }
        var claim = claims[0];
        if (!MaterialOutputCustodyContract.TryMuseumDonated(snapshot, claim.QualifiedItemId, out var donated) || donated)
            errors.Add("output_custody_exact_undonated_museum_item_required");
        var portfolioId = MaterialOutputCustodyContract.PortfolioId(request.PortfolioId, request.RouteSourceDecisionId,
            claim.ConsumerOptionId, claim.QualifiedItemId);
        var reservationId = MaterialOutputCustodyContract.ReservationId(portfolioId);
        if (current is null || current.MaterialReservations.Any(row => row.ReservationId == reservationId) ||
            current.CurrencyReservations.Any(row => row.ReservationId == reservationId) ||
            current.History.Any(row => row.CommitmentId == portfolioId))
            errors.Add("output_custody_identity_already_used");
    }

    private ReservationPortfolioCommitResult RetainOutputCustody(StrategyCommitmentLedger staged,
        SnapshotEnvelope snapshot, ReservationPortfolioRouteSettlementRequest request, string updatedAt)
    {
        var claim = request.OutputCustodyClaims!.Single();
        var portfolioId = MaterialOutputCustodyContract.PortfolioId(request.PortfolioId, request.RouteSourceDecisionId,
            claim.ConsumerOptionId, claim.QualifiedItemId);
        var reservationId = MaterialOutputCustodyContract.ReservationId(portfolioId);
        var result = Commit(staged, snapshot, new ReservationPortfolioCommitRequest
        {
            StateHash = request.StateHash, ExpectedLedgerRevision = staged.Revision,
            PortfolioId = portfolioId, SourceDecisionId = portfolioId, GoalId = request.GoalId,
            MaterialClaims = new[] { new MaterialReservationUpsertRequest
            {
                StateHash = request.StateHash, ExpectedLedgerRevision = staged.Revision,
                ReservationId = reservationId, SourceDecisionId = portfolioId, GoalId = request.GoalId,
                NodeId = claim.NodeId, SlotIndex = claim.SlotIndex, QualifiedItemId = claim.QualifiedItemId,
                Quantity = claim.Quantity, Purpose = "retain_verified_acquisition_output_until:" + claim.ConsumerOptionId
            } }
        }, updatedAt);
        if (!result.Accepted || result.Ledger is null) return result;
        result.Ledger.MaterialReservations.Single(row => row.ReservationId == reservationId).OutputCustody = new()
        {
            AcquisitionPortfolioId = request.PortfolioId, AcquisitionRouteSourceDecisionId = request.RouteSourceDecisionId,
            ConsumerOptionId = claim.ConsumerOptionId, FreshTerminalReceiptSha256 = request.FreshTerminalReceiptSha256,
            CreatedLedgerRevision = result.Ledger.Revision
        };
        var history = result.Ledger.History.Single(row => row.CommitmentId == reservationId &&
            row.LedgerRevision == result.Ledger.Revision && row.Operation == "material_reservation_upsert");
        history.Operation = MaterialOutputCustodyContract.CreatedOperation;
        history.Reason = request.FreshTerminalReceiptSha256;
        return result;
    }
}
