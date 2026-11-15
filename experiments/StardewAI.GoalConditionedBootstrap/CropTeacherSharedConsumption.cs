using System.Text.Json.Serialization;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherSharedConsumptionPreparationReference(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("reply_path")] string ReplyPath,
    [property: JsonPropertyName("journal_path")] string JournalPath,
    [property: JsonPropertyName("backend_url")] string BackendUrl);

public sealed record CropTeacherSharedConsumptionSettlementReference(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("reply_path")] string ReplyPath,
    [property: JsonPropertyName("journal_path")] string JournalPath,
    [property: JsonPropertyName("backend_url")] string BackendUrl,
    [property: JsonPropertyName("preparation")] CropTeacherSharedConsumptionPreparationReference Preparation);

internal static class CropTeacherSharedConsumption
{
    internal static StrategyCommitmentLedger VerifyPreparation(SnapshotEnvelope source, StrategyCommitmentLedger before,
        CropTeacherSharedConsumptionPreparationReference reference)
    {
        var (request, reply) = CropTeacherSharedPortfolioMutation.Read<ReservationPortfolioCommitRequest, ReservationPortfolioCommitResult>(
            reference.RequestPath, reference.ReplyPath, reference.JournalPath, reference.BackendUrl, "commit");
        Require(before.PreparedMaterialTransfer is null && before.PreparedInventoryConsumption is null &&
                request.InventoryConsumptionPreparation is not null && reply.Ledger is not null,
            "Shared cooking requires its original explicit consumption preparation.");
        var reasons = new List<string>();
        Require(ReservationPortfolioCommitEvidenceVerifier.ValidateMutation(request, before, reply.Ledger!, source, reply, reasons),
            "Shared cooking preparation does not replay its original commit: " + string.Join(",", reasons));
        var prepared = reply.Ledger!.PreparedInventoryConsumption;
        Require(prepared is not null && PreparedInventoryConsumptionContract.IsBound(reply.Ledger, prepared.BindingSha256,
                prepared.PortfolioId, prepared.GoalId, prepared.RouteSourceDecisionId),
            "Shared cooking preparation lost its original bound inventory consumption.");
        return CropTeacherSeedSupportSource.Copy(reply.Ledger);
    }

    internal static ReservationPortfolioSupportingTransitionSettlementRequest CreateRequest(StrategyCommitmentLedger before,
        VerifiedNativeExecutionQueue native)
    {
        ValidateNative(before, native);
        var prepared = before.PreparedInventoryConsumption!;
        return new()
        {
            StateHash = native.After.StateHash, ExpectedLedgerRevision = before.Revision,
            PortfolioId = prepared.PortfolioId, GoalId = prepared.GoalId, RouteSourceDecisionId = prepared.RouteSourceDecisionId,
            ConsumerOptionId = PreparedInventoryConsumptionContract.Consumer, PreparedInventoryConsumptionSha256 = prepared.BindingSha256,
            SupportingTransitionReceiptSha256 = native.SourceSha256["aggregate"], Reason = "verified_shared_inventory_consumption"
        };
    }

    internal static StrategyCommitmentLedger Verify(StrategyCommitmentLedger before, VerifiedNativeExecutionQueue native,
        CropTeacherSharedConsumptionSettlementReference reference)
    {
        var (request, reply) = CropTeacherSharedPortfolioMutation.Read<ReservationPortfolioSupportingTransitionSettlementRequest,
            ReservationPortfolioSupportingTransitionSettlementResult>(reference.RequestPath, reference.ReplyPath,
            reference.JournalPath, reference.BackendUrl, "settle-supporting-transition");
        return VerifyReply(before, native, request, reply);
    }

    internal static StrategyCommitmentLedger VerifyReply(StrategyCommitmentLedger before, VerifiedNativeExecutionQueue native,
        ReservationPortfolioSupportingTransitionSettlementRequest request, ReservationPortfolioSupportingTransitionSettlementResult reply)
    {
        Require(EqualJson(request, CreateRequest(before, native)) && reply.Accepted && reply.Errors is { Length: 0 } && reply.Ledger is not null,
            "Shared cooking return differs from its original native-bound consumption settlement.");
        var replay = new ReservationPortfolioLedgerService().SettleSupportingTransition(before, native.After, request, reply.Ledger!.UpdatedAt);
        Require(replay.Accepted && EqualJson(replay, reply) && reply.Ledger.PreparedInventoryConsumption is null,
            "Shared cooking return does not replay its complete original settlement.");
        return CropTeacherSeedSupportSource.Copy(reply.Ledger);
    }

    private static void ValidateNative(StrategyCommitmentLedger before, VerifiedNativeExecutionQueue native)
    {
        var prepared = before.PreparedInventoryConsumption;
        Require(prepared is not null && native.Actions.Length == 1 &&
                new ActionQueueDispatchReadinessService().Evaluate(native.Queue, native.Actions[0].Item, before, native.Before.StateHash).Ready &&
                PreparedCookingConsumptionBinding.IsBound(native.Actions[0].Item, before),
            "Shared cooking return differs from its one original prepared native invocation.");
        Require(MaterialInventoryGraphReader.TryRead(native.Before, out var beforeGraph) &&
                InventoryConsumptionProjector.VerifyBefore(prepared!, beforeGraph!).Length == 0,
            "Shared cooking native before differs from its prepared physical inventory.");
        Require(MaterialInventoryGraphReader.TryRead(native.After, out var afterGraph) &&
                InventoryConsumptionProjector.VerifyAfter(prepared!, afterGraph!).Length == 0,
            "Shared cooking native after differs from its original consumption and output.");
    }
}
