using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public interface ICropTeacherSeedRuntime
{
    void ValidateDispatchQueue(string path);
    Task<StrategyLedgerEvidence> CompleteAsync(string queuePath, string receiptPath, string afterSnapshotPath, string selectedCandidateId);
    Task<StrategyLedgerEvidence> CompleteAsync(MoneyRevenueCropNativeLifecycleTransitionRequest transition);
}
