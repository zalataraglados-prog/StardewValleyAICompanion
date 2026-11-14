using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

// Consumes one original compiled till; it never dispatches, claims production or
// commits the owner ledger. The existing supporting protocol owns those steps.
public sealed class CropTeacherSiteTilling : ICropTeacherSupportingTransition
{
    private readonly CropTeacherSiteNativeTransition transition;

    internal CropTeacherSiteTilling(CropTeacherSiteTillCompilation preparation,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string stepId)
    {
        NativeTillingResourceSummary? resources = null;
        this.transition = new(preparation, transition, stepId, "till_soil", frame =>
        {
            var observation = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(preparation.ObservationPath, "selected till bounded inputs");
            resources = NativeOutputObservationBoundary.ReadTillingResources(frame.Request, frame.Result,
                frame.Before, frame.After, observation, "Farm", preparation.Target,
                preparation.ToolSlotIndex, preparation.ToolQualifiedItemId);
        });
        Resources = resources!;
    }

    public NativeTillingResourceSummary Resources { get; }
    public string AfterSnapshotPath => transition.AfterSnapshotPath;
    public bool NativeCropLifecycleVerified => false;
    public bool FormalProductTrainingAuthorized => false;
    public StrategyCommitmentLedger BaseLedger => transition.BaseLedger;
    public ReservationPortfolioCommitRequest RebindRequest => transition.RebindRequest;
    public StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply) => transition.VerifyRebindReply(reply);
    public IReadOnlyDictionary<string, string> InputHashes => transition.InputHashes;
    public void ValidateInputsUnchanged() => transition.ValidateInputsUnchanged();
}
