namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherRefillNativeOutcomeStep(string QueueItemId, string OptionId, string BeforeStateHash, string AfterStateHash);

public sealed partial class CropTeacherRefillApproachNativeTrace
{
    public IReadOnlyList<CropTeacherRefillNativeOutcomeStep> NativeOutcomes { get; }
    public bool NativeEngineEffectsObserved { get; }

    public static CropTeacherRefillApproachNativeTrace OpenNativeObservedEffects(CropTeacherRefillApproachCompilation compilation,
        string executionReceiptPath, string afterSnapshotPath, string runId, string executorVersion) =>
        new(compilation, executionReceiptPath, afterSnapshotPath, runId, verifySnapshotInventoryContinuity: false,
            nativeExecutorVersion: RequireNativeExecutorVersion(executorVersion), nativeObservedEffects: true);

    private static void VerifyNativeObservedTarget(NativeExecutionActionFrame frame)
    {
        var x = frame.Request.TargetTileX ?? throw new InvalidDataException("Native refill lacks its compiled target x.");
        var y = frame.Request.TargetTileY ?? throw new InvalidDataException("Native refill lacks its compiled target y.");
        if (frame.Request.OptionId == "executor.clear_obstacle") CropTeacherSiteClearance.VerifyNativeTarget(frame, x, y);
        else
            Require(MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(frame.After, "player", "tile_x") == x &&
                MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(frame.After, "player", "tile_y") == y,
                "native refill move did not reach its original compiled target");
    }
}
