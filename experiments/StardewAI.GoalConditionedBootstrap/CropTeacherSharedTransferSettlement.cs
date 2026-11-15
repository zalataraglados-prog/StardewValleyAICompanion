using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherSharedTransferSettlementReference(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("reply_path")] string ReplyPath,
    [property: JsonPropertyName("journal_path")] string JournalPath,
    [property: JsonPropertyName("backend_url")] string BackendUrl)
{
    [JsonPropertyName("preparation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CropTeacherSharedTransferPreparationReference? Preparation { get; init; }
}

internal static class CropTeacherSharedTransferSettlement
{
    internal static StrategyCommitmentLedger Verify(StrategyCommitmentLedger before, VerifiedNativeExecutionQueue native,
        CropTeacherSharedTransferSettlementReference reference)
    {
        var (request, reply) = CropTeacherSharedPortfolioMutation.Read<ReservationPortfolioSupportingTransitionSettlementRequest,
            ReservationPortfolioSupportingTransitionSettlementResult>(reference.RequestPath, reference.ReplyPath,
                reference.JournalPath, reference.BackendUrl, "settle-supporting-transition");
        var prepared = before.PreparedMaterialTransfer;
        Require(prepared is not null && request.PreparedMaterialTransferSha256 == prepared.BindingSha256 &&
                request.SupportingTransitionReceiptSha256 == native.SourceSha256["aggregate"] &&
                request.MaterialConsumptions is { Length: 0 } && request.CurrencyConsumptions is { Length: 0 } &&
                request.ConsumedQuantity == 0 && string.IsNullOrEmpty(request.MaterialReservationId) &&
                string.IsNullOrEmpty(request.MachineSupportIntentId) && string.IsNullOrEmpty(request.MachineSupportIntentStage) &&
                request.ConsumerOptionId is null,
            "Shared transfer return requires only the original native-bound transfer settlement.");
        var transfers = native.Actions.Where(row => row.Item.OptionId == "executor.transfer_material").ToArray();
        Require(transfers.Length == 1 && MaterialTransferIntentBinder.TryBuildIntent(
                    transfers[0].Item.NormalizedCommand.Parameters, out var intent) &&
                EqualJson(intent, prepared!.Intent) && new ActionQueueDispatchReadinessService().Evaluate(
                    native.Queue, transfers[0].Item, before, native.Before.StateHash).Ready,
            "Shared transfer return differs from its original prepared native invocation.");

        Require(reply.Accepted && reply.Errors is { Length: 0 } && reply.Ledger is not null,
            "Shared transfer return has no complete accepted settlement reply.");
        var replay = new ReservationPortfolioLedgerService().SettleSupportingTransition(before, native.After,
            request, reply.Ledger!.UpdatedAt);
        Require(replay.Accepted && EqualJson(replay, reply) && reply.Ledger.PreparedMaterialTransfer is null,
            "Shared transfer return does not replay the original complete settlement.");
        return CropTeacherSeedSupportSource.Copy(reply.Ledger!);
    }
}
