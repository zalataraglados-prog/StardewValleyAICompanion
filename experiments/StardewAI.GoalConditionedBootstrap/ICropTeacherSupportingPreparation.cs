using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public interface ICropTeacherSupportingPreparation
{
    string SourcePath { get; }
    string QueuePath { get; }
    int TotalDay { get; }
    string CandidateId { get; }
    StrategyCommitmentLedger BaseLedger { get; }
    IReadOnlyDictionary<string, string> InputHashes { get; }
    void ValidateInputsUnchanged();
    ICropTeacherSupportingTransition VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition);
}
