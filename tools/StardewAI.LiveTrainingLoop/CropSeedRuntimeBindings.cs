using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal sealed record CropSeedRuntimeOptions(string BackendUrl, string EvidenceDirectory, string RunId, string ExecutorVersion);

internal sealed record CropSeedRuntimeScopeBinding(string CommitmentId, string SnapshotPath, int TotalDay,
    string TransitionKind, string RuntimeSchema, IReadOnlyDictionary<string, string> InputHashes,
    Func<StrategyCommitmentLedger> ReadBaseLedger, Func<ReservationPortfolioCommitRequest> ReadPreparationRequest,
    Action ValidateInputsUnchanged, Func<ReservationPortfolioCommitResult, CropSeedRuntimeMarkerBinding> VerifyPreparationReply,
    System.Text.Json.Nodes.JsonObject? PreparationDetails = null);

internal sealed record CropSeedRuntimeMarkerBinding(StrategyCommitmentLedger Ledger, Func<string, string> ValidateQueue,
    Func<MoneyRevenueCropNativeLifecycleTransitionRequest, string, string, CropSeedRuntimeSettlementBinding> VerifyTransition);

internal sealed record CropSeedRuntimeSettlementBinding(string ProofPath, string AfterStateHash, bool RequiresAcquisition,
    Func<ReservationPortfolioSupportingTransitionSettlementRequest> ReadSettlementRequest,
    Func<ReservationPortfolioSupportingTransitionSettlementResult, CropSeedRuntimeCustodyBinding> VerifySettlementReply);

internal sealed record CropSeedRuntimeCustodyBinding(StrategyCommitmentLedger Ledger,
    Func<ReservationPortfolioCommitRequest>? ReadAcquiredSeedRequest = null,
    Func<ReservationPortfolioCommitResult, StrategyCommitmentLedger>? VerifyAcquiredSeedReply = null);

internal static class CropSeedRuntimeBindings
{
    internal static CropSeedRuntimeScopeBinding Purchase(CropTeacherSeedPurchaseSupport scope) => new(
        scope.CommitmentId, scope.SnapshotPath, scope.TotalDay, "seed_acquisition_stage", "crop_teacher_seed_support_runtime.v1",
        scope.InputHashes, () => scope.BaseLedger, () => scope.MarkerRequest, scope.ValidateInputsUnchanged, result =>
        {
            var marker = scope.VerifyMarkerReply(result);
            return new(marker.Ledger, marker.ValidateQueue, (transition, proofPath, queueHash) =>
            {
                var verified = marker.VerifyTransition(transition, proofPath, queueHash);
                var proof = verified.Proof;
                var purchase = proof.Stage == "purchase_slice";
                return new(verified.ProofPath, proof.AfterStateHash, purchase, () => verified.SettlementRequest, reply =>
                {
                    var custody = verified.VerifySettlementReply(reply);
                    return purchase ? new(custody.Ledger, () => custody.AcquiredSeedRequest, custody.VerifyAcquiredSeedReply)
                        : new(custody.Ledger);
                });
            });
        });

    internal static CropSeedRuntimeMarkerBinding Transfer(CropTeacherSeedTransferMarker marker) => new(
        marker.Ledger, marker.ValidateQueue, (transition, proofPath, queueHash) =>
        {
            var verified = marker.VerifyTransition(transition, proofPath, queueHash);
            return new(verified.ProofPath, verified.Proof.AfterStateHash, false, () => verified.SettlementRequest,
                reply => new(verified.VerifySettlementReply(reply)));
        });
}
