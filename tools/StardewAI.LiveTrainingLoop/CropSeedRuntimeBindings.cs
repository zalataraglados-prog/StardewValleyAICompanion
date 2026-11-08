using System.Text.Json;
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
    Func<MoneyRevenueCropNativeLifecycleTransitionRequest, string, string, CropSeedRuntimeSettlementBinding> VerifyTransition,
    Func<MoneyRevenueCropNativeLifecycleTransitionRequest, string, string, string, CropSeedRuntimeSettlementBinding> ResumeTransition,
    Func<string, string, CropTeacherSeedPlantingPrefix>? OpenPlantingPrefix = null);

internal sealed record CropSeedRuntimeSettlementBinding(string ProofPath, string AfterStateHash, bool RequiresAcquisition,
    Func<ReservationPortfolioSupportingTransitionSettlementRequest> ReadSettlementRequest,
    Func<ReservationPortfolioSupportingTransitionSettlementResult, CropSeedRuntimeCustodyBinding> VerifySettlementReply);

internal sealed record CropSeedRuntimeCustodyBinding(StrategyCommitmentLedger Ledger,
    Func<ReservationPortfolioCommitRequest>? ReadAcquiredSeedRequest = null,
    Func<ReservationPortfolioCommitResult, StrategyCommitmentLedger>? VerifyAcquiredSeedReply = null);

internal static class CropSeedRuntimeBindings
{
    internal static CropSeedRuntimeScopeBinding Transfer(CropTeacherSeedTransferSupport scope,
        Action<CropTeacherSeedTransferMarker>? onMarker = null) => new(scope.CommitmentId, scope.SnapshotPath, scope.TotalDay,
        "owned_seed_transfer", "crop_teacher_seed_transfer_runtime.v1", scope.InputHashes, () => scope.BaseLedger,
        () => scope.PreparationRequest, scope.ValidateInputsUnchanged, reply =>
        {
            var marker = scope.VerifyPreparationReply(reply);
            onMarker?.Invoke(marker);
            return Transfer(marker);
        });

    internal static CropSeedRuntimeScopeBinding Planting(CropTeacherSeedPlantingSupport scope,
        MoneyRevenueCropNativeProductionActionBatch requested, MoneyRevenueCropNativeProductionActionBatch bounded,
        string? reservationId, Action<CropTeacherSeedPlantingMarker>? onMarker = null) => new(
        scope.CommitmentId, scope.SnapshotPath, scope.TotalDay, "owned_seed_planting", "crop_teacher_seed_planting_runtime.v1",
        scope.InputHashes, () => scope.BaseLedger, () => scope.MarkerRequest, scope.ValidateInputsUnchanged, reply =>
        {
            var marker = scope.VerifyMarkerReply(reply);
            onMarker?.Invoke(marker);
            return Planting(marker);
        }, JsonSerializer.SerializeToNode(new { requested_batch = requested, bound_batch = bounded, reservation_id = reservationId },
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject());

    internal static CropSeedRuntimeScopeBinding Purchase(CropTeacherSeedPurchaseSupport scope) => new(
        scope.CommitmentId, scope.SnapshotPath, scope.TotalDay, "seed_acquisition_stage", "crop_teacher_seed_support_runtime.v1",
        scope.InputHashes, () => scope.BaseLedger, () => scope.MarkerRequest, scope.ValidateInputsUnchanged, result =>
        {
            var marker = scope.VerifyMarkerReply(result);
            return new(marker.Ledger, marker.ValidateQueue,
                (transition, path, hash) => PurchaseSettlement(marker.VerifyTransition(transition, path, hash)),
                (transition, path, hash, proof) => PurchaseSettlement(marker.ResumeTransition(transition, path, hash, proof)));
        });

    internal static CropSeedRuntimeMarkerBinding Transfer(CropTeacherSeedTransferMarker marker) => new(
        marker.Ledger, marker.ValidateQueue,
        (transition, path, hash) => TransferSettlement(marker.VerifyTransition(transition, path, hash)),
        (transition, path, hash, proof) => TransferSettlement(marker.ResumeTransition(transition, path, hash, proof)));

    internal static CropSeedRuntimeMarkerBinding Planting(CropTeacherSeedPlantingMarker marker) => new(
        marker.Ledger, marker.ValidateQueue,
        (transition, path, hash) => PlantingSettlement(marker.VerifyTransition(transition, path, hash)),
        (transition, path, hash, proof) => PlantingSettlement(marker.ResumeTransition(transition, path, hash, proof)), marker.OpenPrefix);

    private static CropSeedRuntimeSettlementBinding PurchaseSettlement(CropTeacherSeedPurchaseSettlement verified)
    {
        var proof = verified.Proof;
        var purchase = proof.Stage == "purchase_slice";
        return new(verified.ProofPath, proof.AfterStateHash, purchase, () => verified.SettlementRequest, reply =>
        {
            var custody = verified.VerifySettlementReply(reply);
            return purchase ? new(custody.Ledger, () => custody.AcquiredSeedRequest, custody.VerifyAcquiredSeedReply)
                : new(custody.Ledger);
        });
    }

    private static CropSeedRuntimeSettlementBinding TransferSettlement(CropTeacherSeedTransferSettlement verified) =>
        new(verified.ProofPath, verified.Proof.AfterStateHash, false, () => verified.SettlementRequest,
            reply => new(verified.VerifySettlementReply(reply)));

    private static CropSeedRuntimeSettlementBinding PlantingSettlement(CropTeacherSeedPlantingSettlement verified) =>
        new(verified.ProofPath, verified.Proof.AfterStateHash, false, () => verified.SettlementRequest,
            reply => new(verified.VerifySettlementReply(reply)));
}
