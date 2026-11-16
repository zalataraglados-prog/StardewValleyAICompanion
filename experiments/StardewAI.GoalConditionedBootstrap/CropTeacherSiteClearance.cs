using System.Text.Json;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSiteClearance : ICropTeacherSupportingTransition
{
    private readonly CropTeacherSiteNativeTransition transition;

    internal CropTeacherSiteClearance(CropTeacherSiteClearanceCompilation preparation,
        MoneyRevenueCropNativeLifecycleTransitionRequest request, string stepId)
    {
        transition = new(preparation, request, stepId, "clear_obstacle", frame =>
            VerifyNativeTarget(frame, preparation.Target.TileX, preparation.Target.TileY));
    }

    internal static void VerifyNativeTarget(NativeExecutionActionFrame frame, int x, int y)
    {
        var beforeLocation = NativeExecutionSourceSupport.Value(frame.Before, "player", "location_id");
        var afterLocation = NativeExecutionSourceSupport.Value(frame.After, "player", "location_id");
        var explicitTarget = frame.Result.TargetLocation == "Farm" &&
            frame.Result.TargetTileX == x && frame.Result.TargetTileY == y;
        // The original clear writer binds the journal target through RequestedEffect,
        // not the optional duplicate result coordinates used by other primitives.
        var originalTarget = frame.Result.TargetLocation == string.Empty &&
            frame.Result.TargetTileX is null && frame.Result.TargetTileY is null &&
            frame.Result.RequestedEffect == $"current_location.obstacle[{x},{y}]=clear";
        if (frame.Request.TargetTileX != x || frame.Request.TargetTileY != y ||
            beforeLocation.ValueKind != JsonValueKind.String || beforeLocation.GetString() != "Farm" ||
            afterLocation.ValueKind != JsonValueKind.String || afterLocation.GetString() != "Farm" ||
            !(explicitTarget || originalTarget))
            throw new InvalidDataException("Native clearance did not execute its original selected target.");
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
