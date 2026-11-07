using StardewAI.Contracts.Strategy;
using static StardewAI.GoalConditionedBootstrap.CropTeacherSeedPurchaseSupport;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSeedPurchaseMarker
{
    private readonly CropTeacherSeedPurchaseSupport scope;
    private readonly StrategyCommitmentLedger ledger;

    internal CropTeacherSeedPurchaseMarker(CropTeacherSeedPurchaseSupport scope, StrategyCommitmentLedger ledger)
    {
        this.scope = scope;
        this.ledger = ledger;
    }

    public StrategyCommitmentLedger Ledger => Copy(ledger);
    public string ValidateQueue(string path) => scope.ValidateQueue(path);

    public CropTeacherSeedPurchaseSettlement VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        string proofOutputPath, string preparedQueueSha256) =>
        scope.VerifyTransitionCore(ledger, transition, proofOutputPath, preparedQueueSha256, null);

    public CropTeacherSeedPurchaseSettlement ResumeTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        string proofPath, string preparedQueueSha256, string expectedProofSha256)
    {
        Require(IsLowerSha256(expectedProofSha256), "Crop seed support resume proof hash is invalid.");
        return scope.VerifyTransitionCore(ledger, transition, proofPath, preparedQueueSha256, expectedProofSha256);
    }
}
