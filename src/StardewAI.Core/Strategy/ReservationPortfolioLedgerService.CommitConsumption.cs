using System;
using System.Collections.Generic;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public sealed partial class ReservationPortfolioLedgerService
{
    private static void ValidateCommitMaterialConsumption(ReservationPortfolioCommitRequest request, ICollection<string> errors)
    {
        var value = request.MaterialConsumption;
        if (value is null) return;
        if (value.SchemaVersion != "reservation_portfolio_commit_material_consumption.v1" ||
            value.Consumption is null || string.IsNullOrWhiteSpace(value.RouteSourceDecisionId) ||
            string.IsNullOrWhiteSpace(value.ConsumerOptionId) || string.IsNullOrWhiteSpace(value.Reason) ||
            !MaterialOutputCustodyContract.IsLowerSha256(value.SupportingTransitionReceiptSha256) ||
            request.PortfolioId != request.SourceDecisionId)
            errors.Add("commit_material_consumption_invalid");
        if (request.MaterialClaims is not { Length: 0 } || request.CurrencyClaims is not { Length: 0 } ||
            request.ReleaseReservationIds is not { Length: 0 } || request.MachineSupportIntent is not null ||
            request.MaterialTransferPreparation is not null || request.InventoryConsumptionPreparation is not null)
            errors.Add("commit_material_consumption_mixed_mutation");
    }

    // Stage the existing marker and settlement kernels privately, then publish one revision or neither.
    private ReservationPortfolioSupportingTransitionSettlementResult SettleCommitMaterialConsumption(
        StrategyCommitmentLedger staged, SnapshotEnvelope snapshot, ReservationPortfolioCommitRequest request, string updatedAt)
    {
        var value = request.MaterialConsumption!;
        return SettleSupportingTransition(staged, snapshot, new ReservationPortfolioSupportingTransitionSettlementRequest
        {
            StateHash = request.StateHash, ExpectedLedgerRevision = staged.Revision,
            PortfolioId = request.PortfolioId, GoalId = request.GoalId,
            RouteSourceDecisionId = value.RouteSourceDecisionId, ConsumerOptionId = value.ConsumerOptionId,
            SupportingTransitionReceiptSha256 = value.SupportingTransitionReceiptSha256,
            MaterialConsumptions = new[] { value.Consumption }, Reason = value.Reason
        }, updatedAt);
    }
}
