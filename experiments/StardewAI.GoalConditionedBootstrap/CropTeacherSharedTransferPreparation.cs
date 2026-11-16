using System.Text.Json.Serialization;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherSharedTransferPreparationReference(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("reply_path")] string ReplyPath,
    [property: JsonPropertyName("journal_path")] string JournalPath,
    [property: JsonPropertyName("backend_url")] string BackendUrl);

internal static class CropTeacherSharedTransferPreparation
{
    internal static StrategyCommitmentLedger Verify(SnapshotEnvelope source, StrategyCommitmentLedger before,
        CropTeacherSharedTransferPreparationReference reference)
    {
        var (request, reply) = CropTeacherSharedPortfolioMutation.Read<ReservationPortfolioCommitRequest, ReservationPortfolioCommitResult>(
            reference.RequestPath, reference.ReplyPath, reference.JournalPath, reference.BackendUrl, "commit");
        return VerifyReply(source, before, request, reply);
    }

    internal static StrategyCommitmentLedger VerifyReply(SnapshotEnvelope source, StrategyCommitmentLedger before,
        ReservationPortfolioCommitRequest request, ReservationPortfolioCommitResult reply)
    {
        Require(before.PreparedMaterialTransfer is null && request.MaterialTransferPreparation is not null && request.MaterialConsumption is null,
            "Shared transfer return requires the original non-consuming preparation from its owner source.");
        var reasons = new List<string>();
        Require(reply.Ledger is not null && ReservationPortfolioCommitEvidenceVerifier.ValidateMutation(
                request, before, reply.Ledger, source, reply, reasons),
            "Shared transfer preparation does not replay its complete original commit: " + string.Join(",", reasons));
        var prepared = reply.Ledger!.PreparedMaterialTransfer;
        Require(prepared is not null && PreparedMaterialTransferContract.IsBound(reply.Ledger, prepared.BindingSha256,
                prepared.PortfolioId, prepared.GoalId, prepared.RouteSourceDecisionId),
            "Shared transfer preparation has no original bound transfer.");
        return CropTeacherSeedSupportSource.Copy(reply.Ledger!);
    }
}
