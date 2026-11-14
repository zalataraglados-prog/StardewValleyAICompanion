namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropProductionProofCheckpointBuilder
{
    private const string NativeCheckpointRequestSchema = "money_revenue_crop_production_proof_checkpoint_request.v13";
    private const string NativeCheckpointSchema = "money_revenue_crop_production_proof_checkpoint.v13";
    private const string NativeCapacitySchema = "money_revenue_crop_daily_capacity_proof.v2";
    private const string NativeTerminalSchema = "crop_teacher_native_variant_terminal_proof.v1";

    public static MoneyRevenueCropProductionProofCheckpoint BuildNativeObserved(string requestPath) =>
        BuildCore(requestPath, nativeObserved: true);

    private static MoneyRevenueCropDailyCapacityProof RebuildCollectedCapacity(MoneyRevenueCropDailyCapacityProof proof,
        bool nativeObserved) => nativeObserved && proof.SchemaVersion == NativeCapacitySchema
        ? MoneyRevenueCropDailyCapacityProofBuilder.BuildNativeObserved(proof.RequestPath)
        : MoneyRevenueCropDailyCapacityProofBuilder.Build(proof.RequestPath);

    private static MoneyRevenueCropTerminalSettlementProof RebuildCollectedTerminal(MoneyRevenueCropTerminalSettlementProof proof,
        bool nativeObserved) => nativeObserved && proof.SchemaVersion == NativeTerminalSchema
        ? CropTeacherNativeVariantTerminalProof.Build(proof.RequestPath)
        : MoneyRevenueCropTerminalSettlementProofBuilder.Build(proof.RequestPath);
}
