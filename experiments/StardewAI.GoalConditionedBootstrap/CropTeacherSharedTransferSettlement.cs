using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherSharedTransferSettlementReference(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("reply_path")] string ReplyPath,
    [property: JsonPropertyName("journal_path")] string JournalPath,
    [property: JsonPropertyName("backend_url")] string BackendUrl);

internal static class CropTeacherSharedTransferSettlement
{
    internal static StrategyCommitmentLedger Verify(StrategyCommitmentLedger before, VerifiedNativeExecutionQueue native,
        CropTeacherSharedTransferSettlementReference reference)
    {
        Require(new FileInfo(reference.RequestPath).Length <= ExecutionRequestJournalReader.MaximumRequestBytes &&
                new FileInfo(reference.JournalPath).Length <= 16 * 1024,
            "Shared transfer return exceeds the original RPC journal budget.");
        var request = CurrentTeacherFrontierSupport.Read<ReservationPortfolioSupportingTransitionSettlementRequest>(
            reference.RequestPath, "original shared transfer settlement request");
        var reply = CurrentTeacherFrontierSupport.Read<ReservationPortfolioSupportingTransitionSettlementResult>(
            reference.ReplyPath, "original shared transfer settlement reply");
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

        var requestHash = CurrentTeacherFrontierSupport.HashFile(reference.RequestPath);
        var stem = Path.Combine(Path.GetDirectoryName(reference.JournalPath)!, "portfolio-mutation-" + requestHash);
        Require(CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(reference.RequestPath, stem + "-rpc-request.json") &&
                CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(reference.JournalPath, stem + "-rpc-journal.json"),
            "Shared transfer return lost its original content-addressed RPC paths.");
        var expectedJournal = JsonSerializer.SerializeToNode(new
        {
            schema_version = "stardewai.portfolio_rpc_journal.v1", backend_url = reference.BackendUrl,
            operation = "settle-supporting-transition", request_file = Path.GetFileName(reference.RequestPath),
            request_sha256 = requestHash
        });
        Require(Uri.TryCreate(reference.BackendUrl, UriKind.Absolute, out var origin) && origin.Scheme is "http" or "https" &&
                origin.UserInfo.Length == 0 && origin.Query.Length == 0 && origin.Fragment.Length == 0 &&
                JsonNode.DeepEquals(expectedJournal, CurrentTeacherFrontierSupport.Read<JsonNode>(
                    reference.JournalPath, "original shared transfer RPC journal")),
            "Shared transfer return lost its original settlement RPC identity.");
        Require(reply.Accepted && reply.Errors is { Length: 0 } && reply.Ledger is not null,
            "Shared transfer return has no complete accepted settlement reply.");
        var replay = new ReservationPortfolioLedgerService().SettleSupportingTransition(before, native.After,
            request, reply.Ledger!.UpdatedAt);
        Require(replay.Accepted && EqualJson(replay, reply) && reply.Ledger.PreparedMaterialTransfer is null,
            "Shared transfer return does not replay the original complete settlement.");
        return CropTeacherSeedSupportSource.Copy(reply.Ledger!);
    }
}
