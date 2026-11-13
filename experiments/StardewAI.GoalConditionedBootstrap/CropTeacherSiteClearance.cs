using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSiteClearance : ICropTeacherSupportingTransition
{
    private readonly CropTeacherSiteNativeTransition transition;

    internal CropTeacherSiteClearance(CropTeacherSiteClearanceCompilation preparation,
        MoneyRevenueCropNativeLifecycleTransitionRequest request, string stepId)
    {
        transition = new(preparation, request, stepId, "clear_obstacle", frame =>
        {
            if (frame.Result.TargetLocation != "Farm" ||
                frame.Request.TargetTileX != preparation.Target.TileX || frame.Request.TargetTileY != preparation.Target.TileY ||
                frame.Result.TargetTileX != preparation.Target.TileX || frame.Result.TargetTileY != preparation.Target.TileY)
                throw new InvalidDataException("Native clearance did not execute its original selected target.");
        });
    }

    public bool NativeOutcomeVerified => true;
    public string OutcomeAuthority => "native_engine";
    public bool ObservedAfterStateRetained => true;
    public bool PrivateResourceEffectsPredicted => false;
    public bool ProducerProvenanceVerified => false;
    public bool ResourceCreditAuthorized => false;
    public string AfterSnapshotPath => transition.AfterSnapshotPath;
    public bool NativeCropLifecycleVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public StrategyCommitmentLedger BaseLedger => transition.BaseLedger;
    public ReservationPortfolioCommitRequest RebindRequest => transition.RebindRequest;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply) => transition.VerifyRebindReply(reply);
    public IReadOnlyDictionary<string, string> InputHashes => transition.InputHashes;
    public void ValidateInputsUnchanged() => transition.ValidateInputsUnchanged();
}
